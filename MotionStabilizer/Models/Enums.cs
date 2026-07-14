namespace MotionStabilizer.Models;

/// <summary>Overlay shape types</summary>
public enum OverlayShape
{
    Box,
    Dome,
    Flag
}

/// <summary>Crosshair shape types</summary>
public enum CrosshairShape
{
    Circle,
    Cross,
    Diamond
}

/// <summary>Aspect ratio options</summary>
public enum AspectRatio
{
    Ratio16x9,
    Ratio21x9
}

/// <summary>Size presets (2XS through 2XL)</summary>
public enum SizePreset
{
    XXS,
    XS,
    S,
    M,
    L,
    XL,
    XXL
}

/// <summary>Length/thickness offset levels (+0 through +6)</summary>
public enum OffsetLevel
{
    Plus0,
    Plus1,
    Plus2,
    Plus3,
    Plus4,
    Plus5,
    Plus6
}

/// <summary>Overlay display mode</summary>
public enum DisplayMode
{
    Window,
    Stretch
}

/// <summary>Split screen direction</summary>
public enum SplitScreen
{
    None,
    Vertical,
    Horizontal
}

/// <summary>Color presets</summary>
public enum ColorPreset
{
    Red,
    Green,
    Yellow,
    Custom
}

/// <summary>Clock time format</summary>
public enum ClockFormat
{
    HHmm,
    HHmmss,
    hmmAmPm
}

/// <summary>UI scale options</summary>
public enum UIScale
{
    Auto,
    Percent100,
    Percent125,
    Percent150,
    Percent200
}

/// <summary>Supported languages</summary>
public enum Language
{
    Chinese,
    English
}
