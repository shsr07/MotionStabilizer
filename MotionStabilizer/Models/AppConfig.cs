namespace MotionStabilizer.Models;

/// <summary>
/// Global application options (选项).
/// </summary>
public class AppConfig
{
    // Behavior settings
    public bool MinimizeToTrayOnStart { get; set; } = false;
    public bool AutoSaveOnClose { get; set; } = true;
    public bool ConfirmBeforeClose { get; set; } = true;

    // UI customization
    public UIScale Scale { get; set; } = UIScale.Auto;
    public Language Language { get; set; } = Language.Chinese;
}

/// <summary>
/// The complete profile that can be saved/loaded (overlay + crosshair + clock).
/// Hotkeys are intentionally excluded per spec.
/// </summary>
public class ProfileData
{
    public string ProfileName { get; set; } = "Default";
    public OverlayConfig Overlay { get; set; } = new();
    public CrosshairConfig Crosshair { get; set; } = new();
    public ClockConfig Clock { get; set; } = new();
}
