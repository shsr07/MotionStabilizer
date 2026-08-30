namespace MotionStabilizer.Models;

/// <summary>
/// Canonical hotkey binding names — the single source of truth for the strings
/// linking HotkeyConfig (registration) and OsdTextBuilder (OSD mapping).
/// Centralized so a rename fails compilation instead of silently dropping the
/// OSD feedback at runtime.
/// </summary>
public static class HotkeyNames
{
    public const string ToggleOverlay = "ToggleOverlay";
    public const string ToggleCrosshair = "ToggleCrosshair";
    public const string ToggleClock = "ToggleClock";
    public const string CycleDisplayMode = "CycleDisplayMode";
    public const string CycleSplitScreen = "CycleSplitScreen";
    public const string CycleOverlayShape = "CycleOverlayShape";
    public const string CycleCrosshairShape = "CycleCrosshairShape";
    public const string CycleAspectRatio = "CycleAspectRatio";
    public const string CycleOpacityMode = "CycleOpacityMode";
    public const string CycleTargetMonitor = "CycleTargetMonitor";
    public const string CycleOverlayColor = "CycleOverlayColor";
    public const string CycleCrosshairColor = "CycleCrosshairColor";
}

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
    public HotkeyBinding ToggleOverlay { get; set; } = new() { Name = HotkeyNames.ToggleOverlay, Key = "F1" };
    public HotkeyBinding ToggleCrosshair { get; set; } = new() { Name = HotkeyNames.ToggleCrosshair, Key = "F2" };
    public HotkeyBinding ToggleClock { get; set; } = new() { Name = HotkeyNames.ToggleClock, Key = "F3" };
    public HotkeyBinding CycleDisplayMode { get; set; } = new() { Name = HotkeyNames.CycleDisplayMode, Key = "F6" };
    public HotkeyBinding CycleSplitScreen { get; set; } = new() { Name = HotkeyNames.CycleSplitScreen };
    public HotkeyBinding CycleOverlayShape { get; set; } = new() { Name = HotkeyNames.CycleOverlayShape, Key = "F4" };
    public HotkeyBinding CycleCrosshairShape { get; set; } = new() { Name = HotkeyNames.CycleCrosshairShape, Key = "F5" };
    public HotkeyBinding CycleAspectRatio { get; set; } = new() { Name = HotkeyNames.CycleAspectRatio };
    public HotkeyBinding CycleOpacityMode { get; set; } = new() { Name = HotkeyNames.CycleOpacityMode, Key = "F7" };
    public HotkeyBinding CycleTargetMonitor { get; set; } = new() { Name = HotkeyNames.CycleTargetMonitor };

    // 2 color cycle hotkeys: overlay / crosshair each cycle Red → Green → Blue → Custom
    public HotkeyBinding CycleOverlayColor { get; set; } = new() { Name = HotkeyNames.CycleOverlayColor, Key = "F9" };
    public HotkeyBinding CycleCrosshairColor { get; set; } = new() { Name = HotkeyNames.CycleCrosshairColor, Key = "F10" };

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
