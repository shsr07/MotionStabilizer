using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pen = System.Drawing.Pen;

namespace MotionStabilizer.Services;

/// <summary>
/// Provides the application icon (green ring with crosshair) for both the
/// system tray (WinForms NotifyIcon) and the WPF main window taskbar icon.
/// Centralises the GDI+ drawing so the tray and taskbar stay in sync.
/// </summary>
public static class AppIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>Render the icon bitmap (32×32): dark disc + green ring + white crosshair.</summary>
    public static Bitmap CreateBitmap()
    {
        var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.Transparent);
            using var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(28, 28, 30));
            g.FillEllipse(bgBrush, 2, 2, 28, 28);
            using var greenPen = new Pen(System.Drawing.Color.FromArgb(0, 230, 118), 2);
            g.DrawEllipse(greenPen, 4, 4, 24, 24);
            using var whitePen = new Pen(System.Drawing.Color.White, 1);
            g.DrawLine(whitePen, 16, 10, 16, 22);
            g.DrawLine(whitePen, 10, 16, 22, 16);
        }
        return bitmap;
    }

    /// <summary>Create a WinForms Icon for the system tray.</summary>
    public static Icon CreateIcon()
    {
        using var bitmap = CreateBitmap();
        var handle = bitmap.GetHicon();
        // Clone so the Icon owns its own copy, then destroy the original handle.
        var icon = (Icon)Icon.FromHandle(handle).Clone();
        DestroyIcon(handle);
        return icon;
    }

    /// <summary>Create a WPF ImageSource for the window taskbar icon.</summary>
    public static ImageSource CreateImageSource()
    {
        using var bitmap = CreateBitmap();
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
