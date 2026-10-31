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
    private string _customColorHex = "#FFFFFF";
    private int _opacity = 80;

    // ── Crosshair outline (mirrors the overlay outline: on by default, black) ──
    private bool _outlineEnabled = true;
    private OutlineColorPreset _outlineColorPreset = OutlineColorPreset.Black;
    private string _outlineCustomHex = "#FFFFFF";
    private double _outlineWidth = 0.5;

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

    // ── Crosshair outline ──

    /// <summary>
    /// Draw a contrasting backdrop behind every crosshair element (ring, lines,
    /// diamond, centre dot) so the crosshair stays visible when the game frame
    /// matches its colour. On by default in black, mirroring the overlay outline.
    /// </summary>
    public bool OutlineEnabled { get => _outlineEnabled; set => SetProperty(ref _outlineEnabled, value); }

    /// <summary>Outline colour preset for the crosshair.</summary>
    public OutlineColorPreset OutlineColorPreset
    {
        get => _outlineColorPreset;
        set => SetProperty(ref _outlineColorPreset, value);
    }

    public string OutlineCustomHex { get => _outlineCustomHex; set => SetProperty(ref _outlineCustomHex, value); }

    /// <summary>
    /// Outline thickness in pixels; the visible band extends this far beyond
    /// each crosshair element. Deliberately absolute rather than proportional
    /// to the crosshair size, same reasoning as the overlay outline.
    /// </summary>
    public double OutlineWidth { get => _outlineWidth; set => SetProperty(ref _outlineWidth, value); }

    /// <summary>Effective outline colour, resolving the custom hex when selected.</summary>
    public Color GetOutlineColor()
    {
        return OutlineColorPreset switch
        {
            OutlineColorPreset.White => Color.FromRgb(0xFF, 0xFF, 0xFF),
            OutlineColorPreset.Black => Color.FromRgb(0x00, 0x00, 0x00),
            OutlineColorPreset.Custom => TryParseColor(OutlineCustomHex, Color.FromRgb(0x00, 0x00, 0x00)),
            _ => Color.FromRgb(0x00, 0x00, 0x00)
        };
    }

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
            ColorPreset.Custom => TryParseColor(CustomColorHex, Color.FromRgb(0xFF, 0xFF, 0xFF)),
            _ => Color.FromRgb(0xFF, 0xFF, 0xFF)
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
