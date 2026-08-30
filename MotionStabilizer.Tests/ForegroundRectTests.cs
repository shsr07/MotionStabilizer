using MotionStabilizer.Overlay;
using MotionStabilizer.Services;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for the Window-mode foreground-rect change detection used by
/// the overlay's low-frequency tracking timer (RECT has no equality members,
/// so the comparison is field-wise).
/// </summary>
public class ForegroundRectTests
{
    private static Win32Interop.RECT Rect(int left, int top, int right, int bottom) => new()
    {
        Left = left, Top = top, Right = right, Bottom = bottom
    };

    [Fact]
    public void EqualRects_AreEqual()
    {
        var a = Rect(10, 20, 110, 220);
        var b = Rect(10, 20, 110, 220);

        Assert.True(OverlayWindow.ForegroundRectEquals(a, b));
    }

    [Fact]
    public void MovedOrResizedRects_AreNotEqual()
    {
        var a = Rect(10, 20, 110, 220);

        Assert.False(OverlayWindow.ForegroundRectEquals(a, Rect(11, 20, 110, 220))); // moved X
        Assert.False(OverlayWindow.ForegroundRectEquals(a, Rect(10, 21, 110, 220))); // moved Y
        Assert.False(OverlayWindow.ForegroundRectEquals(a, Rect(10, 20, 120, 220))); // resized W
        Assert.False(OverlayWindow.ForegroundRectEquals(a, Rect(10, 20, 110, 230))); // resized H
    }

    [Fact]
    public void NullAndValue_AreNotEqual()
    {
        Assert.False(OverlayWindow.ForegroundRectEquals(Rect(10, 20, 110, 220), null));
        Assert.False(OverlayWindow.ForegroundRectEquals(null, Rect(10, 20, 110, 220)));
    }

    [Fact]
    public void BothNull_AreEqual()
    {
        Assert.True(OverlayWindow.ForegroundRectEquals(null, null));
    }
}
