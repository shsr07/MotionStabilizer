using System.Windows.Media;

namespace MotionStabilizer.Models;

/// <summary>
/// Configuration for the floating clock (悬浮时钟).
/// </summary>
public class ClockConfig
{
    public bool IsVisible { get; set; } = false;
    public ClockFormat Format { get; set; } = ClockFormat.HHmm;
    public string FontFamily { get; set; } = "Outline";
    public int FontSize { get; set; } = 24;
    public string ColorHex { get; set; } = "#FFFFFF";
    public int PositionX { get; set; } = 20;
    public int PositionY { get; set; } = 20;
    public int Opacity { get; set; } = 100;

    /// <summary>
    /// Returns the actual WPF font family for rendering.
    /// "Outline" is a pseudo-font that uses Consolas with a DropShadowEffect outline.
    /// </summary>
    public string GetRenderFontFamily()
    {
        return FontFamily == "Outline" ? "Consolas" : FontFamily;
    }

    /// <summary>Whether the current font selection is the outline pseudo-font.</summary>
    public bool IsOutlineFont => FontFamily == "Outline";

    public Color GetColor()
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(ColorHex);
        }
        catch
        {
            return Colors.White;
        }
    }
}
