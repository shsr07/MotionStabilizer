using System.Windows.Media;

namespace MotionStabilizer.Models;

/// <summary>
/// Configuration for the center crosshair (中心准星).
/// </summary>
public class CrosshairConfig
{
    public bool IsVisible { get; set; } = false;
    public CrosshairShape Shape { get; set; } = CrosshairShape.Cross;
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Ratio16x9;
    public SizePreset Size { get; set; } = SizePreset.M;
    public OffsetLevel Thickness { get; set; } = OffsetLevel.Plus3;
    public int PositionX { get; set; } = 0;  // offset from center, 0 = dead center
    public int PositionY { get; set; } = 0;
    public SplitScreen Split { get; set; } = SplitScreen.None;
    public ColorPreset ColorPreset { get; set; } = ColorPreset.Red;
    public string CustomColorHex { get; set; } = "#FF0000";
    public int Opacity { get; set; } = 80;

    public Color GetColor()
    {
        return ColorPreset switch
        {
            ColorPreset.Red => Color.FromRgb(0xFF, 0x00, 0x00),
            ColorPreset.Green => Color.FromRgb(0x00, 0xFF, 0x00),
            ColorPreset.Blue => Color.FromRgb(0x00, 0x99, 0xFF),
            ColorPreset.Custom => TryParseColor(CustomColorHex, Color.FromRgb(0xFF, 0x00, 0x00)),
            _ => Color.FromRgb(0xFF, 0x00, 0x00)
        };
    }

    private static Color TryParseColor(string hex, Color fallback)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return fallback;
        }
    }
}
