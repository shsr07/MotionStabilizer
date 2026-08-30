using System.ComponentModel;
using MotionStabilizer.Models;

namespace MotionStabilizer.Services;

/// <summary>
/// Centralized, observable store for all application configuration state.
/// 
/// Replaces the former pattern of scattered <c>App.OverlayConfig</c>, <c>App.CrosshairConfig</c>,
/// etc. static properties that required manual <c>App.RefreshOverlay()</c> calls after every
/// modification. With <see cref="ConfigStore"/>, any property change on any config object
/// automatically fires <see cref="Changed"/>, which the application subscribes to for:
/// <list type="bullet">
///   <item>Re-rendering the overlay window</item>
///   <item>Scheduling a debounced auto-save</item>
/// </list>
/// 
/// Config objects implement <see cref="INotifyPropertyChanged"/> via <see cref="ObservableObject"/>,
/// so individual property-level changes are detected without any explicit notification calls.
/// When an entire config object is replaced (e.g. during profile load or factory reset),
/// the setter automatically unsubscribes from the old object and subscribes to the new one.
/// </summary>
public class ConfigStore
{
    private OverlayConfig _overlay = new();
    private CrosshairConfig _crosshair = new();
    private ClockConfig _clock = new();
    private AppConfig _app = new();

    /// <summary>Overlay configuration. Replacing this object re-subscribes change events.</summary>
    public OverlayConfig Overlay
    {
        get => _overlay;
        set
        {
            if (ReferenceEquals(_overlay, value)) return;
            _overlay.PropertyChanged -= OnConfigPropertyChanged;
            _overlay = value;
            _overlay.PropertyChanged += OnConfigPropertyChanged;
            Changed?.Invoke();
        }
    }

    /// <summary>Crosshair configuration. Replacing this object re-subscribes change events.</summary>
    public CrosshairConfig Crosshair
    {
        get => _crosshair;
        set
        {
            if (ReferenceEquals(_crosshair, value)) return;
            _crosshair.PropertyChanged -= OnConfigPropertyChanged;
            _crosshair = value;
            _crosshair.PropertyChanged += OnConfigPropertyChanged;
            Changed?.Invoke();
        }
    }

    /// <summary>Clock configuration. Replacing this object re-subscribes change events.</summary>
    public ClockConfig Clock
    {
        get => _clock;
        set
        {
            if (ReferenceEquals(_clock, value)) return;
            _clock.PropertyChanged -= OnConfigPropertyChanged;
            _clock = value;
            _clock.PropertyChanged += OnConfigPropertyChanged;
            Changed?.Invoke();
        }
    }

    /// <summary>Global application options. Replacing this object re-subscribes change events.</summary>
    public AppConfig App
    {
        get => _app;
        set
        {
            if (ReferenceEquals(_app, value)) return;
            _app.PropertyChanged -= OnConfigPropertyChanged;
            _app = value;
            _app.PropertyChanged += OnConfigPropertyChanged;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Hotkey bindings. Not observable — hotkey changes are explicitly handled by
    /// <see cref="HotkeyManager"/> and saved via <see cref="ConfigManager.SaveHotkeys"/>.
    /// </summary>
    public HotkeyConfig Hotkeys { get; set; } = new();

    /// <summary>
    /// Fired when any tracked config property changes or when a config object is replaced.
    /// Subscribers: overlay re-render, debounced auto-save.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Initialize all config objects and subscribe to their change events.
    /// </summary>
    public ConfigStore()
    {
        _overlay.PropertyChanged += OnConfigPropertyChanged;
        _crosshair.PropertyChanged += OnConfigPropertyChanged;
        _clock.PropertyChanged += OnConfigPropertyChanged;
        _app.PropertyChanged += OnConfigPropertyChanged;
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => Changed?.Invoke();

    /// <summary>
    /// Replace overlay, crosshair, and clock configs from a loaded profile.
    /// Triggers a single <see cref="Changed"/> event per replaced object.
    /// </summary>
    public void ApplyProfile(ProfileData profile)
    {
        Overlay = profile.Overlay;
        Crosshair = profile.Crosshair;
        Clock = profile.Clock;
    }

    /// <summary>
    /// Reset all configs to factory defaults by replacing with fresh instances.
    /// The hotkey-notice acknowledgement is carried over on purpose: the user
    /// has already been told once that the default hotkeys are captured
    /// system-wide — a factory reset of appearance settings must not resurface
    /// that dialog.
    /// </summary>
    public void ResetToDefaults()
    {
        App = new AppConfig { HotkeyWarningAcknowledged = _app.HotkeyWarningAcknowledged };
        Hotkeys = new HotkeyConfig();
        Overlay = new OverlayConfig();
        Crosshair = new CrosshairConfig();
        Clock = new ClockConfig();
    }
}
