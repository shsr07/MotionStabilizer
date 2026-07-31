using MotionStabilizer.Models;
using MotionStabilizer.Overlay;
using MotionStabilizer.Services;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for <see cref="OverlayWindow.ComputeZonesForMonitor"/> — the
/// extracted, Win32-free zone geometry logic. Tests verify correct zone
/// count, positioning, split behavior, edge visibility, and opacity.
/// </summary>
public class ComputeMotionZonesTests
{
    // Standard test monitor: 1920×1080 at (0,0), 100% DPI
    private static readonly Win32Interop.MonitorInfo TestMonitor =
        new(0, 0, 1920, 1080, 1.0);

    private const int VsX = 0;
    private const int VsY = 0;

    // ── Basic zone count ──

    [Fact]
    public void NoSplit_BothEdgesVisible_ReturnsTwoZones()
    {
        var cfg = new OverlayConfig
        {
            IsVisible = true,
            Shape = OverlayShape.MotionDots,
            Split = SplitScreen.None,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Equal(2, zones.Count);
    }

    [Fact]
    public void NoSplit_OnlyLeftVisible_ReturnsOneZone()
    {
        var cfg = new OverlayConfig
        {
            EdgeLeftVisible = true,
            EdgeRightVisible = false
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Single(zones);
        Assert.True(zones[0].IsLeftSide);
    }

    [Fact]
    public void NoSplit_OnlyRightVisible_ReturnsOneZone()
    {
        var cfg = new OverlayConfig
        {
            EdgeLeftVisible = false,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Single(zones);
        Assert.False(zones[0].IsLeftSide);
    }

    [Fact]
    public void NoEdgesVisible_ReturnsEmpty()
    {
        var cfg = new OverlayConfig
        {
            EdgeLeftVisible = false,
            EdgeRightVisible = false
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Empty(zones);
    }

    // ── Split behavior ──

    [Fact]
    public void VerticalSplit_BothEdges_ReturnsFourZones()
    {
        var cfg = new OverlayConfig
        {
            Split = SplitScreen.Vertical,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Equal(4, zones.Count);
    }

    [Fact]
    public void HorizontalSplit_BothEdges_ReturnsFourZones()
    {
        var cfg = new OverlayConfig
        {
            Split = SplitScreen.Horizontal,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Equal(4, zones.Count);
    }

    [Fact]
    public void VerticalSplit_OnlyLeft_ReturnsTwoZones()
    {
        var cfg = new OverlayConfig
        {
            Split = SplitScreen.Vertical,
            EdgeLeftVisible = true,
            EdgeRightVisible = false
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Equal(2, zones.Count);
        Assert.All(zones, z => Assert.True(z.IsLeftSide));
    }

    // ── Zone positioning ──

    [Fact]
    public void LeftZone_IsOnLeftSide_OfMonitor()
    {
        var cfg = new OverlayConfig
        {
            EdgeLeftVisible = true,
            EdgeRightVisible = false,
            Length = OffsetLevel.Plus0
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Single(zones);
        // Left zone X should be near 0 (only offset by lengthPx which is 0 for Plus0)
        Assert.True(zones[0].X < zones[0].Width);
    }

    [Fact]
    public void RightZone_IsOnRightSide_OfMonitor()
    {
        var cfg = new OverlayConfig
        {
            EdgeLeftVisible = false,
            EdgeRightVisible = true,
            Length = OffsetLevel.Plus0
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Single(zones);
        // Right zone should be near the right edge of the monitor
        float zoneW = 1920 * 0.12f;
        Assert.True(zones[0].X > 1920 - zoneW * 2);
    }

    [Fact]
    public void ZoneWidth_Is12Percent_OfMonitorWidth()
    {
        var cfg = new OverlayConfig
        {
            EdgeLeftVisible = true,
            EdgeRightVisible = false
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Single(zones);
        Assert.Equal(1920 * 0.12f, zones[0].Width);
    }

    // ── Opacity ──

    [Fact]
    public void UniformOpacity_UsesGlobalOpacity()
    {
        var cfg = new OverlayConfig
        {
            OpacityMode = EdgeOpacityMode.Uniform,
            Opacity = 75,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.All(zones, z => Assert.Equal(0.75f, z.Opacity));
    }

    [Fact]
    public void PerEdgeOpacity_UsesIndividualOpacity()
    {
        var cfg = new OverlayConfig
        {
            OpacityMode = EdgeOpacityMode.PerEdge,
            Opacity = 100,
            EdgeLeftOpacity = 30,
            EdgeRightOpacity = 60,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Equal(2, zones.Count);
        var leftZone = zones.First(z => z.IsLeftSide);
        var rightZone = zones.First(z => !z.IsLeftSide);
        Assert.Equal(0.30f, leftZone.Opacity);
        Assert.Equal(0.60f, rightZone.Opacity);
    }

    // ── Length offset ──

    [Fact]
    public void LengthOffset_ShiftsLeftZone_Rightward()
    {
        var cfgNoOffset = new OverlayConfig
        {
            EdgeLeftVisible = true,
            EdgeRightVisible = false,
            Length = OffsetLevel.Plus0
        };
        var cfgWithOffset = new OverlayConfig
        {
            EdgeLeftVisible = true,
            EdgeRightVisible = false,
            Length = OffsetLevel.Plus6
        };

        var zonesNoOffset = OverlayWindow.ComputeZonesForMonitor(cfgNoOffset, TestMonitor, VsX, VsY, null);
        var zonesWithOffset = OverlayWindow.ComputeZonesForMonitor(cfgWithOffset, TestMonitor, VsX, VsY, null);

        Assert.True(zonesWithOffset[0].X > zonesNoOffset[0].X,
            "Left zone with length offset should be shifted rightward");
    }

    // ── Window mode ──

    [Fact]
    public void WindowMode_ForegroundOnDifferentMonitor_ReturnsEmpty()
    {
        var cfg = new OverlayConfig
        {
            Mode = DisplayMode.Window,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };
        // Foreground window is at (5000, 5000) — not on our test monitor at (0,0,1920,1080)
        var fwRect = new Win32Interop.RECT { Left = 5000, Top = 5000, Right = 6000, Bottom = 6000 };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, fwRect);

        Assert.Empty(zones);
    }

    [Fact]
    public void WindowMode_ForegroundOnThisMonitor_ReturnsZones()
    {
        var cfg = new OverlayConfig
        {
            Mode = DisplayMode.Window,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };
        // Foreground window at (100, 100, 800, 600) — on our test monitor
        var fwRect = new Win32Interop.RECT { Left = 100, Top = 100, Right = 800, Bottom = 600 };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, fwRect);

        Assert.Equal(2, zones.Count);
        // Zones should be sized to the foreground window, not the full monitor
        Assert.True(zones[0].Height < 1080, "Zone height should match foreground window height");
    }

    [Fact]
    public void WindowMode_NullForeground_FallsBackToFullScreen()
    {
        var cfg = new OverlayConfig
        {
            Mode = DisplayMode.Window,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Equal(2, zones.Count);
        // Should use full monitor height since no foreground window
        Assert.Equal(1080f, zones[0].Height);
    }

    // ── Stretch mode ──

    [Fact]
    public void StretchMode_IgnoresForegroundWindow()
    {
        var cfg = new OverlayConfig
        {
            Mode = DisplayMode.Stretch,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };
        // Even with a foreground window rect, Stretch mode should use full monitor
        var fwRect = new Win32Interop.RECT { Left = 100, Top = 100, Right = 800, Bottom = 600 };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, fwRect);

        Assert.Equal(2, zones.Count);
        Assert.Equal(1080f, zones[0].Height);
    }

    // ── Parallax area bounds ──

    [Fact]
    public void NoSplit_AreaBounds_MatchMonitor()
    {
        var cfg = new OverlayConfig
        {
            AspectRatio = AspectRatio.Ratio16x9, // Full screen
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Equal(2, zones.Count);
        var leftZone = zones.First(z => z.IsLeftSide);
        // AreaLeftX should be 0 (left edge of monitor), AreaRightX should be 1920
        Assert.Equal(0f, leftZone.AreaLeftX);
        Assert.Equal(1920f, leftZone.AreaRightX);
    }

    [Fact]
    public void VerticalSplit_EachHalfHasOwnAreaBounds()
    {
        var cfg = new OverlayConfig
        {
            AspectRatio = AspectRatio.Ratio16x9,
            Split = SplitScreen.Vertical,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, TestMonitor, VsX, VsY, null);

        Assert.Equal(4, zones.Count);
        // First two zones: left half (0..960), second two: right half (960..1920)
        var firstLeft = zones[0];
        Assert.Equal(0f, firstLeft.AreaLeftX);
        Assert.Equal(960f, firstLeft.AreaRightX);
    }

    // ── DPI scale ──

    [Fact]
    public void DpiScale150_ZoneWidthScalesWithMonitorWidth()
    {
        // Monitor at 150% DPI: physical 2880×1620, logical 1920×1080
        var mon150 = new Win32Interop.MonitorInfo(0, 0, 2880, 1620, 1.5);
        var cfg = new OverlayConfig
        {
            AspectRatio = AspectRatio.Ratio16x9,
            EdgeLeftVisible = true,
            EdgeRightVisible = false
        };

        var zones = OverlayWindow.ComputeZonesForMonitor(cfg, mon150, VsX, VsY, null);

        Assert.Single(zones);
        // Zone width = physical width * 0.12
        Assert.Equal(2880 * 0.12f, zones[0].Width);
    }
}
