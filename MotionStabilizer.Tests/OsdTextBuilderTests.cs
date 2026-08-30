using MotionStabilizer.Models;
using MotionStabilizer.Services;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for the hotkey → OSD text mapping. Uses an identity resource
/// resolver so assertions verify the exact resource keys and "label: value"
/// formatting chosen for each hotkey.
/// </summary>
public class OsdTextBuilderTests
{
    private static string Res(string key) => key; // identity resolver

    private const string MonitorLabel = "Monitor 2 (2560x1440)";

    private static string? Build(
        string name,
        Action<OverlayConfig>? overlay = null,
        Action<CrosshairConfig>? crosshair = null,
        Action<ClockConfig>? clock = null,
        bool crosshairPage = false,
        string? monitorLabel = null)
    {
        var oc = new OverlayConfig();
        var cc = new CrosshairConfig();
        var ck = new ClockConfig();
        overlay?.Invoke(oc);
        crosshair?.Invoke(cc);
        clock?.Invoke(ck);
        return OsdTextBuilder.Build(name, oc, cc, ck, crosshairPage, Res, monitorLabel ?? MonitorLabel);
    }

    // ── Toggles ─────────────────────────────────────────────────────────

    [Fact]
    public void ToggleOverlay_ShowsFeatureAndState()
    {
        Assert.Equal("Nav_Overlay: Enabled", Build(HotkeyNames.ToggleOverlay, o => o.IsVisible = true));
        Assert.Equal("Nav_Overlay: Disabled", Build(HotkeyNames.ToggleOverlay, o => o.IsVisible = false));
    }

    [Fact]
    public void ToggleCrosshair_ReadsCrosshairConfigOnly()
    {
        Assert.Equal("Nav_Crosshair: Enabled",
            Build(HotkeyNames.ToggleCrosshair, crosshair: c => c.IsVisible = true, overlay: o => o.IsVisible = false));
        Assert.Equal("Nav_Crosshair: Disabled", Build(HotkeyNames.ToggleCrosshair));
    }

    [Fact]
    public void ToggleClock_ReadsClockConfigOnly()
    {
        Assert.Equal("Nav_Clock: Enabled", Build(HotkeyNames.ToggleClock, clock: c => c.IsVisible = true));
        Assert.Equal("Nav_Clock: Disabled", Build(HotkeyNames.ToggleClock));
    }

    // ── Display mode / split / aspect / opacity ─────────────────────────

    [Fact]
    public void CycleDisplayMode_MapsBothModes()
    {
        Assert.Equal("DisplayMode: Mode_Window", Build(HotkeyNames.CycleDisplayMode, o => o.Mode = DisplayMode.Window));
        Assert.Equal("DisplayMode: Mode_Stretch", Build(HotkeyNames.CycleDisplayMode, o => o.Mode = DisplayMode.Stretch));
    }

    [Fact]
    public void CycleSplitScreen_UsesOverlayConfigOnOverlayPage()
    {
        Assert.Equal("SplitScreen: Split_Vertical",
            Build(HotkeyNames.CycleSplitScreen, o => o.Split = SplitScreen.Vertical, crosshair: c => c.Split = SplitScreen.Horizontal));
    }

    [Fact]
    public void CycleSplitScreen_UsesCrosshairConfigOnCrosshairPage()
    {
        Assert.Equal("SplitScreen: Split_Horizontal",
            Build(HotkeyNames.CycleSplitScreen, o => o.Split = SplitScreen.Vertical, crosshair: c => c.Split = SplitScreen.Horizontal,
                crosshairPage: true));
        Assert.Equal("SplitScreen: Split_None", Build(HotkeyNames.CycleSplitScreen, crosshairPage: true));
    }

    [Theory]
    [InlineData(AspectRatio.Ratio16x9, "16:9")]
    [InlineData(AspectRatio.Ratio21x9, "21:9")]
    [InlineData(AspectRatio.Ratio4x3, "4:3")]
    [InlineData(AspectRatio.Ratio5x4, "5:4")]
    public void CycleAspectRatio_MapsAllValues(AspectRatio aspect, string expected)
    {
        Assert.Equal($"AspectRatio: {expected}", Build(HotkeyNames.CycleAspectRatio, o => o.AspectRatio = aspect));
    }

    [Fact]
    public void CycleAspectRatio_RespectsCrosshairPageFlag()
    {
        Assert.Equal("AspectRatio: 21:9",
            Build(HotkeyNames.CycleAspectRatio, o => o.AspectRatio = AspectRatio.Ratio16x9,
                crosshair: c => c.AspectRatio = AspectRatio.Ratio21x9, crosshairPage: true));
    }

    [Fact]
    public void CycleOpacityMode_MapsBothModes()
    {
        Assert.Equal("OpacityMode: OpacityMode_Uniform", Build(HotkeyNames.CycleOpacityMode, o => o.OpacityMode = EdgeOpacityMode.Uniform));
        Assert.Equal("OpacityMode: OpacityMode_PerEdge", Build(HotkeyNames.CycleOpacityMode, o => o.OpacityMode = EdgeOpacityMode.PerEdge));
    }

    // ── Shapes / colors ─────────────────────────────────────────────────

    [Theory]
    [InlineData(OverlayShape.Pole, "Shape_Pole")]
    [InlineData(OverlayShape.Box, "Shape_Box")]
    [InlineData(OverlayShape.Dome, "Shape_Dome")]
    [InlineData(OverlayShape.Flag, "Shape_Flag")]
    [InlineData(OverlayShape.MotionDots, "Shape_MotionDots")]
    public void CycleOverlayShape_MapsAllShapes(OverlayShape shape, string expected)
    {
        Assert.Equal($"Shape: {expected}", Build(HotkeyNames.CycleOverlayShape, o => o.Shape = shape));
    }

    [Theory]
    [InlineData(CrosshairShape.Circle, "Shape_Circle")]
    [InlineData(CrosshairShape.Cross, "Shape_Cross")]
    [InlineData(CrosshairShape.Diamond, "Shape_Diamond")]
    public void CycleCrosshairShape_MapsAllShapes(CrosshairShape shape, string expected)
    {
        Assert.Equal($"Shape: {expected}", Build(HotkeyNames.CycleCrosshairShape, crosshair: c => c.Shape = shape));
    }

    [Theory]
    [InlineData(ColorPreset.Red, "Color_Red")]
    [InlineData(ColorPreset.Green, "Color_Green")]
    [InlineData(ColorPreset.Blue, "Color_Blue")]
    [InlineData(ColorPreset.Custom, "Color_Custom")]
    public void CycleColor_UsesDistinctLabelsForOverlayAndCrosshair(ColorPreset preset, string expected)
    {
        Assert.Equal($"Osd_OverlayColor: {expected}", Build(HotkeyNames.CycleOverlayColor, o => o.ColorPreset = preset));
        Assert.Equal($"Osd_CrosshairColor: {expected}", Build(HotkeyNames.CycleCrosshairColor, crosshair: c => c.ColorPreset = preset));
    }

    // ── Target monitor / unknown names ──────────────────────────────────

    [Fact]
    public void CycleTargetMonitor_PassesLabelThrough()
    {
        Assert.Equal(MonitorLabel, Build(HotkeyNames.CycleTargetMonitor));
    }

    [Theory]
    [InlineData("")]
    [InlineData("SomeUnknownHotkey")]
    public void UnknownName_ReturnsNull(string name)
    {
        Assert.Null(Build(name));
    }
}
