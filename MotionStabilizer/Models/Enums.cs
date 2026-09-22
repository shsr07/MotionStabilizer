namespace MotionStabilizer.Models;

// ──────────────────────────────────────────────────────────────────────────
// Every enum in this file is serialized BY NAME into profile JSON and
// appconfig.json (JsonStringEnumConverter). Renaming, removing or reordering a
// member that has already shipped therefore breaks every existing user's config
// file: System.Text.Json throws on an unrecognised member name, and the working
// profile is loaded during startup — a single rename can keep the app from
// launching at all, for every user upgrading to that build.
//
// Rule: only APPEND new members. Never rename, never remove, never renumber.
// ──────────────────────────────────────────────────────────────────────────

/// <summary>Overlay shape types</summary>
public enum OverlayShape
{
    Pole,
    Box,
    Dome,
    Flag,
    MotionDots
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
    Ratio21x9,
    Ratio4x3,
    Ratio5x4
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
    Blue,
    Custom
}

/// <summary>Clock time format</summary>
public enum ClockFormat
{
    HHmm,
    HHmmss,
    HhMmAmPm
}

/// <summary>UI scale options</summary>
public enum UIScale
{
    Percent75,
    Percent100,
    Percent125,
    Auto
}

/// <summary>Supported languages</summary>
public enum Language
{
    Chinese,
    English
}

/// <summary>Edge sides of the overlay</summary>
public enum EdgeSide
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>Edge opacity control mode</summary>
public enum EdgeOpacityMode
{
    Uniform,
    PerEdge
}
