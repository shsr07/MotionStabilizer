using MotionStabilizer.Overlay;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for the 1×1 compact-surface decision: the WPF surface may shrink
/// only when native motion dots are the sole visible content and no OSD is up.
/// </summary>
public class CompactSurfaceTests
{
    private static bool Compact(
        bool osdVisible = false,
        bool crosshair = false,
        bool clock = false,
        bool overlay = true,
        bool motionDots = true,
        bool nativeReady = true)
        => OverlayWindow.ShouldCompactSurface(overlay, motionDots, nativeReady, crosshair, clock, osdVisible);

    [Fact]
    public void DotsOnly_IsCompact() => Assert.True(Compact());

    [Fact]
    public void OsdVisible_BlocksCompact() => Assert.False(Compact(osdVisible: true));

    [Fact]
    public void CrosshairVisible_BlocksCompact() => Assert.False(Compact(crosshair: true));

    [Fact]
    public void ClockVisible_BlocksCompact() => Assert.False(Compact(clock: true));

    [Fact]
    public void OverlayHidden_NotCompact() => Assert.False(Compact(overlay: false));

    [Fact]
    public void NonDotsShape_NotCompact() => Assert.False(Compact(motionDots: false));

    [Fact]
    public void NativeRendererNotReady_NotCompact() => Assert.False(Compact(nativeReady: false));
}
