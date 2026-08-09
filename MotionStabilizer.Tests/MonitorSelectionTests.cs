using MotionStabilizer.Services;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for target-monitor filtering and EDID extraction.
/// Uses the PnP DeviceID path (not the unstable GDI \\.\DISPLAY1 name).
/// </summary>
public class MonitorSelectionTests
{
    // PnP DeviceID paths — format: \\?\DISPLAY#VENDOR_PRODUCT#INSTANCE#UID#{GUID}
    private static readonly Win32Interop.MonitorInfo MonitorA =
        new(@"\\.\DISPLAY1", @"\\?\DISPLAY#DELF0F81#5&2e8a1c4a&0&UID256#{e6f07b5f-3a8c-4d6a-86b6-9a0e4e1b}",
            "DELL S2721DGF", 0, 0, 1920, 1080, 1.0);

    private static readonly Win32Interop.MonitorInfo MonitorB =
        new(@"\\.\DISPLAY2", @"\\?\DISPLAY#ACR05A3#6&1b2c3d4e&0&UID257#{e6f07b5f-3a8c-4d6a-86b6-9a0e4e1b}",
            "Acer Predator", 1920, 0, 2560, 1440, 1.25);

    private static readonly Win32Interop.MonitorInfo[] Monitors = [MonitorA, MonitorB];

    // ── Empty / null target → all monitors ──

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SelectTargetMonitors_EmptyTarget_ReturnsAll(string target)
    {
        var result = Win32Interop.SelectTargetMonitors(Monitors, target);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SelectTargetMonitors_NullTarget_ReturnsAll()
    {
        var result = Win32Interop.SelectTargetMonitors(Monitors, null!);
        Assert.Equal(2, result.Count);
    }

    // ── Exact PnP DeviceID match ──

    [Fact]
    public void SelectTargetMonitors_ExactDeviceIdMatch_ReturnsOnlyThatMonitor()
    {
        var result = Win32Interop.SelectTargetMonitors(Monitors, MonitorB.DeviceId);

        Assert.Single(result);
        Assert.Equal(MonitorB.DeviceId, result[0].DeviceId);
    }

    [Fact]
    public void SelectTargetMonitors_ExactDeviceIdMatch_IsCaseInsensitive()
    {
        var lower = MonitorA.DeviceId.ToLowerInvariant();
        var result = Win32Interop.SelectTargetMonitors(Monitors, lower);

        Assert.Single(result);
        Assert.Equal(MonitorA.DeviceId, result[0].DeviceId);
    }

    // ── EDID fallback (port change) ──

    [Fact]
    public void SelectTargetMonitors_EdidFallback_MatchesSameModelDifferentPort()
    {
        // Same EDID vendor+product as MonitorA, but different instance ID (port change)
        var portChangedId = @"\\?\DISPLAY#DELF0F81#7&9f8e7d6c&0&UID512#{e6f07b5f-3a8c-4d6a-86b6-9a0e4e1b}";
        var result = Win32Interop.SelectTargetMonitors(Monitors, portChangedId);

        Assert.Single(result);
        Assert.Equal(MonitorA.DeviceId, result[0].DeviceId);
    }

    [Fact]
    public void SelectTargetMonitors_EdidFallback_AmbiguousModels_ReturnsAll()
    {
        // Two monitors with the same EDID vendor+product → ambiguous → fallback to all
        var monA = new Win32Interop.MonitorInfo("d1", @"\\?\DISPLAY#DELF0F81#5&aaa&0&UID1#{}", "", 0, 0, 1920, 1080, 1.0);
        var monB = new Win32Interop.MonitorInfo("d2", @"\\?\DISPLAY#DELF0F81#6&bbb&0&UID2#{}", "", 1920, 0, 1920, 1080, 1.0);
        var monitors = new[] { monA, monB };

        var target = @"\\?\DISPLAY#DELF0F81#7&ccc&0&UID3#{}";

        var result = Win32Interop.SelectTargetMonitors(monitors, target);
        Assert.Equal(2, result.Count);
    }

    // ── Unknown device → fallback to all ──

    [Fact]
    public void SelectTargetMonitors_UnknownDeviceId_FallsBackToAll()
    {
        var unknown = @"\\?\DISPLAY#UNKNOWN9#5&xxx&0&UID999#{}";
        var result = Win32Interop.SelectTargetMonitors(Monitors, unknown);

        Assert.Equal(2, result.Count);
    }

    // ── ExtractEdidPart ──

    [Theory]
    [InlineData(@"\\?\DISPLAY#DELF0F81#5&2e8a1c4a&0&UID256#{guid}", "DELF0F81")]
    [InlineData(@"\\?\DISPLAY#ACR05A3#6&1b2c3d4e&0&UID257#{guid}", "ACR05A3")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ExtractEdidPart_ReturnsCorrectSegment(string? deviceId, string expected)
    {
        Assert.Equal(expected, Win32Interop.ExtractEdidPart(deviceId));
    }
}
