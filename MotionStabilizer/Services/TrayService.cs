using System.Drawing;
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
    private Icon? _customIcon;
    private bool _disposed;

    public TrayService()
    {
        _customIcon = AppIcon.CreateIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _customIcon,
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _customIcon?.Dispose();
            _customIcon = null;
            _disposed = true;
        }
    }
}
