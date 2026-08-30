using System.Text.Json.Serialization;
using System.Windows.Media;

namespace MotionStabilizer.Models;

/// <summary>
/// Configuration for the edge overlay (边缘叠加).
/// Implements <see cref="INotifyPropertyChanged"/> via <see cref="ObservableObject"/>
/// so that the <see cref="ConfigStore"/> and overlay renderer are automatically
/// notified when any property changes.
/// </summary>
public class OverlayConfig : ObservableObject
{
    private bool _isVisible = false;
    private OverlayShape _shape = OverlayShape.Pole;
    private AspectRatio _aspectRatio = AspectRatio.Ratio16x9;
    private SizePreset _size = SizePreset.M;
    private OffsetLevel _length = OffsetLevel.Plus0;
    private DisplayMode _mode = DisplayMode.Stretch;
    private SplitScreen _split = SplitScreen.None;
    private ColorPreset _colorPreset = ColorPreset.Green;
    private string _customColorHex = "#FFFF00";
    private int _opacity = 60;

    // ── Per-edge visibility & opacity ──
    private EdgeOpacityMode _opacityMode = EdgeOpacityMode.Uniform;
    private bool _edgeTopVisible = true;
    private bool _edgeBottomVisible = true;
    private bool _edgeLeftVisible = true;
    private bool _edgeRightVisible = true;
    private int _edgeTopOpacity = 60;
    private int _edgeBottomOpacity = 60;
    private int _edgeLeftOpacity = 60;
    private int _edgeRightOpacity = 60;

    // ── Dynamic motion cue settings (MotionDots shape) ──
    private bool _motionKeyboardEnabled = false;
    private bool _motionGamepadEnabled = false;
    private int _motionDotCount = 6;
    private int _motionDotColumns = 2;
    private double _motionDotSpacingV = 1.0;
    private double _motionDotSpacingH = 0.7;
    private double _motionSensitivity = 1.5;
    private double _motionKeyboardSensitivity = 1.0;
    private double _motionGamepadSensitivity = 1.0;
    // Keep in sync with XInputInterop.DefaultDeadzone — Models cannot reference
    // the internal Services type from a public config class.
    private double _motionGamepadDeadzone = 0.15;
    private int _motionReturnMs = 260;
    private int _motionRefreshRate = 120;
    private bool _motionInverted = false;
    private bool _motionParallaxScale = true;
    private double _motionParallaxAmount = 0.8;

    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public OverlayShape Shape { get => _shape; set => SetProperty(ref _shape, value); }
    public AspectRatio AspectRatio { get => _aspectRatio; set => SetProperty(ref _aspectRatio, value); }
    public SizePreset Size { get => _size; set => SetProperty(ref _size, value); }
    public OffsetLevel Length { get => _length; set => SetProperty(ref _length, value); }
    public DisplayMode Mode { get => _mode; set => SetProperty(ref _mode, value); }
    public SplitScreen Split { get => _split; set => SetProperty(ref _split, value); }
    public ColorPreset ColorPreset { get => _colorPreset; set => SetProperty(ref _colorPreset, value); }
    public string CustomColorHex { get => _customColorHex; set => SetProperty(ref _customColorHex, value); }
    public int Opacity { get => _opacity; set => SetProperty(ref _opacity, value); }

    // ── Per-edge visibility & opacity ──
    public EdgeOpacityMode OpacityMode { get => _opacityMode; set => SetProperty(ref _opacityMode, value); }
    public bool EdgeTopVisible { get => _edgeTopVisible; set => SetProperty(ref _edgeTopVisible, value); }
    public bool EdgeBottomVisible { get => _edgeBottomVisible; set => SetProperty(ref _edgeBottomVisible, value); }
    public bool EdgeLeftVisible { get => _edgeLeftVisible; set => SetProperty(ref _edgeLeftVisible, value); }
    public bool EdgeRightVisible { get => _edgeRightVisible; set => SetProperty(ref _edgeRightVisible, value); }
    public int EdgeTopOpacity { get => _edgeTopOpacity; set => SetProperty(ref _edgeTopOpacity, value); }
    public int EdgeBottomOpacity { get => _edgeBottomOpacity; set => SetProperty(ref _edgeBottomOpacity, value); }
    public int EdgeLeftOpacity { get => _edgeLeftOpacity; set => SetProperty(ref _edgeLeftOpacity, value); }
    public int EdgeRightOpacity { get => _edgeRightOpacity; set => SetProperty(ref _edgeRightOpacity, value); }

    // ── Dynamic motion cue settings (MotionDots shape) ──
    /// <summary>
    /// Safety gate: when false, GetAsyncKeyState is never called.
    /// Deliberately never persisted ([JsonIgnore]) — keyboard control must be
    /// re-confirmed after every restart, so a crash can never leave it enabled.
    /// </summary>
    [JsonIgnore]
    public bool MotionKeyboardEnabled { get => _motionKeyboardEnabled; set => SetProperty(ref _motionKeyboardEnabled, value); }

    /// <summary>
    /// Safety gate: when false, XInput sticks are never polled.
    /// Deliberately never persisted ([JsonIgnore]) — gamepad control must be
    /// re-confirmed after every restart, matching MotionKeyboardEnabled.
    /// </summary>
    [JsonIgnore]
    public bool MotionGamepadEnabled { get => _motionGamepadEnabled; set => SetProperty(ref _motionGamepadEnabled, value); }
    /// <summary>
    /// Radial stick deadzone as a fraction of full deflection (0–1). Persisted
    /// like the sensitivity settings — unlike the enable gates this is a plain
    /// tuning preference. Raise it to absorb stick drift on worn controllers.
    /// </summary>
    public double MotionGamepadDeadzone { get => _motionGamepadDeadzone; set => SetProperty(ref _motionGamepadDeadzone, value); }
    public int MotionDotCount { get => _motionDotCount; set => SetProperty(ref _motionDotCount, value); }
    public int MotionDotColumns { get => _motionDotColumns; set => SetProperty(ref _motionDotColumns, value); }
    public double MotionDotSpacingV { get => _motionDotSpacingV; set => SetProperty(ref _motionDotSpacingV, value); }
    public double MotionDotSpacingH { get => _motionDotSpacingH; set => SetProperty(ref _motionDotSpacingH, value); }
    /// <summary>
    /// Mouse/right-stick turning sensitivity, persisted on the legacy internal scale
    /// (default 1.5). The settings UI displays value / 1.5, so the default shows as
    /// "1.0x" while feeling identical to the pre-2.8.0 "1.5x" default. Stored values
    /// from older profiles therefore keep their exact feel without migration.
    /// </summary>
    public double MotionSensitivity { get => _motionSensitivity; set => SetProperty(ref _motionSensitivity, value); }
    public double MotionKeyboardSensitivity { get => _motionKeyboardSensitivity; set => SetProperty(ref _motionKeyboardSensitivity, value); }
    public double MotionGamepadSensitivity { get => _motionGamepadSensitivity; set => SetProperty(ref _motionGamepadSensitivity, value); }
    public int MotionReturnMs { get => _motionReturnMs; set => SetProperty(ref _motionReturnMs, value); }
    public int MotionRefreshRate { get => _motionRefreshRate; set => SetProperty(ref _motionRefreshRate, value); }
    public bool MotionInverted { get => _motionInverted; set => SetProperty(ref _motionInverted, value); }
    public bool MotionParallaxScale { get => _motionParallaxScale; set => SetProperty(ref _motionParallaxScale, value); }
    public double MotionParallaxAmount { get => _motionParallaxAmount; set => SetProperty(ref _motionParallaxAmount, value); }

    /// <summary>Returns the actual Color based on preset or custom value.</summary>
    public Color GetColor()
    {
        return ColorPreset switch
        {
            ColorPreset.Red => Color.FromRgb(0xFF, 0x00, 0x00),
            ColorPreset.Green => Color.FromRgb(0x00, 0xFF, 0x00),
            ColorPreset.Blue => Color.FromRgb(0x00, 0x99, 0xFF),
            ColorPreset.Custom => TryParseColor(CustomColorHex, Color.FromRgb(0x00, 0xFF, 0x00)),
            _ => Color.FromRgb(0x00, 0xFF, 0x00)
        };
    }

    private static Color TryParseColor(string hex, Color fallback)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return c;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>Whether the given edge shape should be drawn.</summary>
    public bool IsEdgeVisible(EdgeSide side) => side switch
    {
        EdgeSide.Top => EdgeTopVisible,
        EdgeSide.Bottom => EdgeBottomVisible,
        EdgeSide.Left => EdgeLeftVisible,
        EdgeSide.Right => EdgeRightVisible,
        _ => true
    };

    /// <summary>Effective opacity for an edge, considering the opacity mode.</summary>
    public int GetEdgeOpacity(EdgeSide side)
    {
        if (OpacityMode == EdgeOpacityMode.Uniform) return Opacity;
        return side switch
        {
            EdgeSide.Top => EdgeTopOpacity,
            EdgeSide.Bottom => EdgeBottomOpacity,
            EdgeSide.Left => EdgeLeftOpacity,
            EdgeSide.Right => EdgeRightOpacity,
            _ => Opacity
        };
    }
}
