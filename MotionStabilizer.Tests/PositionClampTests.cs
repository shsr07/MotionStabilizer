using MotionStabilizer.Overlay;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for the resolution-change safety net that clamps clock/crosshair
/// positions back on-screen instead of resetting them — games entering/exiting
/// fullscreen must not wipe user-placed positions.
/// </summary>
public class PositionClampTests
{
    // ── Clock: top-left corner clamped into [0, max] ────────────────────

    [Theory]
    [InlineData(100, 500.0, 100)]  // in range → unchanged
    [InlineData(0, 500.0, 0)]      // at edge → unchanged
    [InlineData(-20, 500.0, 0)]    // off left/top → clamped to 0
    [InlineData(600, 500.0, 500)]  // off right/bottom → clamped to max
    [InlineData(600, -5.0, 0)]     // degenerate (negative) max → 0
    public void ClampIntoRange_MapsOutOfBoundsToEdge(int value, double max, int expected)
        => Assert.Equal(expected, OverlayWindow.ClampIntoRange(value, max));

    // ── Crosshair: center offset clamped into ±maxAbs ───────────────────

    [Theory]
    [InlineData(0, 960.0, 0)]
    [InlineData(300, 960.0, 300)]    // in range → unchanged
    [InlineData(-1200, 960.0, -960)] // off left → clamped to -bound
    [InlineData(1200, 960.0, 960)]   // off right → clamped to +bound
    [InlineData(5, 0.0, 0)]          // degenerate bound → 0
    public void ClampOffset_MapsOutOfBoundsToBound(int value, double maxAbs, int expected)
        => Assert.Equal(expected, OverlayWindow.ClampOffset(value, maxAbs));
}
