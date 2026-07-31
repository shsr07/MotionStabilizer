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
