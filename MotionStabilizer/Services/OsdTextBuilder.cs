using MotionStabilizer.Models;

namespace MotionStabilizer.Services;

/// <summary>
/// Maps a hotkey binding name to a short localized OSD message describing the
/// NEW config state — hotkey callbacks run before the OSD is composed, so the
/// config properties already reflect the change when this is called.
/// Pure and unit-testable: resource lookup and monitor labeling are injected.
/// </summary>
internal static class OsdTextBuilder
{
    /// <param name="name">Hotkey binding name (HotkeyBinding.Name).</param>
    /// <param name="overlay">Post-change overlay config.</param>
    /// <param name="crosshair">Post-change crosshair config.</param>
    /// <param name="clock">Post-change clock config.</param>
    /// <param name="crosshairPageActive">True when the settings window's crosshair
    /// page is open — the split/aspect hotkeys act on the crosshair config then.</param>
    /// <param name="res">Localized resource lookup by key.</param>
    /// <param name="targetMonitorLabel">Pre-computed localized label of the target
    /// monitor ("All monitors" / "Monitor 2 (2560x1440)") — needs Win32 calls, so
    /// it is resolved by the caller.</param>
    /// <returns>OSD text, or null when the binding name is unknown.</returns>
    public static string? Build(
        string name,
        OverlayConfig overlay,
        CrosshairConfig crosshair,
        ClockConfig clock,
        bool crosshairPageActive,
        Func<string, string> res,
        string targetMonitorLabel)
    {
        var split = crosshairPageActive ? crosshair.Split : overlay.Split;
        var aspect = crosshairPageActive ? crosshair.AspectRatio : overlay.AspectRatio;

        return name switch
        {
            HotkeyNames.ToggleOverlay => $"{res("Nav_Overlay")}: {(overlay.IsVisible ? res("Enabled") : res("Disabled"))}",
            HotkeyNames.ToggleCrosshair => $"{res("Nav_Crosshair")}: {(crosshair.IsVisible ? res("Enabled") : res("Disabled"))}",
            HotkeyNames.ToggleClock => $"{res("Nav_Clock")}: {(clock.IsVisible ? res("Enabled") : res("Disabled"))}",
            HotkeyNames.CycleDisplayMode => $"{res("DisplayMode")}: {(overlay.Mode == DisplayMode.Window ? res("Mode_Window") : res("Mode_Stretch"))}",
            HotkeyNames.CycleSplitScreen => $"{res("SplitScreen")}: {split switch
            {
                SplitScreen.Vertical => res("Split_Vertical"),
                SplitScreen.Horizontal => res("Split_Horizontal"),
                _ => res("Split_None")
            }}",
            HotkeyNames.CycleOverlayShape => $"{res("Shape")}: {overlay.Shape switch
            {
                OverlayShape.Box => res("Shape_Box"),
                OverlayShape.Dome => res("Shape_Dome"),
                OverlayShape.Flag => res("Shape_Flag"),
                OverlayShape.MotionDots => res("Shape_MotionDots"),
                _ => res("Shape_Pole")
            }}",
            HotkeyNames.CycleCrosshairShape => $"{res("Shape")}: {crosshair.Shape switch
            {
                CrosshairShape.Cross => res("Shape_Cross"),
                CrosshairShape.Diamond => res("Shape_Diamond"),
                _ => res("Shape_Circle")
            }}",
            // Aspect ratio values are language-neutral literals, matching the
            // OverlayPage ComboBox items ("16:9" … "5:4").
            HotkeyNames.CycleAspectRatio => $"{res("AspectRatio")}: {aspect switch
            {
                AspectRatio.Ratio21x9 => "21:9",
                AspectRatio.Ratio4x3 => "4:3",
                AspectRatio.Ratio5x4 => "5:4",
                _ => "16:9"
            }}",
            HotkeyNames.CycleOpacityMode => $"{res("OpacityMode")}: {(overlay.OpacityMode == EdgeOpacityMode.Uniform ? res("OpacityMode_Uniform") : res("OpacityMode_PerEdge"))}",
            HotkeyNames.CycleOverlayColor => $"{res("Osd_OverlayColor")}: {ColorLabel(overlay.ColorPreset, res)}",
            HotkeyNames.CycleCrosshairColor => $"{res("Osd_CrosshairColor")}: {ColorLabel(crosshair.ColorPreset, res)}",
            HotkeyNames.CycleTargetMonitor => targetMonitorLabel,
            _ => null
        };
    }

    private static string ColorLabel(ColorPreset preset, Func<string, string> res) => preset switch
    {
        ColorPreset.Green => res("Color_Green"),
        ColorPreset.Blue => res("Color_Blue"),
        ColorPreset.Custom => res("Color_Custom"),
        _ => res("Color_Red")
    };
}
