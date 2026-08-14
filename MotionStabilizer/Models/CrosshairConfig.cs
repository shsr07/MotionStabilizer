using System.Windows.Media;

namespace MotionStabilizer.Models;

/// <summary>
/// Configuration for the center crosshair (中心准星).
/// Implements <see cref="INotifyPropertyChanged"/> via <see cref="ObservableObject"/>.
/// </summary>
public class CrosshairConfig : ObservableObject
{
    /// <summary>Default crosshair offset (centered).</summary>
    public const int DefaultPositionX = 0;
    public const int DefaultPositionY = 0;

    private bool _isVisible = false;
    private CrosshairShape _shape = CrosshairShape.Cross;
    private AspectRatio _aspectRatio = AspectRatio.Ratio16x9;
    private SizePreset _size = SizePreset.M;
    private OffsetLevel _thickness = OffsetLevel.Plus3;
    private int _positionX = DefaultPositionX;
    private int _positionY = DefaultPositionY;
    private SplitScreen _split = SplitScreen.None;
    private ColorPreset _colorPreset = ColorPreset.Red;
    private string _customColorHex = "#FFFF00";
    private int _opacity = 80;

    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    public CrosshairShape Shape { get => _shape; set => SetProperty(ref _shape, value); }
    public AspectRatio AspectRatio { get => _aspectRatio; set => SetProperty(ref _aspectRatio, value); }
    public SizePreset Size { get => _size; set => SetProperty(ref _size, value); }
    public OffsetLevel Thickness { get => _thickness; set => SetProperty(ref _thickness, value); }
    public int PositionX { get => _positionX; set => SetProperty(ref _positionX, value); }
    public int PositionY { get => _positionY; set => SetProperty(ref _positionY, value); }
    public SplitScreen Split { get => _split; set => SetProperty(ref _split, value); }
    public ColorPreset ColorPreset { get => _colorPreset; set => SetProperty(ref _colorPreset, value); }
    public string CustomColorHex { get => _customColorHex; set => SetProperty(ref _customColorHex, value); }
    public int Opacity { get => _opacity; set => SetProperty(ref _opacity, value); }

    /// <summary>Reset the position offset to the default (0, 0).</summary>
    public void ResetPosition()
    {
        PositionX = DefaultPositionX;
        PositionY = DefaultPositionY;
    }

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
