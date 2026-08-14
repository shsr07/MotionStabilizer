namespace MotionStabilizer.Models;

/// <summary>
/// Represents a single hotkey binding.
/// Hotkeys are NOT saved to/loaded from config profiles (per spec).
/// </summary>
public class HotkeyBinding
{
    /// <summary>Display name / function key</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The key code (e.g. "F1", "R", "G"). Empty means unbound.</summary>
    public string Key { get; set; } = string.Empty;

    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }

    public bool IsSet => !string.IsNullOrWhiteSpace(Key);

    public string DisplayString
    {
        get
        {
            if (!IsSet) return "—";
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(Key);
            return string.Join(" + ", parts);
        }
    }

    public HotkeyBinding Clone() => new()
    {
        Name = Name,
        Key = Key,
        Ctrl = Ctrl,
        Alt = Alt,
        Shift = Shift
    };
}

/// <summary>
/// All hotkey bindings for the application.
/// </summary>
public class HotkeyConfig
{
    // 8 display mode toggle hotkeys
    public HotkeyBinding ToggleOverlay { get; set; } = new() { Name = "ToggleOverlay", Key = "F1" };
    public HotkeyBinding ToggleCrosshair { get; set; } = new() { Name = "ToggleCrosshair", Key = "F2" };
    public HotkeyBinding ToggleClock { get; set; } = new() { Name = "ToggleClock", Key = "F3" };
    public HotkeyBinding CycleDisplayMode { get; set; } = new() { Name = "CycleDisplayMode", Key = "F6" };
    public HotkeyBinding CycleSplitScreen { get; set; } = new() { Name = "CycleSplitScreen" };
    public HotkeyBinding CycleOverlayShape { get; set; } = new() { Name = "CycleOverlayShape", Key = "F4" };
    public HotkeyBinding CycleCrosshairShape { get; set; } = new() { Name = "CycleCrosshairShape", Key = "F5" };
    public HotkeyBinding CycleAspectRatio { get; set; } = new() { Name = "CycleAspectRatio" };
    public HotkeyBinding CycleOpacityMode { get; set; } = new() { Name = "CycleOpacityMode", Key = "F7" };
    public HotkeyBinding CycleTargetMonitor { get; set; } = new() { Name = "CycleTargetMonitor" };

    // 2 color cycle hotkeys: overlay / crosshair each cycle Red → Green → Blue → Custom
    public HotkeyBinding CycleOverlayColor { get; set; } = new() { Name = "CycleOverlayColor", Key = "F9" };
    public HotkeyBinding CycleCrosshairColor { get; set; } = new() { Name = "CycleCrosshairColor", Key = "F10" };

    public List<HotkeyBinding> AllBindings => new()
    {
        ToggleOverlay,
        ToggleCrosshair,
        ToggleClock,
        CycleDisplayMode,
        CycleSplitScreen,
        CycleOverlayShape,
        CycleCrosshairShape,
        CycleAspectRatio,
        CycleOpacityMode,
        CycleTargetMonitor,
        CycleOverlayColor,
        CycleCrosshairColor
    };
}
