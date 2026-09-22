using System.Linq;
using MotionStabilizer.Models;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for the bulk hotkey operations behind the "clear all" and
/// "restore defaults" buttons on the hotkeys page.
/// </summary>
public class HotkeyConfigTests
{
    [Fact]
    public void Defaults_BindTheFunctionKeyRow()
    {
        var cfg = new HotkeyConfig();

        Assert.Equal("F1", cfg.ToggleOverlay.Key);
        Assert.Equal("F2", cfg.ToggleCrosshair.Key);
        Assert.Equal("F3", cfg.ToggleClock.Key);
        Assert.Equal("F4", cfg.CycleOverlayShape.Key);
        Assert.Equal("F5", cfg.CycleCrosshairShape.Key);
        Assert.Equal("F6", cfg.CycleDisplayMode.Key);
        Assert.Equal("F7", cfg.CycleOpacityMode.Key);
        Assert.Equal("F9", cfg.CycleOverlayColor.Key);
        Assert.Equal("F10", cfg.CycleCrosshairColor.Key);
    }

    [Fact]
    public void Defaults_LeaveSomeBindingsEmpty()
    {
        var cfg = new HotkeyConfig();

        // Unbound by design: these would collide with common game keys.
        Assert.False(cfg.CycleSplitScreen.IsSet);
        Assert.False(cfg.CycleAspectRatio.IsSet);
        Assert.False(cfg.CycleTargetMonitor.IsSet);
    }

    [Fact]
    public void ClearAll_UnbindsEveryHotkey()
    {
        var cfg = new HotkeyConfig();
        Assert.Contains(cfg.AllBindings, b => b.IsSet);

        cfg.ClearAll();

        Assert.All(cfg.AllBindings, b => Assert.False(b.IsSet));
        Assert.All(cfg.AllBindings, b => Assert.Equal(string.Empty, b.Key));
    }

    [Fact]
    public void ClearAll_AlsoDropsModifiers()
    {
        var cfg = new HotkeyConfig();
        cfg.ToggleOverlay.Ctrl = true;
        cfg.ToggleOverlay.Alt = true;
        cfg.ToggleOverlay.Shift = true;

        cfg.ClearAll();

        Assert.False(cfg.ToggleOverlay.Ctrl);
        Assert.False(cfg.ToggleOverlay.Alt);
        Assert.False(cfg.ToggleOverlay.Shift);
    }

    [Fact]
    public void AllBindings_ExposesEveryBinding()
    {
        var cfg = new HotkeyConfig();

        // Guards the bulk actions: a binding added to HotkeyConfig but forgotten
        // in AllBindings would silently survive "clear all" and duplicate checks.
        Assert.Equal(14, cfg.AllBindings.Count);
        Assert.Equal(cfg.AllBindings.Count, cfg.AllBindings.Select(b => b.Name).Distinct().Count());
    }
}
