using MotionStabilizer.Services;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for target-monitor filtering used by the settings page and overlay.
/// </summary>
public class MonitorSelectionTests
{
    private static readonly Win32Interop.MonitorInfo MonitorA =
        new(@"\\.\DISPLAY1", 0, 0, 1920, 1080, 1.0);

    private static readonly Win32Interop.MonitorInfo MonitorB =
        new(@"\\.\DISPLAY2", 1920, 0, 2560, 1440, 1.25);

    private static readonly Win32Interop.MonitorInfo[] Monitors = [MonitorA, MonitorB];

    [Fact]
    public void SelectTargetMonitors_EmptyTarget_ReturnsAll()
    {
        var result = Win32Interop.SelectTargetMonitors(Monitors, "");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SelectTargetMonitors_MatchingDevice_ReturnsOnlyThatMonitor()
    {
        var result = Win32Interop.SelectTargetMonitors(Monitors, @"\\.\DISPLAY2");

        Assert.Single(result);
        Assert.Equal(@"\\.\DISPLAY2", result[0].DeviceName);
    }

    [Fact]
    public void SelectTargetMonitors_MatchingDevice_IsCaseInsensitive()
    {
        var result = Win32Interop.SelectTargetMonitors(Monitors, @"\\.\display1");

        Assert.Single(result);
        Assert.Equal(@"\\.\DISPLAY1", result[0].DeviceName);
    }

    [Fact]
    public void SelectTargetMonitors_UnknownDevice_FallsBackToAll()
    {
        var result = Win32Interop.SelectTargetMonitors(Monitors, @"\\.\DISPLAY9");

        Assert.Equal(2, result.Count);
    }
}
