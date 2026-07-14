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
    private Point _clockDragOffset;

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
        int w = Win32Interop.GetScreenWidth();
        int h = Win32Interop.GetScreenHeight();
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

        double sw = this.Width > 0 ? this.Width : Win32Interop.GetScreenWidth();
        double sh = this.Height > 0 ? this.Height : Win32Interop.GetScreenHeight();

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
                _clockConfig.GetColor().B))
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
            // am/pm h:mm — hour displayed as 0-11 (00-11) instead of 1-12
            ClockFormat.hmmAmPm => $"{now.ToString("tt")} {now.Hour % 12:00}:{now:mm}",
            _ => now.ToString("HH:mm")
        };
        _clockText.Text = text;
    }

    /// <summary>Enable mouse dragging of the clock element.</summary>
    public void EnableClockDrag()
    {
        _isClockDragging = true;
        // When dragging is enabled, we temporarily disable click-through
        // so the overlay can receive mouse events
        var helper = new WindowInteropHelper(this);
        int style = Win32Interop.GetWindowLong(helper.Handle, Win32Interop.GWL_EXSTYLE);
        style &= ~Win32Interop.WS_EX_TRANSPARENT; // Remove click-through
        Win32Interop.SetWindowLong(helper.Handle, Win32Interop.GWL_EXSTYLE, style);
    }

    /// <summary>Disable mouse dragging and restore click-through.</summary>
    public void DisableClockDrag()
    {
        _isClockDragging = false;
        // Restore click-through
        var helper = new WindowInteropHelper(this);
        Win32Interop.MakeOverlayWindow(helper.Handle);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Add hook for mouse events when dragging
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Handle WM_LBUTTONDOWN (0x201), WM_MOUSEMOVE (0x200), WM_LBUTTONUP (0x202)
        // for clock dragging
        if (_isClockDragging && _clockText != null)
        {
            const int WM_LBUTTONDOWN = 0x201;
            const int WM_MOUSEMOVE = 0x200;
            const int WM_LBUTTONUP = 0x202;

            long lVal = lParam.ToInt64();
            int x = (int)(short)(lVal & 0xFFFF);
            int y = (int)(short)((lVal >> 16) & 0xFFFF);

            if (msg == WM_LBUTTONDOWN)
            {
                _clockDragOffset = new Point(x - _clockConfig.PositionX, y - _clockConfig.PositionY);
                handled = true;
            }
            else if (msg == WM_MOUSEMOVE)
            {
                _clockConfig.PositionX = (int)(x - _clockDragOffset.X);
                _clockConfig.PositionY = (int)(y - _clockDragOffset.Y);
                Canvas.SetLeft(_clockText, _clockConfig.PositionX);
                Canvas.SetTop(_clockText, _clockConfig.PositionY);
                handled = true;
            }
            else if (msg == WM_LBUTTONUP)
            {
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>Called when the screen resolution may have changed.</summary>
    public void OnScreenResolutionChanged()
    {
        UpdateScreenBounds();
        Render();
    }
}
