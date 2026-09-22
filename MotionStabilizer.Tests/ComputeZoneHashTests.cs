using System.Collections.Generic;
using MotionStabilizer.Overlay;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for the motion-zone layout hash: it must react to geometry only.
/// Draw-time properties (zone opacity, IsLeftSide) must never invalidate the dot
/// field — including them makes dragging the opacity slider rebuild every dot and
/// reset the motion velocity, which the user sees as a jump.
/// </summary>
public class ComputeZoneHashTests
{
    private static MotionZone Zone(
        float opacity = 0.6f,
        bool isLeft = true,
        float x = 10f,
        float y = 20f,
        float w = 100f,
        float h = 500f)
        => new(x, y, w, h, isLeft, opacity, 0f, 1000f);

    private static int Hash(params MotionZone[] zones)
        => DirectCompositionMotionRenderer.ComputeZoneHash(new List<MotionZone>(zones));

    [Fact]
    public void OpacityChange_DoesNotInvalidateLayout()
        => Assert.Equal(Hash(Zone(opacity: 0.2f)), Hash(Zone(opacity: 0.9f)));

    [Fact]
    public void SideChange_DoesNotInvalidateLayout()
        => Assert.Equal(Hash(Zone(isLeft: true)), Hash(Zone(isLeft: false)));

    [Fact]
    public void GeometryChange_InvalidatesLayout()
    {
        int baseline = Hash(Zone());
        Assert.NotEqual(baseline, Hash(Zone(x: 11f)));
        Assert.NotEqual(baseline, Hash(Zone(y: 21f)));
        Assert.NotEqual(baseline, Hash(Zone(w: 101f)));
        Assert.NotEqual(baseline, Hash(Zone(h: 501f)));
    }

    [Fact]
    public void ZoneCountChange_InvalidatesLayout()
        => Assert.NotEqual(Hash(Zone()), Hash(Zone(), Zone(x: 500f)));
}
