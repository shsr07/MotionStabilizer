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

    /// <summary>Returns the actual Color based on preset or custom value.</summary>
    public Color GetColor()
    {
        return ColorPreset switch
        {
            ColorPreset.Red => Color.FromRgb(0xFF, 0x00, 0x00),
            ColorPreset.Green => Color.FromRgb(0x00, 0xFF, 0x00),
            ColorPreset.Yellow => Color.FromRgb(0xFF, 0xFF, 0x00),
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
}
