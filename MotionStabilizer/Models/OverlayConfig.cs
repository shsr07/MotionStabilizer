using System.Text.Json.Serialization;
using System.Windows.Media;

namespace MotionStabilizer.Models;

/// <summary>
/// Configuration for the edge overlay (边缘叠加).
/// </summary>
public class OverlayConfig
{
    public bool IsVisible { get; set; } = false;
    public OverlayShape Shape { get; set; } = OverlayShape.Box;
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Ratio16x9;
    public SizePreset Size { get; set; } = SizePreset.M;
    public OffsetLevel Length { get; set; } = OffsetLevel.Plus0;
    public DisplayMode Mode { get; set; } = DisplayMode.Stretch;
    public SplitScreen Split { get; set; } = SplitScreen.None;
    public ColorPreset ColorPreset { get; set; } = ColorPreset.Green;
    public string CustomColorHex { get; set; } = "#00FF00";
    public int Opacity { get; set; } = 60;

    // ── Per-edge visibility & opacity ──
    public EdgeOpacityMode OpacityMode { get; set; } = EdgeOpacityMode.Uniform;
    public bool EdgeTopVisible { get; set; } = true;
    public bool EdgeBottomVisible { get; set; } = true;
    public bool EdgeLeftVisible { get; set; } = true;
    public bool EdgeRightVisible { get; set; } = true;
    public int EdgeTopOpacity { get; set; } = 60;
    public int EdgeBottomOpacity { get; set; } = 60;
    public int EdgeLeftOpacity { get; set; } = 60;
    public int EdgeRightOpacity { get; set; } = 60;

    // ── Dynamic motion cue settings (MotionDots shape) ──
    /// <summary>
    /// Safety gate: when false, GetAsyncKeyState is never called.
    /// Deliberately never persisted ([JsonIgnore]) — keyboard control must be
    /// re-confirmed after every restart, so a crash can never leave it enabled.
    /// </summary>
    [JsonIgnore]
    public bool MotionKeyboardEnabled { get; set; } = false;
    public int MotionDotCount { get; set; } = 6;
    public int MotionDotColumns { get; set; } = 2;
    public double MotionDotSpacingV { get; set; } = 1.0;
    public double MotionDotSpacingH { get; set; } = 0.7;
    public double MotionSensitivity { get; set; } = 1.5;
    public double MotionKeyboardSensitivity { get; set; } = 1.0;
    public int MotionReturnMs { get; set; } = 260;
    public int MotionRefreshRate { get; set; } = 120;
    public bool MotionInverted { get; set; } = false;
    public bool MotionParallaxScale { get; set; } = true;
    public double MotionParallaxAmount { get; set; } = 0.8;

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
