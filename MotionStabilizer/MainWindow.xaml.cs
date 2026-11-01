using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MotionStabilizer.Models;
using MotionStabilizer.Services;
using MotionStabilizer.Views;

namespace MotionStabilizer;

/// <summary>
/// Main settings console with sidebar navigation and content area.
/// Hosts the 5 settings pages: Overlay, Crosshair, Clock, Hotkeys, Options.
/// </summary>
public partial class MainWindow : Window
{
    private readonly OverlayPage _overlayPage = new();
    private readonly CrosshairPage _crosshairPage = new();
    private readonly ClockPage _clockPage = new();
    private readonly HotkeysPage _hotkeysPage = new();
    private readonly OptionsPage _optionsPage = new();

    /// <summary>Tracks which page is currently active (for hotkey context).</summary>
    public string CurrentPage { get; private set; } = "Overlay";

    /// <summary>Flag to allow actual window close during app shutdown.</summary>
    private bool _isShuttingDown = false;

    public MainWindow()
    {
        InitializeComponent();
        try { Icon = AppIcon.CreateImageSource(); }
        catch { /* icon generation is non-critical */ }
        ContentFrame.Navigate(_overlayPage);
        StateChanged += MainWindow_StateChanged;

        Loaded += (_, _) =>
        {
            // Set as main window for tray service
            if (Application.Current.MainWindow != this)
                Application.Current.MainWindow = this;
        };

        // Enable native window resize for WindowStyle=None + AllowsTransparency=True
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var hwndSource = HwndSource.FromHwnd(hwnd);
            hwndSource?.AddHook(WndProc);
        };
    }

    // ── Window resize support for borderless window ──────────────────

    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int ResizeMargin = 6;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Second instance asked us to show the main window
        if (msg == App.ShowMainWindowMessage)
        {
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == WM_NCHITTEST)
        {
            // Get screen coordinates of the mouse
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

            // Convert to window coordinates
            var screenPoint = new Point(x, y);
            var windowPoint = PointFromScreen(screenPoint);

            double w = ActualWidth;
            double h = ActualHeight;

            bool onLeft = windowPoint.X <= ResizeMargin;
            bool onRight = windowPoint.X >= w - ResizeMargin;
            bool onTop = windowPoint.Y <= ResizeMargin;
            bool onBottom = windowPoint.Y >= h - ResizeMargin;

            if (onTop && onLeft) { handled = true; return (IntPtr)HTTOPLEFT; }
            if (onTop && onRight) { handled = true; return (IntPtr)HTTOPRIGHT; }
            if (onBottom && onLeft) { handled = true; return (IntPtr)HTBOTTOMLEFT; }
            if (onBottom && onRight) { handled = true; return (IntPtr)HTBOTTOMRIGHT; }
            if (onLeft) { handled = true; return (IntPtr)HTLEFT; }
            if (onRight) { handled = true; return (IntPtr)HTRIGHT; }
            if (onTop) { handled = true; return (IntPtr)HTTOP; }
            if (onBottom) { handled = true; return (IntPtr)HTBOTTOM; }
        }
        return IntPtr.Zero;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender == NavOverlay)
        {
            ContentFrame.Navigate(_overlayPage);
        StateChanged += MainWindow_StateChanged;
            CurrentPage = "Overlay";
        }
        else if (sender == NavCrosshair)
        {
            ContentFrame.Navigate(_crosshairPage);
            CurrentPage = "Crosshair";
        }
        else if (sender == NavClock)
        {
            ContentFrame.Navigate(_clockPage);
            CurrentPage = "Clock";
        }
        else if (sender == NavHotkeys)
        {
            ContentFrame.Navigate(_hotkeysPage);
            CurrentPage = "Hotkeys";
        }
        else if (sender == NavOptions)
        {
            ContentFrame.Navigate(_optionsPage);
            CurrentPage = "Options";
        }

        NotifyConfigChanged();
    }

    /// <summary>Called when clock drag is confirmed via left-click.</summary>
    public void NotifyClockDragConfirmed() => _clockPage.OnClockDragConfirmed();

    /// <summary>
    /// Called when configs change (via hotkey, profile load, or reset) to refresh pages.
    /// Note: overlay re-render and auto-save are handled automatically by ConfigStore.Changed.
    /// This method only refreshes the page UIs.
    /// </summary>
    public void NotifyConfigChanged()
    {
        _overlayPage.RefreshFromConfig();
        _crosshairPage.RefreshFromConfig();
        _clockPage.RefreshFromConfig();
        _hotkeysPage.RefreshFromConfig();
        _optionsPage.RefreshFromConfig();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        if (App.AppConfig.HideFromTaskbarOnMinimize)
        {
            this.Hide();
            return;
        }

        // Keep the taskbar button: minimize normally, with a Win11-style
        // fade + shrink-toward-taskbar animation.
        AnimateMinimizeOut();
    }

    // ── Win11-style minimize/restore animation ──

    private readonly ScaleTransform _minimizeScale = new(1, 1);
    private readonly TranslateTransform _minimizeTranslate = new();
    private TransformGroup? _minimizeTransformGroup;
    private bool _minimizeAnimOutRunning;
    private bool _restoreFromMinimize;

    private void ApplyMinimizeTransform()
    {
        if (_minimizeTransformGroup == null)
        {
            _minimizeTransformGroup = new TransformGroup();
            _minimizeTransformGroup.Children.Add(_minimizeScale);
            _minimizeTransformGroup.Children.Add(_minimizeTranslate);
        }
        // Shrink around the bottom edge — toward the taskbar. Transforms go on
        // the root Border: a Window does not support RenderTransform.
        RootBorder.RenderTransformOrigin = new Point(0.5, 1);
        RootBorder.RenderTransform = _minimizeTransformGroup;
    }

    private void SnapMinimizeState()
    {
        ApplyMinimizeTransform();
        _minimizeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _minimizeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _minimizeTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        _minimizeScale.ScaleX = 0.8;
        _minimizeScale.ScaleY = 0.8;
        _minimizeTranslate.Y = 40;
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
    }

    private void AnimateMinimizeOut()
    {
        if (_minimizeAnimOutRunning) return;
        _minimizeAnimOutRunning = true;

        ApplyMinimizeTransform();
        var dur = TimeSpan.FromMilliseconds(180);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };

        var sx = new DoubleAnimation(1, 0.8, dur) { EasingFunction = ease };
        var sy = new DoubleAnimation(1, 0.8, dur) { EasingFunction = ease };
        var ty = new DoubleAnimation(0, 40, dur) { EasingFunction = ease };
        var fade = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            _minimizeAnimOutRunning = false;
            _restoreFromMinimize = true;
            WindowState = WindowState.Minimized;
        };

        _minimizeScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        _minimizeScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        _minimizeTranslate.BeginAnimation(TranslateTransform.YProperty, ty);
        BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateRestoreIn()
    {
        ApplyMinimizeTransform();
        var dur = TimeSpan.FromMilliseconds(200);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        _minimizeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _minimizeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _minimizeTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        _minimizeScale.ScaleX = 0.8;
        _minimizeScale.ScaleY = 0.8;
        _minimizeTranslate.Y = 40;
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;

        var sx = new DoubleAnimation(0.8, 1, dur) { EasingFunction = ease };
        var sy = new DoubleAnimation(0.8, 1, dur) { EasingFunction = ease };
        var ty = new DoubleAnimation(40, 0, dur) { EasingFunction = ease };
        var fade = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };
        fade.Completed += (_, _) => ResetMinimizeVisualState();

        _minimizeScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        _minimizeScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        _minimizeTranslate.BeginAnimation(TranslateTransform.YProperty, ty);
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Clear every minimize-animation artefact (transform, opacity,
    /// running animations) so the window renders in its normal state.</summary>
    private void ResetMinimizeVisualState()
    {
        _minimizeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _minimizeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _minimizeTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        BeginAnimation(OpacityProperty, null);
        RootBorder.RenderTransform = null;
        Opacity = 1;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            // Invisible while minimized — pre-set the restore start state so
            // the window doesn't flash at full opacity when restored.
            if (!_minimizeAnimOutRunning)
                SnapMinimizeState();
            _restoreFromMinimize = true;
        }
        else if (WindowState == WindowState.Normal && _restoreFromMinimize)
        {
            _restoreFromMinimize = false;
            AnimateRestoreIn();
        }
        else
        {
            // Maximized or other states — drop any animation artefacts so the
            // window never gets stuck faded/shrunk.
            _restoreFromMinimize = false;
            ResetMinimizeVisualState();
        }
    }

    /// <summary>
    /// Shared close path for the ✕ button and Alt+F4: shows the confirmation
    /// dialog (if enabled), then either minimizes to tray or exits.
    /// </summary>
    private void RequestClose()
    {
        if (App.AppConfig.ConfirmBeforeClose)
        {
            var msg = (string)FindResource("ConfirmClose_Msg");
            var title = (string)FindResource("ConfirmClose_Title");
            var yesText = (string)FindResource("ConfirmClose_Yes");
            var noText = (string)FindResource("ConfirmClose_No");

            var result = CustomMessageBox.Show(title, msg, yesText, noText);
            if (result == CustomMessageBox.Result.Option2)
            {
                // Minimize to tray
                this.Hide();
                return;
            }
        }

        // Allow the window to close and shut down the app
        _isShuttingDown = true;
        Application.Current.Shutdown();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => RequestClose();

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
        => ProfileService.SaveProfile();

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
        => ProfileService.LoadProfile();

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        => ProfileService.DeleteProfile();

    protected override void OnClosing(CancelEventArgs e)
    {
        // During app shutdown, allow the window to close
        if (_isShuttingDown)
        {
            base.OnClosing(e);
            return;
        }

        // Alt+F4 behaves exactly like the ✕ button
        e.Cancel = true;
        RequestClose();
        base.OnClosing(e);
    }
}
