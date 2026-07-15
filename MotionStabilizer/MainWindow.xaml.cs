﻿using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
        ContentFrame.Navigate(_overlayPage);

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

    /// <summary>Called when configs change (via hotkey or UI) to refresh pages.</summary>
    public void NotifyConfigChanged()
    {
        _overlayPage.RefreshFromConfig();
        _crosshairPage.RefreshFromConfig();
        _clockPage.RefreshFromConfig();
        _hotkeysPage.RefreshFromConfig();
        _optionsPage.RefreshFromConfig();
        App.RefreshOverlay();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
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

        // Otherwise, hide instead of closing (minimize to tray)
        e.Cancel = true;
        this.Hide();
        base.OnClosing(e);
    }
}
