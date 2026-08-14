using System.Windows.Media;

namespace MotionStabilizer.Models;

/// <summary>
/// Configuration for the floating clock (悬浮时钟).
/// Implements <see cref="INotifyPropertyChanged"/> via <see cref="ObservableObject"/>.
/// </summary>
public class ClockConfig : ObservableObject
{
    /// <summary>Default clock position (DIP relative to the overlay window's top-left corner).</summary>
    public const int DefaultPositionX = 20;
    public const int DefaultPositionY = 20;

    private bool _isVisible = false;
    private ClockFormat _format = ClockFormat.HHmm;
    private string _fontFamily = "Outline";
    private int _fontSize = 24;
    private string _colorHex = "#FFFFFF";
    private int _positionX = DefaultPositionX;
    private int _positionY = DefaultPositionY;
    private int _opacity = 100;

    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public ClockFormat Format { get => _format; set => SetProperty(ref _format, value); }
    public string FontFamily { get => _fontFamily; set => SetProperty(ref _fontFamily, value); }
    public int FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value); }
    public string ColorHex { get => _colorHex; set => SetProperty(ref _colorHex, value); }
    public int PositionX { get => _positionX; set => SetProperty(ref _positionX, value); }
    public int PositionY { get => _positionY; set => SetProperty(ref _positionY, value); }
    public int Opacity { get => _opacity; set => SetProperty(ref _opacity, value); }

    /// <summary>Reset the position to the default (20, 20).</summary>
    public void ResetPosition()
    {
        PositionX = DefaultPositionX;
        PositionY = DefaultPositionY;
    }

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
