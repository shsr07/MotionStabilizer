using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using MotionStabilizer.Models;
using MotionStabilizer.Services;
using SharpGen.Runtime;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;
using Vortice.Mathematics;
using D2DAlphaMode = Vortice.DCommon.AlphaMode;
using DXGIAlphaMode = Vortice.DXGI.AlphaMode;
using D2DPixelFormat = Vortice.DCommon.PixelFormat;
using D3DFeatureLevel = Vortice.Direct3D.FeatureLevel;
using static Vortice.Direct2D1.D2D1;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DirectComposition.DComp;

namespace MotionStabilizer.Overlay;

/// <summary>
/// A motion zone rectangle defining where dots should appear.
/// Passed from OverlayWindow based on Window mode, Split, Length, and Edge visibility.
/// </summary>
public readonly struct MotionZone
{
    public readonly float X, Y, Width, Height;
    public readonly bool IsLeftSide;
    public readonly float Opacity;
    public readonly float AreaLeftX, AreaRightX;

    public MotionZone(float x, float y, float w, float h, bool isLeft, float opacity,
        float areaLeftX, float areaRightX)
    {
        X = x; Y = y; Width = w; Height = h;
        IsLeftSide = isLeft; Opacity = opacity;
        AreaLeftX = areaLeftX; AreaRightX = areaRightX;
    }
}

/// <summary>
/// Draws motion cue dots in screen-side zones through Direct2D/DirectComposition.
/// Dots appear in zones defined by MotionZone rectangles, and move uniformly
/// based on mouse X/Y delta (inverted), (optionally) WASD key state, and
/// (optionally) gamepad sticks (left = WASD role, right = mouse role).
/// Includes continuous pulsing (breathing) and horizontal alpha-mask fade.
/// </summary>
internal sealed class DirectCompositionMotionRenderer : IDisposable
{
    // ── Visual constants ──
    private const float ZoneWidthRatio = 0.12f;
    private const float FadeMarginX = 0.04f;  // 4% of zone width — narrow wrap gap
    private const float PulseAmplitude = 0.10f;
    private const float PulsePeriod = 2.0f;
    private const float ParallaxEdgeDeadZone = 80f;
    private const float KeyboardBaseSpeed = 210.0f;
    private const float MouseSpeedScale = 1.575f;

    // ── Win32 constants ──
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExNoRedirectionBitmap = 0x00200000;
    private const int GwlExStyle = -20;
    private const int WmMouseActivate = 0x0021;
    private const int WmNcHitTest = 0x0084;
    private const int MaNoActivate = 3;
    private const int HtTransparent = -1;
    private const uint LwaAlpha = 0x00000002;
    private const int ProcessPowerThrottling = 4;
    private const uint PowerThrottlingCurrentVersion = 1;
    private const uint PowerThrottlingExecutionSpeed = 0x1;
    private const uint PowerThrottlingIgnoreTimerResolution = 0x4;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    // ── Fields ──
    private readonly System.Threading.Timer _timer;
    private readonly Dispatcher _dispatcher;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Random _random = new(0x51A8);
    private readonly List<ZoneDot> _dots = new();
    private List<MotionZone> _zones = new();

    private MotionOverlayNativeWindow? _window;
    private RawInputNativeWindow? _rawInputWindow;
    private ID3D11Device? _d3dDevice;
    private ID3D11DeviceContext? _d3dContext;
    private IDXGISwapChain1? _swapChain;
    private ID2D1Factory1? _d2dFactory;
    private ID2D1Device? _d2dDevice;
    private ID2D1DeviceContext? _d2dContext;
    private ID2D1Bitmap1? _targetBitmap;
    private ID2D1SolidColorBrush? _brush;
    private IDCompositionDevice? _compositionDevice;
    private IDCompositionTarget? _compositionTarget;
    private IDCompositionVisual? _compositionVisual;

    private OverlayConfig _config = new();
    private int _screenX;
    private int _screenY;
    private int _width;
    private int _height;
    private bool _visible;
    private bool _disposed;
    private int _timerEnabled;
    private int _tickQueued;
    private int _timerResolutionActive;
    private bool _powerThrottlingOverridden;
    private IntPtr _mmcssHandle;

    private Vector2 _velocity;
    private float _pendingMouseDeltaX;
    private float _pendingMouseDeltaY;
    private TimeSpan _lastFrame;
    private TimeSpan _lastMouseInput;
    private float _pulseTime; // Only advances when motion is active
    private TimeSpan _lastPixelFrame;
    private double _pixelIntervalMs = 1000.0 / 120.0;
    private int _configuredTimerPeriodMs = 8;
    private int _currentTimerPeriodMs = 8;

    private int _layoutDotColumns = -1;
    private double _layoutDotSpacingV = -1;
    private double _layoutDotSpacingH = -1;
    private SizePreset _layoutSize = (SizePreset)(-1);
    private int _layoutZoneHash;

    private sealed class ZoneDot
    {
        public Vector2 Position;
        public float BaseRadius;
        public float PulsePhase;
        public int ZoneIndex;
        public float SpawnOpacity;
        public float MaxOpacity;
    }

    // ── Native windows ──

    private sealed class MotionOverlayNativeWindow : System.Windows.Forms.NativeWindow, IDisposable
    {
        public MotionOverlayNativeWindow(int x, int y, int width, int height)
        {
            var parameters = new System.Windows.Forms.CreateParams
            {
                Caption = "MotionStabilizer.DirectComposition",
                X = x, Y = y, Width = width, Height = height,
                Style = WsPopup,
                ExStyle = WsExTransparent | WsExLayered | WsExToolWindow |
                          WsExNoActivate | WsExNoRedirectionBitmap
            };
            CreateHandle(parameters);
        }

        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            if (message.Msg == WmNcHitTest) { message.Result = new IntPtr(HtTransparent); return; }
            if (message.Msg == WmMouseActivate) { message.Result = new IntPtr(MaNoActivate); return; }
            base.WndProc(ref message);
        }

        public void Dispose() { if (Handle != IntPtr.Zero) DestroyHandle(); }
    }

    private sealed class RawInputNativeWindow : System.Windows.Forms.NativeWindow, IDisposable
    {
        private static readonly IntPtr HwndMessage = new(-3);
        private readonly Action<int, int> _mouseDelta;

        public RawInputNativeWindow(Action<int, int> mouseDelta)
        {
            _mouseDelta = mouseDelta;
            CreateHandle(new System.Windows.Forms.CreateParams
            { Caption = "MotionStabilizer.RawInput", Parent = HwndMessage });
        }

        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            if (message.Msg == Win32Interop.WM_INPUT &&
                Win32Interop.TryGetRawMouseDelta(message.LParam, out int dx, out int dy))
                _mouseDelta(dx, dy);
            base.WndProc(ref message);
        }

        public void Dispose() { if (Handle != IntPtr.Zero) DestroyHandle(); }
    }

    // ── Public API ──

    public bool IsReady { get; private set; }
    public bool IsVisible => _visible && IsReady;

    public DirectCompositionMotionRenderer()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new System.Threading.Timer(QueueRenderTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool TryInitialize(int width, int height)
    {
        if (IsReady) return true;
        try
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
            _screenX = Win32Interop.GetVirtualScreenX();
            _screenY = Win32Interop.GetVirtualScreenY();
            CreateNativeWindow();
            CreateGraphicsResources();
            _rawInputWindow = new RawInputNativeWindow(OnMouseDelta);
            if (!Win32Interop.RegisterRawMouseInput(_rawInputWindow.Handle))
                throw new InvalidOperationException("Unable to register native raw mouse input.");
            IsReady = true;
            DrawAndPresent();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DirectComposition initialization failed: {ex}");
            DisposeGraphicsResources();
            Win32Interop.UnregisterRawMouseInput();
            _rawInputWindow?.Dispose(); _rawInputWindow = null;
            _window?.Dispose(); _window = null;
            IsReady = false;
            return false;
        }
    }

    public void Configure(OverlayConfig config, int width, int height, List<MotionZone> zones)
    {
        if (!IsReady) return;

        bool sizeChanged = width != _width || height != _height;
        int zoneHash = ComputeZoneHash(zones);
        bool layoutChanged = sizeChanged ||
            config.MotionDotColumns != _layoutDotColumns ||
            config.MotionDotSpacingV != _layoutDotSpacingV ||
            config.MotionDotSpacingH != _layoutDotSpacingH ||
            config.Size != _layoutSize ||
            zoneHash != _layoutZoneHash;

        _config = config;
        _zones = zones;
        int refreshRate = Math.Clamp(config.MotionRefreshRate, 30, 360);
        _pixelIntervalMs = 1000.0 / refreshRate;
        _configuredTimerPeriodMs = Math.Max(1, (int)Math.Round(_pixelIntervalMs));
        _currentTimerPeriodMs = _configuredTimerPeriodMs;

        if (Volatile.Read(ref _timerEnabled) != 0)
            _timer.Change(_currentTimerPeriodMs, _currentTimerPeriodMs);

        if (sizeChanged)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
            Resize(width, height);
        }

        if (layoutChanged || _dots.Count == 0)
        {
            _layoutZoneHash = zoneHash;
            CreateDots();
        }

        SetVisible(config.IsVisible && config.Shape == OverlayShape.MotionDots && zones.Count > 0);
        EnsureTimer();
    }

    public void SetVisible(bool visible)
    {
        if (!IsReady || _window == null || _visible == visible) return;
        _visible = visible;
        if (visible)
        {
            DisableBackgroundThrottling();
            EnableMultimediaScheduling();
        }
        else
        {
            DisableMultimediaScheduling();
            RestoreSystemPowerThrottling();
        }
        EnsureClickThroughStyles(_window.Handle);
        ShowWindow(_window.Handle, visible ? SwShowNoActivate : SwHide);
        if (visible)
        {
            SetWindowPos(_window.Handle, HwndTopmost, _screenX, _screenY, _width, _height,
                SwpNoActivate | SwpShowWindow);
            EnsureTimer();
        }
        else
        {
            StopTimer();
            _velocity = Vector2.Zero;
            _pendingMouseDeltaX = 0;
            _pendingMouseDeltaY = 0;
        }
    }

    public void OnMouseDelta(int deltaX, int deltaY)
    {
        if (!IsVisible) return;
        float sensitivity = (float)Math.Clamp(_config.MotionSensitivity, 0.05, 3.0);
        // Mouse right → dots move right, mouse up → dots move up
        // When inverted, directions are reversed
        float sign = _config.MotionInverted ? -1f : 1f;
        _pendingMouseDeltaX += sign * deltaX * sensitivity * MouseSpeedScale;
        _pendingMouseDeltaY += sign * deltaY * sensitivity * MouseSpeedScale;
        _lastMouseInput = _clock.Elapsed;
        EnsureTimer();
    }

    // ── Zone helpers ──

    private static int ComputeZoneHash(List<MotionZone> zones)
    {
        int hash = 17;
        foreach (var z in zones)
        {
            hash = hash * 31 + z.X.GetHashCode();
            hash = hash * 31 + z.Y.GetHashCode();
            hash = hash * 31 + z.Width.GetHashCode();
            hash = hash * 31 + z.Height.GetHashCode();
            hash = hash * 31 + z.IsLeftSide.GetHashCode();
            hash = hash * 31 + z.Opacity.GetHashCode();
        }
        return hash;
    }

    private float ZoneFadeMargin(MotionZone zone) =>
        zone.Width * FadeMarginX;

    private float ComputeEdgeOpacity(Vector2 pos, MotionZone zone, float radius)
    {
        float opacity = 1f;
        // X fade: moderate gap, fade over 2*radius
        float fadeDistX = 2f * radius;
        opacity = Math.Min(opacity, Math.Clamp((pos.X - (zone.X - radius)) / fadeDistX, 0f, 1f));
        opacity = Math.Min(opacity, Math.Clamp(((zone.X + zone.Width + radius) - pos.X) / fadeDistX, 0f, 1f));
        // Y fade: smooth transition over 4*radius
        float fadeDistY = 4f * radius;
        opacity = Math.Min(opacity, Math.Clamp((pos.Y - (zone.Y - 2f * radius)) / fadeDistY, 0f, 1f));
        opacity = Math.Min(opacity, Math.Clamp(((zone.Y + zone.Height + 2f * radius) - pos.Y) / fadeDistY, 0f, 1f));
        return opacity;
    }

    // ── Dot lifecycle ──

    private void CreateDots()
    {
        _dots.Clear();
        _velocity = Vector2.Zero;
        _pendingMouseDeltaX = 0;
        _pendingMouseDeltaY = 0;
        _layoutDotColumns = _config.MotionDotColumns;
        _layoutDotSpacingV = _config.MotionDotSpacingV;
        _layoutDotSpacingH = _config.MotionDotSpacingH;
        _layoutSize = _config.Size;

        double dpiScale = _window != null
            ? Win32Interop.GetDpiScaleForWindow(_window.Handle)
            : Win32Interop.GetDpiScale();
        float baseDiameter = (float)(
            RenderHelper.MotionDotDiameter(_config.Size) *
            dpiScale);
        float radius = baseDiameter / 2f;
        int columns = Math.Clamp(_config.MotionDotColumns, 1, 6);
        float spacingMultV = (float)Math.Clamp(_config.MotionDotSpacingV, 0.5, 3.0);
        float spacingMultH = (float)Math.Clamp(_config.MotionDotSpacingH, 0.5, 3.0);

        for (int zi = 0; zi < _zones.Count; zi++)
        {
            var zone = _zones[zi];
            // Spread columns across full zone width, first/last at one radius from edge
            float availW = zone.Width - 2f * radius;
            float baseColSpacing = columns > 1 ? availW / (columns - 1) : 0f;
            float colSpacing = baseColSpacing * spacingMultH;
            float totalColWidth = colSpacing * (columns - 1);
            // Center if spacing multiplier < 1 makes columns narrower than zone
            float xOrigin = totalColWidth < availW
                ? zone.X + (zone.Width - totalColWidth) / 2f
                : zone.X + radius;

            // Target vertical distance between rows
            float targetRowDist = Math.Max(radius * 4f, 36f) * spacingMultV;
            int rows = Math.Max(2, (int)(zone.Height / targetRowDist) + 1);
            float rowSpacing = zone.Height / (rows - 1);

            // Checkerboard pattern: even rows use even cols, odd rows use odd cols
            for (int row = 0; row < rows; row++)
            {
                int startCol = row % 2;
                for (int col = startCol; col < columns; col += 2)
                {
                    float x = xOrigin + colSpacing * col;
                    float y = zone.Y + rowSpacing * row;

                    _dots.Add(new ZoneDot
                    {
                        Position = new Vector2(x, y),
                        BaseRadius = radius,
                        PulsePhase = (float)(_random.NextDouble() * Math.PI * 2),
                        ZoneIndex = zi,
                        SpawnOpacity = 1f
                    });
                }
            }
        }
    }

    // ── Render tick ──

    private void Timer_Tick()
    {
        if (!IsVisible) { StopTimer(); return; }

        TimeSpan now = _clock.Elapsed;
        float dt = (float)Math.Clamp((now - _lastFrame).TotalSeconds, 0, 0.05);
        _lastFrame = now;

        // ── Gamepad input (safety-gated) ──
        // Left stick = WASD-role velocity (keyboard priority slot); right stick =
        // mouse-role: a dt-compensated synthetic delta is injected into the mouse
        // accumulator so it flows through the exact mouse chain below (sensitivity,
        // inversion, decay). mouseVel = pending/(dt·60) ⇒ injecting stick·dt·speed
        // makes the synthesized velocity independent of the tick cadence.
        float gamepadVelX = 0;
        float gamepadVelY = 0;
        bool gamepadActive = false;
        if (_config.MotionGamepadEnabled &&
            XInputInterop.TryGetSticks(
                Math.Clamp((float)_config.MotionGamepadDeadzone, 0f, 0.95f),
                out float stickLX, out float stickLY, out float stickRX, out float stickRY))
        {
            (gamepadVelX, gamepadVelY) = XInputInterop.StickToVelocity(
                stickLX, stickLY, _config.MotionGamepadSensitivity, _config.MotionInverted, KeyboardBaseSpeed);

            if (stickRX != 0 || stickRY != 0)
            {
                // Full right-stick deflection ≈ KeyboardBaseSpeed dot-px/s at mouse
                // sensitivity 1.0: virtualMouseSpeed × MouseSpeedScale / 60 = KeyboardBaseSpeed
                float virtualMouseSpeed = 60f * KeyboardBaseSpeed / MouseSpeedScale;
                (float stickDx, float stickDy) = XInputInterop.StickToMouseDelta(
                    stickRX, stickRY, dt, virtualMouseSpeed);
                float rsSensitivity = (float)Math.Clamp(_config.MotionSensitivity, 0.05, 3.0);
                float rsSign = _config.MotionInverted ? -1f : 1f;
                _pendingMouseDeltaX += rsSign * stickDx * rsSensitivity * MouseSpeedScale;
                _pendingMouseDeltaY += rsSign * stickDy * rsSensitivity * MouseSpeedScale;
            }

            gamepadActive = gamepadVelX != 0 || gamepadVelY != 0 || stickRX != 0 || stickRY != 0;
        }

        // ── Mouse input (both axes, inverted) ──
        // Convert accumulated delta to velocity (px/s) by dividing by dt
        // Divide by reference frame rate (60) so effective sensitivity matches
        // the original 60Hz behavior, regardless of actual refresh rate
        float invDt = dt > 0.0001f ? 1f / dt : 60f;
        float mouseVelX = _pendingMouseDeltaX * invDt / 60f;
        float mouseVelY = _pendingMouseDeltaY * invDt / 60f;
        _pendingMouseDeltaX = 0;
        _pendingMouseDeltaY = 0;

        // Keyboard input (safety-gated)
        float keyboardVelX = 0;
        float keyboardVelY = 0;
        bool keyboardActive = false;
        if (_config.MotionKeyboardEnabled)
        {
            bool wDown = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_W) & 0x8000) != 0;
            bool sDown = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_S) & 0x8000) != 0;
            bool aDown = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_A) & 0x8000) != 0;
            bool dDown = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_D) & 0x8000) != 0;
            // A → dots move left (negative X), D → dots move right (positive X)
            // When inverted, directions are reversed
            float ksign = _config.MotionInverted ? -1f : 1f;
            float dirX = ksign * ((dDown ? 1f : 0f) - (aDown ? 1f : 0f));
            // W → dots move up (negative Y), S → dots move down (positive Y)
            float dirY = ksign * ((sDown ? 1f : 0f) - (wDown ? 1f : 0f));
            if (dirX != 0)
                keyboardVelX = dirX * KeyboardBaseSpeed * (float)Math.Clamp(_config.MotionKeyboardSensitivity, 0.05, 3.0);
            if (dirY != 0)
                keyboardVelY = dirY * KeyboardBaseSpeed * (float)Math.Clamp(_config.MotionKeyboardSensitivity, 0.05, 3.0);
            keyboardActive = dirX != 0 || dirY != 0;
        }

        // ── Velocity update ──
        float returnSeconds = (float)Math.Clamp(_config.MotionReturnMs, 80, 1200) / 1000f;
        bool mouseActive = (now - _lastMouseInput).TotalMilliseconds < 45;

        if (mouseVelX != 0)
            _velocity.X = mouseVelX;
        else if (!mouseActive)
            _velocity.X *= MathF.Exp(-dt / returnSeconds);

        if (mouseVelY != 0)
            _velocity.Y = mouseVelY;
        else if (!mouseActive)
            _velocity.Y *= MathF.Exp(-dt / returnSeconds);

        // Per-axis override chain: keyboard > left stick > mouse/decay velocity
        _velocity.X = XInputInterop.MergeAxis(_velocity.X, keyboardVelX, gamepadVelX);
        _velocity.Y = XInputInterop.MergeAxis(_velocity.Y, keyboardVelY, gamepadVelY);

        if (_velocity.LengthSquared() < 0.01f)
            _velocity = Vector2.Zero;

        // ── Move all dots ──
        Vector2 displacement = _velocity * dt;
        bool motionActive = _velocity.LengthSquared() > 0.01f;

        // Pulsing pauses when no input (mouse, keyboard or gamepad)
        bool hasInput = motionActive || mouseActive || keyboardActive || gamepadActive;
        if (hasInput)
            _pulseTime += dt;

        foreach (ZoneDot dot in _dots)
        {
            if (dot.ZoneIndex >= _zones.Count) continue;
            dot.Position += displacement;
            WrapDot(dot);
            if (dot.SpawnOpacity < 1f)
                dot.SpawnOpacity = Math.Clamp(dot.SpawnOpacity + dt * 2.5f, 0f, 1f);
            dot.MaxOpacity = ComputeEdgeOpacity(dot.Position, _zones[dot.ZoneIndex], dot.BaseRadius);
        }

        // ── Always draw for continuous pulsing ──
        if ((now - _lastPixelFrame).TotalMilliseconds >= _pixelIntervalMs)
        {
            DrawAndPresent();
            _lastPixelFrame = now;
        }

        // Timer cadence: fast when moving, slow when idle (but still running for pulsing)
        int period = motionActive ? _configuredTimerPeriodMs : 33;
        if (_currentTimerPeriodMs != period && Volatile.Read(ref _timerEnabled) != 0)
        {
            _currentTimerPeriodMs = period;
            _timer.Change(period, period);
        }

        if (motionActive)
        {
            if (Interlocked.Exchange(ref _timerResolutionActive, 1) == 0)
                TimeBeginPeriod(1);
        }
        else if (Interlocked.Exchange(ref _timerResolutionActive, 0) != 0)
            TimeEndPeriod(1);
    }

    private void WrapDot(ZoneDot dot)
    {
        if (dot.ZoneIndex >= _zones.Count) return;
        var zone = _zones[dot.ZoneIndex];
        float r = dot.BaseRadius;
        // X: wrap when dot is fully invisible (one radius beyond fade zone)
        float xMin = zone.X - r;
        float xMax = zone.X + zone.Width + r;
        // Y: wrap when dot is fully invisible (two radii beyond)
        float yMin = zone.Y - 2f * r;
        float yMax = zone.Y + zone.Height + 2f * r;

        if (dot.Position.X > xMax) { dot.Position.X = xMin + (dot.Position.X - xMax); }
        else if (dot.Position.X < xMin) { dot.Position.X = xMax - (xMin - dot.Position.X); }

        if (dot.Position.Y > yMax) { dot.Position.Y = yMin + (dot.Position.Y - yMax); }
        else if (dot.Position.Y < yMin) { dot.Position.Y = yMax - (yMin - dot.Position.Y); }
    }

    // ── Drawing ──

    private void DrawAndPresent()
    {
        if (_d2dContext == null || _brush == null || _swapChain == null) return;

        _d2dContext.BeginDraw();
        _d2dContext.Clear(new Color4(0, 0, 0, 0));

        var baseColor = ToColor4(_config.GetColor());

        foreach (ZoneDot dot in _dots)
        {
            if (dot.ZoneIndex >= _zones.Count) continue;
            var zone = _zones[dot.ZoneIndex];
            float finalOpacity = dot.MaxOpacity * dot.SpawnOpacity * zone.Opacity;
            if (finalOpacity < 0.01f) continue;

        float pulse = 1f + PulseAmplitude *
            MathF.Sin(2f * MathF.PI * _pulseTime / PulsePeriod + dot.PulsePhase);
            float radius = dot.BaseRadius * pulse;

            // Parallax scale: shrink dots near screen midline for depth illusion
            if (_config.MotionParallaxScale)
            {
                float midline = (zone.AreaLeftX + zone.AreaRightX) * 0.5f;
                float minScale = 1.0f - (float)Math.Clamp(_config.MotionParallaxAmount, 0.0, 1.0);
                float scaleT = 1f; // default: full scale

                if (zone.IsLeftSide)
                {
                    // No scaling within 80px of area's left edge
                    float deadZoneRight = zone.AreaLeftX + ParallaxEdgeDeadZone;
                    if (dot.Position.X > deadZoneRight)
                    {
                        float distFromMid = midline - dot.Position.X;
                        float maxDist = midline - deadZoneRight;
                        if (maxDist > 0.001f)
                            scaleT = Math.Clamp(distFromMid / maxDist, 0f, 1f);
                    }
                }
                else
                {
                    // No scaling within 80px of area's right edge
                    float deadZoneLeft = zone.AreaRightX - ParallaxEdgeDeadZone;
                    if (dot.Position.X < deadZoneLeft)
                    {
                        float distFromMid = dot.Position.X - midline;
                        float maxDist = deadZoneLeft - midline;
                        if (maxDist > 0.001f)
                            scaleT = Math.Clamp(distFromMid / maxDist, 0f, 1f);
                    }
                }

                // Quadratic curve for more aggressive scaling near midline
                scaleT = scaleT * scaleT;
                radius *= minScale + (1f - minScale) * scaleT;
            }

            _brush.Color = new Color4(baseColor.R, baseColor.G, baseColor.B, 1);
            _brush.Opacity = Math.Clamp(finalOpacity, 0, 1);

            _d2dContext.FillEllipse(
                new Ellipse(dot.Position, radius, radius), _brush);
        }

        _d2dContext.EndDraw(out _, out _).CheckError();
        _swapChain.Present(0, PresentFlags.None).CheckError();
    }

    // ── D3D / D2D / DComp setup ──

    private void CreateNativeWindow()
    {
        _window = new MotionOverlayNativeWindow(_screenX, _screenY, _width, _height);
        EnsureClickThroughStyles(_window.Handle);
        SetLayeredWindowAttributes(_window.Handle, 0, 255, LwaAlpha);
    }

    private void CreateGraphicsResources()
    {
        D3DFeatureLevel[] featureLevels =
        [
            D3DFeatureLevel.Level_11_1, D3DFeatureLevel.Level_11_0,
            D3DFeatureLevel.Level_10_1, D3DFeatureLevel.Level_10_0
        ];

        D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
            featureLevels, out _d3dDevice, out _, out _d3dContext).CheckError();

        using IDXGIDevice dxgiDevice = _d3dDevice.QueryInterface<IDXGIDevice>();
        using IDXGIAdapter adapter = dxgiDevice.GetAdapter();
        using IDXGIFactory2 factory = adapter.GetParent<IDXGIFactory2>();

        _swapChain = factory.CreateSwapChainForComposition(_d3dDevice,
            new SwapChainDescription1
            {
                Width = (uint)_width, Height = (uint)_height,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = DXGIAlphaMode.Premultiplied,
                Flags = SwapChainFlags.None
            }, null);

        _d2dFactory = D2D1CreateFactory<ID2D1Factory1>(FactoryType.SingleThreaded, DebugLevel.None);
        _d2dDevice = _d2dFactory.CreateDevice(dxgiDevice);
        _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
        CreateDirect2DTarget();
        _brush = _d2dContext.CreateSolidColorBrush(new Color4(1, 1, 1, 1));

        _compositionDevice = DCompositionCreateDevice<IDCompositionDevice>(dxgiDevice);
        _compositionDevice.CreateTargetForHwnd(_window!.Handle, true, out _compositionTarget).CheckError();
        _compositionDevice.CreateVisual(out _compositionVisual).CheckError();
        _compositionVisual.SetContent(_swapChain).CheckError();
        _compositionTarget.SetRoot(_compositionVisual).CheckError();
        _compositionDevice.Commit().CheckError();
    }

    private void CreateDirect2DTarget()
    {
        using IDXGISurface surface = _swapChain!.GetBuffer<IDXGISurface>(0);
        _targetBitmap = _d2dContext!.CreateBitmapFromDxgiSurface(surface,
            new BitmapProperties1(
                new D2DPixelFormat(Format.B8G8R8A8_UNorm, D2DAlphaMode.Premultiplied),
                96, 96, BitmapOptions.Target | BitmapOptions.CannotDraw));
        _d2dContext.Target = _targetBitmap;
    }

    private void Resize(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _screenX = Win32Interop.GetVirtualScreenX();
        _screenY = Win32Interop.GetVirtualScreenY();
        if (_window != null)
            SetWindowPos(_window.Handle, HwndTopmost, _screenX, _screenY, _width, _height,
                SwpNoActivate | (_visible ? SwpShowWindow : 0));
        _d2dContext!.Target = null;
        _targetBitmap?.Dispose();
        _targetBitmap = null;
        _swapChain!.ResizeBuffers(2, (uint)_width, (uint)_height,
            Format.B8G8R8A8_UNorm, SwapChainFlags.None).CheckError();
        CreateDirect2DTarget();
    }

    // ── Timer management ──

    private void EnsureTimer()
    {
        if (Interlocked.Exchange(ref _timerEnabled, 1) != 0) return;
        if (Interlocked.Exchange(ref _timerResolutionActive, 1) == 0)
            TimeBeginPeriod(1);
        _lastFrame = _clock.Elapsed;
        _currentTimerPeriodMs = _configuredTimerPeriodMs;
        _timer.Change(0, _currentTimerPeriodMs);
    }

    private void StopTimer()
    {
        if (Interlocked.Exchange(ref _timerEnabled, 0) == 0) return;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        if (Interlocked.Exchange(ref _timerResolutionActive, 0) != 0)
            TimeEndPeriod(1);
    }

    private void QueueRenderTick(object? state)
    {
        if (_disposed || Volatile.Read(ref _timerEnabled) == 0 ||
            Interlocked.Exchange(ref _tickQueued, 1) != 0) return;
        try
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
            {
                Interlocked.Exchange(ref _tickQueued, 0);
                if (!_disposed && Volatile.Read(ref _timerEnabled) != 0)
                    Timer_Tick();
            }));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref _tickQueued, 0);
            StopTimer();
        }
    }

    // ── Power / scheduling ──

    private static void EnsureClickThroughStyles(IntPtr window)
    {
        long styles = GetWindowLongPtrCompat(window, GwlExStyle).ToInt64();
        styles |= WsExTransparent | WsExLayered | WsExToolWindow | WsExNoActivate;
        SetWindowLongPtrCompat(window, GwlExStyle, new IntPtr(styles));
    }

    private void DisableBackgroundThrottling()
    {
        IntPtr process = GetCurrentProcess();
        var exec = new ProcessPowerThrottlingState
        { Version = PowerThrottlingCurrentVersion, ControlMask = PowerThrottlingExecutionSpeed, StateMask = 0 };
        bool e = SetProcessInformation(process, ProcessPowerThrottling, ref exec,
            (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
        var timer = new ProcessPowerThrottlingState
        { Version = PowerThrottlingCurrentVersion, ControlMask = PowerThrottlingIgnoreTimerResolution, StateMask = 0 };
        bool t = SetProcessInformation(process, ProcessPowerThrottling, ref timer,
            (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
        _powerThrottlingOverridden = e || t;
    }

    private void RestoreSystemPowerThrottling()
    {
        if (!_powerThrottlingOverridden) return;
        var state = new ProcessPowerThrottlingState
        { Version = PowerThrottlingCurrentVersion, ControlMask = 0, StateMask = 0 };
        SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state,
            (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
        _powerThrottlingOverridden = false;
    }

    private void EnableMultimediaScheduling()
    {
        if (_mmcssHandle != IntPtr.Zero) return;
        uint taskIndex = 0;
        _mmcssHandle = AvSetMmThreadCharacteristics("Games", ref taskIndex);
        if (_mmcssHandle != IntPtr.Zero) AvSetMmThreadPriority(_mmcssHandle, 1);
    }

    private void DisableMultimediaScheduling()
    {
        if (_mmcssHandle == IntPtr.Zero) return;
        AvRevertMmThreadCharacteristics(_mmcssHandle);
        _mmcssHandle = IntPtr.Zero;
    }

    // ── Utility ──

    private static Color4 ToColor4(System.Windows.Media.Color c) =>
        new(c.R / 255f, c.G / 255f, c.B / 255f, 1);

    // ── Dispose ──

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopTimer();
        _timer.Dispose();
        SetVisible(false);
        RestoreSystemPowerThrottling();
        DisposeGraphicsResources();
        Win32Interop.UnregisterRawMouseInput();
        _rawInputWindow?.Dispose(); _rawInputWindow = null;
        _window?.Dispose(); _window = null;
        GC.SuppressFinalize(this);
    }

    private void DisposeGraphicsResources()
    {
        if (_d2dContext != null) _d2dContext.Target = null;
        _brush?.Dispose(); _targetBitmap?.Dispose();
        _d2dContext?.Dispose(); _d2dDevice?.Dispose(); _d2dFactory?.Dispose();
        _compositionVisual?.Dispose(); _compositionTarget?.Dispose(); _compositionDevice?.Dispose();
        _swapChain?.Dispose(); _d3dContext?.Dispose(); _d3dDevice?.Dispose();
        _brush = null; _targetBitmap = null; _d2dContext = null; _d2dDevice = null;
        _d2dFactory = null; _compositionVisual = null; _compositionTarget = null;
        _compositionDevice = null; _swapChain = null; _d3dContext = null; _d3dDevice = null;
    }

    // ── Win32 P/Invoke ──

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr w, int c);
    // ── Timer resolution management (timeBeginPeriod tradeoff) ──────────
    //
    // timeBeginPeriod(1) is a **process-wide** Win32 call that requests 1 ms
    // timer resolution from the OS. Without it, System.Threading.Timer fires
    // at ~15.6 ms intervals, which is too coarse for 120 Hz motion animation.
    //
    // Known tradeoffs:
    //   • Power: prevents the CPU from entering deep C-states, increasing
    //     idle power consumption by ~1–2 W on typical desktop hardware.
    //   • Scope: the resolution change affects the entire process, not just
    //     our timer. This is acceptable since we are a single-purpose overlay.
    //   • Windows 11 (2004+): SetProcessInformation with
    //     PowerThrottlingIgnoreTimerResolution is also used (see
    //     DisableBackgroundThrottling) to let the OS manage timer resolution
    //     more intelligently on battery-powered devices.
    //
    // Mitigation strategy:
    //   • timeBeginPeriod(1) is called ONLY when motion dots are actively
    //     animating (velocity > threshold). When idle, timeEndPeriod(1) is
    //     called to restore the default resolution.
    //   • The Interlocked.Exchange pattern ensures thread-safe, paired
    //     begin/end calls — no leaked timer resolutions.
    //   • On Dispose(), StopTimer() guarantees timeEndPeriod(1) is called.
    //
    // This is a deliberate, documented tradeoff: the visual smoothness of
    // motion sickness relief dots at high refresh rates (120–360 Hz) requires
    // sub-15ms timer precision, which is only available via timeBeginPeriod.

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")] private static extern uint TimeBeginPeriod(uint p);
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")] private static extern uint TimeEndPeriod(uint p);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState { public uint Version; public uint ControlMask; public uint StateMask; }

    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(IntPtr p, int c, ref ProcessPowerThrottlingState i, uint s);

    [DllImport("avrt.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AvSetMmThreadCharacteristics(string n, ref uint i);
    [DllImport("avrt.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AvSetMmThreadPriority(IntPtr h, int p);
    [DllImport("avrt.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AvRevertMmThreadCharacteristics(IntPtr h);

    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr w, uint c, byte a, uint f);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr w, int i);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr w, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr w, int i, int v);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr w, int i, IntPtr v);

    private static IntPtr GetWindowLongPtrCompat(IntPtr w, int i) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(w, i) : new IntPtr(GetWindowLong32(w, i));
    private static IntPtr SetWindowLongPtrCompat(IntPtr w, int i, IntPtr v) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(w, i, v) : new IntPtr(SetWindowLong32(w, i, v.ToInt32()));

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr w, IntPtr a, int x, int y, int width, int height, uint flags);
}
