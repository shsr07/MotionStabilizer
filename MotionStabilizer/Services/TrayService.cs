using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using MotionStabilizer.Views;

namespace MotionStabilizer.Services;

/// <summary>
/// System tray icon service using Windows.Forms NotifyIcon.
/// Provides quick access to show/hide the main window and exit.
/// </summary>
public class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private bool _disposed;

    // Track GDI resources for proper cleanup
    private Icon? _customIcon;
    private IntPtr _iconHandle = IntPtr.Zero;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public TrayService()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Visible = false,
            Text = "Motion Stabilizer"
        };

        var contextMenu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("显示主窗口 / Show");
        showItem.Click += (_, _) => ShowMainWindow();
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("退出 / Exit");
        exitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            Application.Current.Shutdown();
        };
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    private void ShowMainWindow()
    {
        var mainWin = Application.Current.MainWindow;
        if (mainWin != null)
        {
            mainWin.Show();
            mainWin.WindowState = WindowState.Normal;
            mainWin.Activate();
        }
    }

    /// <summary>Create a simple icon programmatically (green circle with crosshair).</summary>
    private Icon CreateDefaultIcon()
    {
        try
        {
            using var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                // Dark circle background
                using var bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(28, 28, 30));
                g.FillEllipse(bgBrush, 2, 2, 28, 28);
                // Green ring
                using var greenPen = new Pen(System.Drawing.Color.FromArgb(0, 230, 118), 2);
                g.DrawEllipse(greenPen, 4, 4, 24, 24);
                // Crosshair
                using var whitePen = new Pen(System.Drawing.Color.White, 1);
                g.DrawLine(whitePen, 16, 10, 16, 22);
                g.DrawLine(whitePen, 10, 16, 22, 16);
            }

            // GetHicon() allocates an unmanaged GDI icon handle that MUST be freed with DestroyIcon.
            // We clone the icon from the handle so the Icon object owns its own copy,
            // then immediately destroy the original handle to prevent leakage.
            _iconHandle = bitmap.GetHicon();
            _customIcon = (Icon)Icon.FromHandle(_iconHandle).Clone();
            // The cloned Icon is independent; we can safely destroy the original handle now.
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
            return _customIcon;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();

            // Clean up the custom icon GDI resource
            _customIcon?.Dispose();
            _customIcon = null;

            // If the handle wasn't destroyed during Clone (edge case), clean it up now
            if (_iconHandle != IntPtr.Zero)
            {
                DestroyIcon(_iconHandle);
                _iconHandle = IntPtr.Zero;
            }

            _disposed = true;
        }
    }
}
