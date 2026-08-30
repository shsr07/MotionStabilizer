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
    private bool _hotkeyWarningAcknowledged = false;
    private bool _motionKeyboardWarningAcknowledged = false;
    private bool _motionGamepadWarningAcknowledged = false;

    // UI customization
    private UIScale _scale = UIScale.Auto;
    private Language _language = Language.Chinese;

    public bool MinimizeToTrayOnStart { get => _minimizeToTrayOnStart; set => SetProperty(ref _minimizeToTrayOnStart, value); }
    public bool AutoSaveOnClose { get => _autoSaveOnClose; set => SetProperty(ref _autoSaveOnClose, value); }
    public bool ConfirmBeforeClose { get => _confirmBeforeClose; set => SetProperty(ref _confirmBeforeClose, value); }

    /// <summary>
    /// True once the user acknowledged the first-run notice that the default
    /// modifier-free hotkeys (F1–F7, F9, F10) are intercepted system-wide while
    /// the app runs. Persisted so the notice dialog only ever shows once.
    /// </summary>
    public bool HotkeyWarningAcknowledged { get => _hotkeyWarningAcknowledged; set => SetProperty(ref _hotkeyWarningAcknowledged, value); }

    /// <summary>
    /// Persisted "don't show the red risk warning again" choice for KEYBOARD
    /// motion control. Deliberately serialized — unlike the MotionKeyboardEnabled
    /// safety gate itself, which must be re-confirmed every restart: the user's
    /// informed consent to the warning text outlives the session, the dangerous
    /// enable state does not. A factory reset clears it so the warning returns.
    /// </summary>
    public bool MotionKeyboardWarningAcknowledged { get => _motionKeyboardWarningAcknowledged; set => SetProperty(ref _motionKeyboardWarningAcknowledged, value); }

    /// <summary>Same as <see cref="MotionKeyboardWarningAcknowledged"/>, for GAMEPAD motion control.</summary>
    public bool MotionGamepadWarningAcknowledged { get => _motionGamepadWarningAcknowledged; set => SetProperty(ref _motionGamepadWarningAcknowledged, value); }

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
