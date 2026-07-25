using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
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

    private HwndSource? _windowSource;
    private bool _isWpfSurfaceCompact;
    private readonly DirectCompositionMotionRenderer _nativeMotionRenderer = new();
    private IntPtr _hwnd;
    private DispatcherTimer? _topmostTimer;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OverlayWindow_Loaded;
        Closed += OverlayWindow_Closed;
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply Win32 extended styles for click-through + no-activate
        var helper = new WindowInteropHelper(this);
        _hwnd = helper.Handle;
        Win32Interop.MakeOverlayWindow(_hwnd);
        _windowSource = HwndSource.FromHwnd(_hwnd);
        _windowSource?.AddHook(WindowMessageHook);

        // Size to full screen
        UpdateScreenBounds();

        // Start clock timer
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();

        // Periodically re-assert HWND_TOPMOST — fullscreen games (e.g. Cyberpunk 2077)
        // continuously push their own window to the top, causing the WPF layered
        // overlay to sink below the game surface. The DirectComposition native
        // window re-asserts on every visibility/size change, but the WPF window
        // only sets Topmost once at creation time. This timer closes that gap.
        _topmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _topmostTimer.Tick += (_, _) =>
        {
            if (!_isWpfSurfaceCompact)
                Win32Interop.ReassertTopmost(_hwnd);
        };
        _topmostTimer.Start();

        Render();
    }

    private void OverlayWindow_Closed(object? sender, EventArgs e)
    {
        _topmostTimer?.Stop();
        _nativeMotionRenderer.Dispose();
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        _clockTimer?.Stop();
        _dragTimer?.Stop();
    }

    /// <summary>Update window bounds to cover the full screen.</summary>
    public void UpdateScreenBounds()
    {
        double scale = Win32Interop.GetDpiScale();
        int physX = Win32Interop.GetVirtualScreenX();
        int physY = Win32Interop.GetVirtualScreenY();
        int physW = Win32Interop.GetScreenWidth();
        int physH = Win32Interop.GetScreenHeight();
        double w = _isWpfSurfaceCompact ? 1 : physW / scale;
        double h = _isWpfSurfaceCompact ? 1 : physH / scale;
        this.Left = _isWpfSurfaceCompact ? 0 : physX / scale;
        this.Top = _isWpfSurfaceCompact ? 0 : physY / scale;
        this.Width = w;
        this.Height = h;
        OverlayCanvas.Width = w;
        OverlayCanvas.Height = h;

        if (_nativeMotionRenderer.IsReady)
        {
            var zones = ComputeMotionZones(physW, physH);
            _nativeMotionRenderer.Configure(
                _overlayConfig,
                physW, physH,
                zones);
        }
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

        int physicalWidth = Win32Interop.GetScreenWidth();
        int physicalHeight = Win32Interop.GetScreenHeight();
        bool wantsNativeMotion =
            _overlayConfig.IsVisible &&
            _overlayConfig.Shape == OverlayShape.MotionDots;
        var motionZones = wantsNativeMotion ? ComputeMotionZones(physicalWidth, physicalHeight) : new List<MotionZone>();
        bool nativeMotionActive =
            wantsNativeMotion &&
            motionZones.Count > 0 &&
            _nativeMotionRenderer.TryInitialize(physicalWidth, physicalHeight);

        if (nativeMotionActive)
        {
            _nativeMotionRenderer.Configure(
                _overlayConfig,
                physicalWidth,
                physicalHeight,
                motionZones);
        }
        else
        {
            _nativeMotionRenderer.SetVisible(false);
        }

        // Shrink WPF window to 1x1 when DirectComposition is the only visible surface
        SetWpfSurfaceCompact(
            nativeMotionActive &&
            !_crosshairConfig.IsVisible &&
            !_clockConfig.IsVisible);

        double sw = this.Width > 0 ? this.Width : Win32Interop.GetScreenWidth() / Win32Interop.GetDpiScale();
        double sh = this.Height > 0 ? this.Height : Win32Interop.GetScreenHeight() / Win32Interop.GetDpiScale();

        // Render edge overlay (non-MotionDots shapes)
        if (_overlayConfig.IsVisible && _overlayConfig.Shape != OverlayShape.MotionDots)
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

    /// <summary>Compute motion zones based on Window mode, Split, Length, and Edge visibility.</summary>
    private List<MotionZone> ComputeMotionZones(int physW, int physH)
    {
        var zones = new List<MotionZone>();
        var cfg = _overlayConfig;
        double scale = Win32Interop.GetDpiScale();

        // Get draw area in physical pixels
        float drawX = 0, drawY = 0, drawW = physW, drawH = physH;

        // Apply aspect ratio safe area
        var safeLogical = RenderHelper.GetSafeArea(physW / scale, physH / scale, cfg.AspectRatio);
        drawX = (float)(safeLogical.X * scale);
        drawY = (float)(safeLogical.Y * scale);
        drawW = (float)(safeLogical.Width * scale);
        drawH = (float)(safeLogical.Height * scale);

        // Window mode: follow foreground window
        if (cfg.Mode == DisplayMode.Window)
        {
            var fwRect = Win32Interop.GetForegroundWindowRect();
            if (fwRect.HasValue)
            {
                var r = fwRect.Value;
                // Translate from absolute screen coords to window-relative coords
                int vsX = Win32Interop.GetVirtualScreenX();
                int vsY = Win32Interop.GetVirtualScreenY();
                drawX = r.Left - vsX;
                drawY = r.Top - vsY;
                drawW = r.Right - r.Left;
                drawH = r.Bottom - r.Top;
            }
        }

        // Build list of draw areas (split if needed)
        var areas = new List<(float x, float y, float w, float h)> { (drawX, drawY, drawW, drawH) };
        if (cfg.Split == SplitScreen.Vertical)
        {
            var orig = areas; areas = new();
            foreach (var a in orig)
            {
                areas.Add((a.x, a.y, a.w / 2, a.h));
                areas.Add((a.x + a.w / 2, a.y, a.w / 2, a.h));
            }
        }
        else if (cfg.Split == SplitScreen.Horizontal)
        {
            var orig = areas; areas = new();
            foreach (var a in orig)
            {
                areas.Add((a.x, a.y, a.w, a.h / 2));
                areas.Add((a.x, a.y + a.h / 2, a.w, a.h / 2));
            }
        }

        float zoneW = physW * 0.12f;
        float lengthPx = (float)RenderHelper.LengthOffsetPx(cfg.Length) * 8f * (float)scale;

        float leftOpacity = cfg.OpacityMode == EdgeOpacityMode.Uniform
            ? cfg.Opacity / 100f : cfg.EdgeLeftOpacity / 100f;
        float rightOpacity = cfg.OpacityMode == EdgeOpacityMode.Uniform
            ? cfg.Opacity / 100f : cfg.EdgeRightOpacity / 100f;

        foreach (var a in areas)
        {
            if (cfg.EdgeLeftVisible)
                zones.Add(new MotionZone(a.x + lengthPx, a.y, zoneW, a.h, true, leftOpacity));
            if (cfg.EdgeRightVisible)
                zones.Add(new MotionZone(a.x + a.w - lengthPx - zoneW, a.y, zoneW, a.h, false, rightOpacity));
        }

        return zones;
    }

    private void SetWpfSurfaceCompact(bool compact)
    {
        if (_isWpfSurfaceCompact == compact)
            return;

        _isWpfSurfaceCompact = compact;
        if (compact)
        {
            Width = 1;
            Height = 1;
            OverlayCanvas.Width = 1;
            OverlayCanvas.Height = 1;
            return;
        }

        double scale = Win32Interop.GetDpiScale();
        double width = Win32Interop.GetScreenWidth() / scale;
        double height = Win32Interop.GetScreenHeight() / scale;
        Left = Win32Interop.GetVirtualScreenX() / scale;
        Top = Win32Interop.GetVirtualScreenY() / scale;
        Width = width;
        Height = height;
        OverlayCanvas.Width = width;
        OverlayCanvas.Height = height;

        // Re-assert topmost when expanding back to full screen,
        // since the DirectComposition native window may have been above us.
        if (_hwnd != IntPtr.Zero)
            Win32Interop.ReassertTopmost(_hwnd);
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == Win32Interop.WM_INPUT &&
            _overlayConfig.IsVisible &&
            _overlayConfig.Shape == OverlayShape.MotionDots &&
            Win32Interop.TryGetRawMouseDelta(lParam, out int deltaX, out int deltaY))
        {
            _nativeMotionRenderer.OnMouseDelta(deltaX, deltaY);
        }

        return IntPtr.Zero;
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
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Padding = new Thickness(8)
        };

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
        _wasLeftButtonDown = true;

        _clockDragOffset = new Point(0, 0);

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

        bool leftDown = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_LBUTTON) & 0x8000) != 0;
        if (!leftDown && _wasLeftButtonDown)
        {
            _wasLeftButtonDown = false;
            return;
        }
        if (leftDown && !_wasLeftButtonDown)
        {
            DisableClockDrag();
            return;
        }
        _wasLeftButtonDown = leftDown;

        if ((Win32Interop.GetAsyncKeyState(Win32Interop.VK_ESCAPE) & 0x8000) != 0)
        {
            DisableClockDrag();
            return;
        }

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

        App.MainWin?.NotifyClockDragConfirmed();
    }

    /// <summary>Called when the screen resolution may have changed.</summary>
    public void OnScreenResolutionChanged()
    {
        UpdateScreenBounds();
        Render();
    }
}
