using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private Point _clockDragStartPos;
    private bool _wasLeftButtonDown;

    // When true, Render() skips the full canvas rebuild. Used during clock
    // dragging so that the 33ms drag timer does not trigger a 30Hz full
    // re-render of every overlay element; the drag moves the clock TextBlock
    // directly and the canvas is rendered once when dragging ends.
    private bool _suppressRender;

    // One-time warning when MotionDots rendering cannot be initialized.
    private bool _motionInitFailureReported;

    private HwndSource? _windowSource;
    private bool _isWpfSurfaceCompact;
    private readonly DirectCompositionMotionRenderer _nativeMotionRenderer = new();
    private IntPtr _hwnd;
    private DispatcherTimer? _topmostTimer;
    private bool _isClosed;

    // Last foreground-window rect the overlay was rendered for (Window mode tracking).
    private Win32Interop.RECT? _lastForegroundRect;

    // ── Hotkey OSD (top-center transient hint) ──
    // OsdText lives on a Grid layer above OverlayCanvas because hotkey presses
    // themselves trigger Render() (ConfigStore.Changed), and Render() clears
    // the canvas — an OSD inside the canvas would be wiped the instant it
    // appeared. The fields below manage hold/fade timing and the temporary
    // expansion of the compact 1×1 WPF surface (motion-dots-only mode) that
    // would otherwise clip the OSD to a single pixel.
    private DispatcherTimer? _osdTimer;
    private bool _osdVisible;          // shown or fading — blocks compact collapse in Render()
    private bool _osdExpandedSurface;  // ShowOsd expanded the compact surface; restore on fade-out
    private const int OsdHoldMs = 1000;
    private const int OsdFadeMs = 300;
    private const double OsdTopOffsetDip = 120; // below the thickest top bar (XXL = 100 DIP)

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
        SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;

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

            // Window mode tracking: Render() otherwise only runs on config/display
            // changes, so the overlay would stay locked to whichever window was
            // foreground when it last rendered. Poll the foreground rect at this
            // low frequency and re-render only when it actually switched, moved,
            // or was resized — one Render() refreshes both the motion zones and
            // the edge shapes. Zero cost outside Window mode.
            if (_overlayConfig.Mode == DisplayMode.Window && _overlayConfig.IsVisible)
            {
                var fw = Win32Interop.GetForegroundWindowRect();
                if (!ForegroundRectEquals(fw, _lastForegroundRect))
                {
                    _lastForegroundRect = fw;
                    Render();
                }
            }
        };
        _topmostTimer.Start();

        Render();
    }

    private void OverlayWindow_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _topmostTimer?.Stop();
        _osdTimer?.Stop();
        SystemEvents.DisplaySettingsChanged -= OnSystemDisplaySettingsChanged;
        _nativeMotionRenderer.Dispose();
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        _clockTimer?.Stop();
        _dragTimer?.Stop();
    }

    /// <summary>Update window bounds to cover the full screen.</summary>
    public void UpdateScreenBounds()
    {
        double scale = Win32Interop.GetDpiScaleForWindow(_hwnd);
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
        if (_suppressRender) return;

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

            // Surface a one-time warning when MotionDots were requested but the
            // native renderer could not initialize (e.g. remote desktop, virtual
            // machines, missing/outdated GPU drivers) — otherwise the feature
            // fails silently and the user has no idea why nothing appears.
            if (wantsNativeMotion && !_motionInitFailureReported)
            {
                _motionInitFailureReported = true;
                string title = (string)(Application.Current.TryFindResource("Motion_InitFail_Title")
                    ?? "Motion Dots Unavailable");
                string msg = (string)(Application.Current.TryFindResource("Motion_InitFail_Msg")
                    ?? "Motion dots rendering could not be initialized.");
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Shrink WPF window to 1x1 when DirectComposition is the only visible surface.
        // !_osdVisible: a showing OSD must not be collapsed back to 1×1 by the very
        // Render() call the hotkey triggered — fade-out completion restores it.
        SetWpfSurfaceCompact(ShouldCompactSurface(
            overlayVisible: _overlayConfig.IsVisible,
            isMotionDots: _overlayConfig.Shape == OverlayShape.MotionDots,
            nativeMotionActive: nativeMotionActive,
            crosshairVisible: _crosshairConfig.IsVisible,
            clockVisible: _clockConfig.IsVisible,
            osdVisible: _osdVisible));

        double sw = this.Width > 0 ? this.Width : Win32Interop.GetScreenWidth() / Win32Interop.GetDpiScaleForWindow(_hwnd);
        double sh = this.Height > 0 ? this.Height : Win32Interop.GetScreenHeight() / Win32Interop.GetDpiScaleForWindow(_hwnd);

        // Render edge overlay (non-MotionDots shapes) — per monitor
        if (_overlayConfig.IsVisible && _overlayConfig.Shape != OverlayShape.MotionDots)
        {
            int vsX = Win32Interop.GetVirtualScreenX();
            int vsY = Win32Interop.GetVirtualScreenY();
            double windowScale = Win32Interop.GetDpiScaleForWindow(_hwnd);
            var monitors = GetConfiguredMonitors(physicalWidth, physicalHeight, windowScale, vsX, vsY);

            foreach (var mon in monitors)
            {
                // Convert monitor bounds to canvas (window-DPI) coordinate system
                double monLogX = (mon.X - vsX) / windowScale;
                double monLogY = (mon.Y - vsY) / windowScale;
                double monLogW = mon.Width / windowScale;
                double monLogH = mon.Height / windowScale;
                var monBounds = new Rect(monLogX, monLogY, monLogW, monLogH);
                var overlayShapes = RenderHelper.BuildOverlayShapes(_overlayConfig, monBounds, windowScale, mon.DpiScale);
                foreach (var s in overlayShapes)
                    OverlayCanvas.Children.Add(s);
            }
        }

        // Render crosshair — per monitor
        if (_crosshairConfig.IsVisible)
        {
            int vsX = Win32Interop.GetVirtualScreenX();
            int vsY = Win32Interop.GetVirtualScreenY();
            double windowScale = Win32Interop.GetDpiScaleForWindow(_hwnd);
            var monitors = GetConfiguredMonitors(physicalWidth, physicalHeight, windowScale, vsX, vsY);

            foreach (var mon in monitors)
            {
                // Convert monitor bounds to canvas (window-DPI) coordinate system
                double monLogX = (mon.X - vsX) / windowScale;
                double monLogY = (mon.Y - vsY) / windowScale;
                double monLogW = mon.Width / windowScale;
                double monLogH = mon.Height / windowScale;
                var monBounds = new Rect(monLogX, monLogY, monLogW, monLogH);
                var crosshairShapes = RenderHelper.BuildCrosshairShapes(_crosshairConfig, monBounds, windowScale, mon.DpiScale);
                foreach (var s in crosshairShapes)
                    OverlayCanvas.Children.Add(s);
            }
        }

        // Render clock
        if (_clockConfig.IsVisible)
        {
            RenderClock(sw, sh);
        }
    }

    /// <summary>Compute motion zones based on per-monitor edges, Window mode, Split, Length, and Edge visibility.</summary>
    private List<MotionZone> ComputeMotionZones(int physW, int physH)
    {
        var zones = new List<MotionZone>();
        var cfg = _overlayConfig;
        int vsX = Win32Interop.GetVirtualScreenX();
        int vsY = Win32Interop.GetVirtualScreenY();

        var monitors = GetConfiguredMonitors(physW, physH, 0, vsX, vsY);

        Win32Interop.RECT? fwRect = null;
        if (cfg.Mode == DisplayMode.Window)
            fwRect = Win32Interop.GetForegroundWindowRect();

        foreach (var mon in monitors)
        {
            var monZones = ComputeZonesForMonitor(cfg, mon, vsX, vsY, fwRect);
            zones.AddRange(monZones);
        }

        return zones;
    }

    /// <summary>
    /// Compute motion zones for a single monitor. Extracted from
    /// <see cref="ComputeMotionZones"/> for unit testability — this method
    /// contains all the zone geometry logic without any Win32 API calls.
    /// </summary>
    /// <param name="cfg">Overlay configuration</param>
    /// <param name="mon">Monitor bounds and DPI scale</param>
    /// <param name="vsX">Virtual screen origin X (from GetVirtualScreenX)</param>
    /// <param name="vsY">Virtual screen origin Y (from GetVirtualScreenY)</param>
    /// <param name="fwRect">Foreground window rect in physical coords (null if not Window mode or no foreground window)</param>
    /// <returns>List of motion zones for this monitor (empty if foreground window is not on this monitor in Window mode)</returns>
    internal static List<MotionZone> ComputeZonesForMonitor(
        OverlayConfig cfg,
        Win32Interop.MonitorInfo mon,
        int vsX, int vsY,
        Win32Interop.RECT? fwRect)
    {
        var zones = new List<MotionZone>();
        double scale = mon.DpiScale;

        // Translate monitor bounds to virtual-screen-relative coordinates
        float monX = mon.X - vsX;
        float monY = mon.Y - vsY;
        float monW = mon.Width;
        float monH = mon.Height;

        // Apply aspect ratio safe area for this monitor
        var safeLogical = RenderHelper.GetSafeArea(monW / scale, monH / scale, cfg.AspectRatio);
        float drawX = monX + (float)(safeLogical.X * scale);
        float drawY = monY + (float)(safeLogical.Y * scale);
        float drawW = (float)(safeLogical.Width * scale);
        float drawH = (float)(safeLogical.Height * scale);

        // Window mode: follow foreground window, only on the monitor it resides on
        if (cfg.Mode == DisplayMode.Window && fwRect.HasValue)
        {
            var r = fwRect.Value;
            int fwCenterX = (r.Left + r.Right) / 2;
            int fwCenterY = (r.Top + r.Bottom) / 2;
            // Skip monitors that don't contain the foreground window center
            if (fwCenterX < mon.X || fwCenterX >= mon.X + mon.Width ||
                fwCenterY < mon.Y || fwCenterY >= mon.Y + mon.Height)
                return zones; // empty list — foreground window not on this monitor

            drawX = r.Left - vsX;
            drawY = r.Top - vsY;
            drawW = r.Right - r.Left;
            drawH = r.Bottom - r.Top;
        }

        // Zone width based on individual monitor width (not full virtual screen)
        float zoneW = monW * 0.12f;
        float lengthPx = (float)RenderHelper.LengthOffsetPx(cfg.Length) * 8f * (float)scale;

        float leftOpacity = cfg.OpacityMode == EdgeOpacityMode.Uniform
            ? cfg.Opacity / 100f : cfg.EdgeLeftOpacity / 100f;
        float rightOpacity = cfg.OpacityMode == EdgeOpacityMode.Uniform
            ? cfg.Opacity / 100f : cfg.EdgeRightOpacity / 100f;

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

        foreach (var a in areas)
        {
            // Parallax center follows each split area's own center,
            // not the monitor center, so split-boundary zones aren't always at min scale
            float areaLeftX = a.x;
            float areaRightX = a.x + a.w;
            if (cfg.EdgeLeftVisible)
                zones.Add(new MotionZone(a.x + lengthPx, a.y, zoneW, a.h, true, leftOpacity, areaLeftX, areaRightX));
            if (cfg.EdgeRightVisible)
                zones.Add(new MotionZone(a.x + a.w - lengthPx - zoneW, a.y, zoneW, a.h, false, rightOpacity, areaLeftX, areaRightX));
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

        double scale = Win32Interop.GetDpiScaleForWindow(_hwnd);
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
        // Raw Input (WM_INPUT) is handled by the DirectCompositionMotionRenderer's
        // internal RawInputNativeWindow, not here. This hook is reserved for
        // future Win32 message handling.
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
    /// The clock teleports to the current mouse cursor position and then
    /// follows it; left-click confirms the position, Esc cancels and restores
    /// the position from before dragging started.
    /// The overlay stays fully click-through — no UI is blocked.
    /// </summary>
    public void EnableClockDrag()
    {
        _isClockDragging = true;
        _wasLeftButtonDown = true;

        // Remember the pre-drag position so Esc can restore it
        _clockDragStartPos = new Point(_clockConfig.PositionX, _clockConfig.PositionY);

        // Suppress the Changed-driven full re-render while dragging; the clock
        // TextBlock is moved directly and the canvas is rebuilt once on confirm.
        _suppressRender = true;

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

        _dragTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
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
            // Esc: cancel the drag and restore the pre-drag position
            _clockConfig.PositionX = (int)_clockDragStartPos.X;
            _clockConfig.PositionY = (int)_clockDragStartPos.Y;
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

    /// <summary>Disable clock dragging, stop the tracking timer, and render the final state once.</summary>
    public void DisableClockDrag()
    {
        _isClockDragging = false;
        _dragTimer?.Stop();
        _dragTimer = null;

        // One final full render to sync the canvas with the config state
        _suppressRender = false;
        Render();

        App.MainWin?.NotifyClockDragConfirmed();
    }

    /// <summary>
    /// Called when the screen resolution or monitor topology may have changed.
    /// DisplaySettingsChanged also fires when games enter/exit fullscreen or
    /// switch refresh rate — such transitions often leave the layout intact,
    /// so the user's placed clock/crosshair must survive them. Positions are
    /// therefore only clamped when they would now be stranded off-screen;
    /// they are no longer reset to defaults unconditionally.
    /// </summary>
    public void OnScreenResolutionChanged()
    {
        if (_isClosed) return;
        UpdateScreenBounds();
        ClampClockIntoView();
        ClampCrosshairIntoView();
        Render();
    }

    /// <summary>
    /// Keep the clock's top-left corner inside the canvas. Size is measured on
    /// the last rendered clock element (identical config → identical size), so
    /// this runs before Render() and lets it draw the corrected position in
    /// one pass. Skipped when no clock was rendered yet. No-op while in bounds.
    /// </summary>
    private void ClampClockIntoView()
    {
        if (_clockText == null) return;

        _clockText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _clockText.DesiredSize;
        int x = ClampIntoRange(_clockConfig.PositionX, OverlayCanvas.Width - size.Width);
        int y = ClampIntoRange(_clockConfig.PositionY, OverlayCanvas.Height - size.Height);
        if (x == _clockConfig.PositionX && y == _clockConfig.PositionY) return;

        _clockConfig.PositionX = x;
        _clockConfig.PositionY = y;
    }

    /// <summary>
    /// Same safety net for the crosshair: PositionX/Y are offsets from each
    /// target monitor's safe-area center, so clamping them to half the canvas
    /// guarantees the center point lands on some connected screen. No-op when
    /// in range.
    /// </summary>
    private void ClampCrosshairIntoView()
    {
        int x = ClampOffset(_crosshairConfig.PositionX, OverlayCanvas.Width / 2.0);
        int y = ClampOffset(_crosshairConfig.PositionY, OverlayCanvas.Height / 2.0);
        if (x == _crosshairConfig.PositionX && y == _crosshairConfig.PositionY) return;

        _crosshairConfig.PositionX = x;
        _crosshairConfig.PositionY = y;
    }

    /// <summary>Clamp a top-left coordinate into [0, max]; degenerate max → 0.</summary>
    internal static int ClampIntoRange(int value, double max)
        => Math.Clamp(value, 0, (int)Math.Floor(Math.Max(0, max)));

    /// <summary>Clamp a center-offset into ±maxAbs; degenerate maxAbs → 0.</summary>
    internal static int ClampOffset(int value, double maxAbs)
    {
        int bound = (int)Math.Floor(Math.Max(0, maxAbs));
        return Math.Clamp(value, -bound, bound);
    }

    private void OnSystemDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => OnScreenResolutionChanged());
    }

    // ── Hotkey OSD ──────────────────────────────────────────────────────

    /// <summary>
    /// Show a transient OSD hint at the top of the target monitor. Holds for
    /// ~1s by default (<paramref name="holdMs"/>), then fades out over ~300ms;
    /// repeated calls restart the hold timer and replace the text. Safe to call
    /// from any UI-thread hotkey handler.
    /// </summary>
    public void ShowOsd(string text, int holdMs = OsdHoldMs)
    {
        if (_isClosed || string.IsNullOrWhiteSpace(text)) return;

        // The compact 1×1 surface (motion-dots-only mode) would clip the OSD
        // to a single pixel — expand it for the duration; fade-out completion
        // restores the original compact state via ComputeCompactState().
        if (_isWpfSurfaceCompact)
        {
            SetWpfSurfaceCompact(false);
            _osdExpandedSurface = true;
        }

        _osdVisible = true;
        OsdText.Text = text;
        PositionOsd();
        OsdText.BeginAnimation(OpacityProperty, null); // cancel a running fade
        OsdText.Opacity = 1;
        OsdText.Visibility = Visibility.Visible;

        if (_osdTimer == null)
        {
            _osdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(OsdHoldMs) };
            _osdTimer.Tick += (_, _) => FadeOutOsd();
        }
        _osdTimer.Interval = TimeSpan.FromMilliseconds(holdMs);
        _osdTimer.Stop();
        _osdTimer.Start();
    }

    /// <summary>
    /// Place the OSD at the top-center of the target monitor (primary monitor
    /// when the target is "all monitors" or no specific match). Coordinates
    /// follow Render()'s convention: window-DIP relative to the virtual-screen
    /// origin, so the TextBlock stays horizontally centered regardless of its
    /// measured width via HorizontalAlignment=Center + a TranslateTransform.
    /// </summary>
    private void PositionOsd()
    {
        int ax, ay, aw, ah;
        var monitors = Win32Interop.GetTargetMonitors(App.AppConfig.TargetMonitor);
        if (monitors.Count == 1)
        {
            (ax, ay, aw, ah) = (monitors[0].X, monitors[0].Y, monitors[0].Width, monitors[0].Height);
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
            if (primary.HasValue)
                (ax, ay, aw, ah) = (primary.Value.X, primary.Value.Y, primary.Value.Width, primary.Value.Height);
            else
                (ax, ay, aw, ah) = (Win32Interop.GetVirtualScreenX(), Win32Interop.GetVirtualScreenY(),
                    Win32Interop.GetScreenWidth(), Win32Interop.GetScreenHeight());
        }

        double windowScale = Win32Interop.GetDpiScaleForWindow(_hwnd);
        if (windowScale <= 0) windowScale = 1;
        int vsX = Win32Interop.GetVirtualScreenX();
        int vsY = Win32Interop.GetVirtualScreenY();

        // Window center from metrics, not ActualWidth — a just-expanded compact
        // surface has not been re-measured yet, so ActualWidth is still 1.
        double windowCenterX = Win32Interop.GetScreenWidth() / (2.0 * windowScale);
        double centerX = (ax + aw / 2.0 - vsX) / windowScale;
        double topY = (ay - vsY) / windowScale + OsdTopOffsetDip;

        OsdText.RenderTransform = new TranslateTransform(centerX - windowCenterX, topY);
    }

    private void FadeOutOsd()
    {
        _osdTimer?.Stop();
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(OsdFadeMs));
        fade.Completed += (_, _) =>
        {
            OsdText.Visibility = Visibility.Collapsed;
            _osdVisible = false;
            if (_osdExpandedSurface)
            {
                _osdExpandedSurface = false;
                SetWpfSurfaceCompact(ComputeCompactState());
            }
        };
        OsdText.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>
    /// Compact-surface condition mirroring Render()'s SetWpfSurfaceCompact
    /// call — used to restore the 1×1 surface after an OSD-triggered expansion.
    /// </summary>
    private bool ComputeCompactState() =>
        ShouldCompactSurface(
            overlayVisible: _overlayConfig.IsVisible,
            isMotionDots: _overlayConfig.Shape == OverlayShape.MotionDots,
            nativeMotionActive: _nativeMotionRenderer.IsReady,
            crosshairVisible: _crosshairConfig.IsVisible,
            clockVisible: _clockConfig.IsVisible,
            osdVisible: false); // called at fade-out completion — the OSD is gone

    /// <summary>
    /// Pure decision for shrinking the WPF surface to 1×1: the only visible
    /// content is native motion dots (renderer ready) and neither the
    /// crosshair, the clock, nor an OSD needs the WPF layer. Extracted from
    /// Render()/ComputeCompactState() for unit testing.
    /// </summary>
    internal static bool ShouldCompactSurface(
        bool overlayVisible, bool isMotionDots, bool nativeMotionActive,
        bool crosshairVisible, bool clockVisible, bool osdVisible)
        => overlayVisible && isMotionDots && nativeMotionActive
            && !crosshairVisible && !clockVisible && !osdVisible;

    /// <summary>
    /// Field-wise equality for the nullable foreground rect — RECT has no
    /// equality members, so lifted == / != on RECT? is not available.
    /// </summary>
    internal static bool ForegroundRectEquals(Win32Interop.RECT? a, Win32Interop.RECT? b)
    {
        if (!a.HasValue || !b.HasValue)
            return a.HasValue == b.HasValue;
        return a.Value.Left == b.Value.Left && a.Value.Top == b.Value.Top
            && a.Value.Right == b.Value.Right && a.Value.Bottom == b.Value.Bottom;
    }

    /// <summary>
    /// Get the list of monitors to render on, filtered by TargetMonitor config.
    /// Falls back to the full virtual screen if no monitors are detected.
    /// </summary>
    private List<Win32Interop.MonitorInfo> GetConfiguredMonitors(
        int physW, int physH, double windowScale, int vsX, int vsY)
    {
        var monitors = Win32Interop.GetTargetMonitors(App.AppConfig.TargetMonitor);
        if (monitors.Count == 0)
        {
            double scale = windowScale > 0 ? windowScale : Win32Interop.GetDpiScale();
            monitors.Add(new Win32Interop.MonitorInfo(vsX, vsY, physW, physH, scale));
        }
        return monitors;
    }
}
