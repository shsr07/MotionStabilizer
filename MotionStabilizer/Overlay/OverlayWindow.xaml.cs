using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MotionStabilizer.Models;
using MotionStabilizer.Services;

namespace MotionStabilizer.Overlay;

/// <summary>
/// The invisible rendering layer. This is a pure external transparent overlay window.
/// It draws overlay shapes, crosshair, and clock on top of all games.
/// 
/// Key properties:
/// - Click-through: mouse events pass through to the game below
/// - No-activate: never steals focus from the game
/// - Topmost: always rendered on top
/// - Transparent: only drawn shapes are visible
/// 
/// This approach is 100% external — no DLL injection, no process modification,
/// no memory access. It is safe under all anti-cheat systems (Vanguard, EAC, BattlEye).
/// </summary>
public partial class OverlayWindow : Window
{
    private OverlayConfig _overlayConfig = new();
    private CrosshairConfig _crosshairConfig = new();
    private ClockConfig _clockConfig = new();

    private DispatcherTimer? _clockTimer;
    private TextBlock? _clockText;
    private bool _isClockDragging;
    private DispatcherTimer? _dragTimer;
    private Point _clockDragOffset;
    private bool _wasLeftButtonDown;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OverlayWindow_Loaded;
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply Win32 extended styles for click-through + no-activate
        var helper = new WindowInteropHelper(this);
        Win32Interop.MakeOverlayWindow(helper.Handle);

        // Size to full screen
        UpdateScreenBounds();

        // Start clock timer
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        Render();
    }

    /// <summary>Update window bounds to cover the full screen.</summary>
    public void UpdateScreenBounds()
    {
        // GetScreenWidth/Height return physical pixels; WPF Window.Width is in DIP.
        // Convert physical → DIP so the window covers the full physical screen.
        double scale = Win32Interop.GetDpiScale();
        double w = Win32Interop.GetScreenWidth() / scale;
        double h = Win32Interop.GetScreenHeight() / scale;
        this.Left = 0;
        this.Top = 0;
        this.Width = w;
        this.Height = h;
        OverlayCanvas.Width = w;
        OverlayCanvas.Height = h;
    }

    /// <summary>Update all configs and re-render.</summary>
    public void UpdateConfigs(OverlayConfig overlay, CrosshairConfig crosshair, ClockConfig clock)
    {
        _overlayConfig = overlay;
        _crosshairConfig = crosshair;
        _clockConfig = clock;
        Render();
    }

    /// <summary>Re-render all overlay elements on the canvas.</summary>
    public void Render()
    {
        OverlayCanvas.Children.Clear();
        _clockText = null;

        double sw = this.Width > 0 ? this.Width : Win32Interop.GetScreenWidth() / Win32Interop.GetDpiScale();
        double sh = this.Height > 0 ? this.Height : Win32Interop.GetScreenHeight() / Win32Interop.GetDpiScale();

        // Render edge overlay
        if (_overlayConfig.IsVisible)
        {
            var area = new Rect(0, 0, sw, sh);
            var overlayShapes = RenderHelper.BuildOverlayShapes(_overlayConfig, area, sw, sh);
            foreach (var s in overlayShapes)
                OverlayCanvas.Children.Add(s);
        }

        // Render crosshair
        if (_crosshairConfig.IsVisible)
        {
            var crosshairShapes = RenderHelper.BuildCrosshairShapes(_crosshairConfig, sw, sh);
            foreach (var s in crosshairShapes)
                OverlayCanvas.Children.Add(s);
        }

        // Render clock
        if (_clockConfig.IsVisible)
        {
            RenderClock(sw, sh);
        }
    }

    private void RenderClock(double sw, double sh)
    {
        _clockText = new TextBlock
        {
            FontFamily = new FontFamily(_clockConfig.GetRenderFontFamily()),
            FontSize = _clockConfig.FontSize,
            Foreground = new SolidColorBrush(Color.FromArgb(
                RenderHelper.OpacityToByte(_clockConfig.Opacity),
                _clockConfig.GetColor().R,
                _clockConfig.GetColor().G,
                _clockConfig.GetColor().B)),
            // Nearly-invisible background (alpha=1) so the entire TextBlock bounding box
            // is hit-testable by the OS compositor on AllowsTransparency windows.
            // Without this, only the text glyphs themselves receive mouse clicks.
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Padding = new Thickness(8)
        };

        // Apply built-in outline effect when the Outline pseudo-font is selected
        if (_clockConfig.IsOutlineFont)
        {
            _clockText.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 4,
                ShadowDepth = 0,
                Opacity = 1
            };
        }

        Canvas.SetLeft(_clockText, _clockConfig.PositionX);
        Canvas.SetTop(_clockText, _clockConfig.PositionY);
        OverlayCanvas.Children.Add(_clockText);

        UpdateClock();
    }

    private void UpdateClock()
    {
        if (_clockText == null || !_clockConfig.IsVisible) return;

        var now = DateTime.Now;
        var text = _clockConfig.Format switch
        {
            ClockFormat.HHmm => now.ToString("HH:mm"),
            ClockFormat.HHmmss => now.ToString("HH:mm:ss"),
            // am/pm h:mm — 12-hour clock, midnight/noon shown as 12
            ClockFormat.HhMmAmPm => $"{now:tt} {now:hh}:{now:mm}",
            _ => now.ToString("HH:mm")
        };
        _clockText.Text = text;
    }

    /// <summary>
    /// Enable clock dragging via cursor tracking.
    /// The clock follows the mouse cursor; left-click confirms the position.
    /// The overlay stays fully click-through — no UI is blocked.
    /// </summary>
    public void EnableClockDrag()
    {
        _isClockDragging = true;
        _wasLeftButtonDown = true; // ignore the initial click that started dragging

        // Set offset to zero so the clock teleports to the cursor position
        _clockDragOffset = new Point(0, 0);

        // Immediately move the clock to the current cursor position
        if (_clockText != null && Win32Interop.GetCursorPos(out var pt))
        {
            double x = pt.X, y = pt.Y;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformFromDevice;
                var logical = m.Transform(new Point(pt.X, pt.Y));
                x = logical.X;
                y = logical.Y;
            }
            _clockConfig.PositionX = (int)x;
            _clockConfig.PositionY = (int)y;
            Canvas.SetLeft(_clockText, _clockConfig.PositionX);
            Canvas.SetTop(_clockText, _clockConfig.PositionY);
        }

        _dragTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _dragTimer.Tick += DragTimer_Tick;
        _dragTimer.Start();
    }

    private void DragTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isClockDragging || _clockText == null) return;

        // Check for left mouse button click to confirm
        bool leftDown = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_LBUTTON) & 0x8000) != 0;
        if (!leftDown && _wasLeftButtonDown)
        {
            // Left button was released → this is a click → confirm position
            _wasLeftButtonDown = false;
            // Slight delay to avoid immediate re-trigger
            return;
        }
        if (leftDown && !_wasLeftButtonDown)
        {
            // New left click → confirm
            DisableClockDrag();
            return;
        }
        _wasLeftButtonDown = leftDown;

        // Check Escape to cancel
        if ((Win32Interop.GetAsyncKeyState(Win32Interop.VK_ESCAPE) & 0x8000) != 0)
        {
            DisableClockDrag();
            return;
        }

        // Move clock to follow cursor
        if (Win32Interop.GetCursorPos(out var pt))
        {
            double x = pt.X, y = pt.Y;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformFromDevice;
                var logical = m.Transform(new Point(pt.X, pt.Y));
                x = logical.X;
                y = logical.Y;
            }
            _clockConfig.PositionX = (int)(x - _clockDragOffset.X);
            _clockConfig.PositionY = (int)(y - _clockDragOffset.Y);
            Canvas.SetLeft(_clockText, _clockConfig.PositionX);
            Canvas.SetTop(_clockText, _clockConfig.PositionY);
        }
    }

    /// <summary>Disable clock dragging and stop the tracking timer.</summary>
    public void DisableClockDrag()
    {
        _isClockDragging = false;
        _dragTimer?.Stop();
        _dragTimer = null;

        // Notify the MainWindow to restore the ClockPage button state
        App.MainWin?.NotifyClockDragConfirmed();
    }

    /// <summary>Called when the screen resolution may have changed.</summary>
    public void OnScreenResolutionChanged()
    {
        UpdateScreenBounds();
        Render();
    }
}
