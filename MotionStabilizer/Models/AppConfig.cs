namespace MotionStabilizer.Models;

/// <summary>
/// Global application options (选项).
/// Implements <see cref="INotifyPropertyChanged"/> via <see cref="ObservableObject"/>.
/// </summary>
public class AppConfig : ObservableObject
{
    // Behavior settings
    private bool _minimizeToTrayOnStart = false;
    private bool _autoSaveOnClose = true;
    private bool _confirmBeforeClose = true;

    // UI customization
    private UIScale _scale = UIScale.Auto;
    private Language _language = Language.Chinese;

    public bool MinimizeToTrayOnStart { get => _minimizeToTrayOnStart; set => SetProperty(ref _minimizeToTrayOnStart, value); }
    public bool AutoSaveOnClose { get => _autoSaveOnClose; set => SetProperty(ref _autoSaveOnClose, value); }
    public bool ConfirmBeforeClose { get => _confirmBeforeClose; set => SetProperty(ref _confirmBeforeClose, value); }

    public UIScale Scale { get => _scale; set => SetProperty(ref _scale, value); }
    public Language Language { get => _language; set => SetProperty(ref _language, value); }

    private string _targetMonitor = "";

    /// <summary>
    /// PnP Device ID of the monitor to render overlays on. Empty means all monitors.
    /// Uses the monitor-level PnP path from EnumDisplayDevices (e.g.
    /// \\?\DISPLAY#DELF0F81#5&amp;2e8a1c4a&amp;0&amp;UID256#{...}) which is more
    /// stable than the GDI device name (\\.\DISPLAY1) that can swap on reboot.
    /// If the saved target is not found, falls back to all monitors.
    /// </summary>
    public string TargetMonitor { get => _targetMonitor; set => SetProperty(ref _targetMonitor, value); }
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
