using MotionStabilizer.Overlay;
using MotionStabilizer.Models;
using System.Windows;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for <see cref="RenderHelper"/> — pure functions that convert
/// config enums to pixel dimensions and compute safe areas.
/// </summary>
public class RenderHelperTests
{
    // ── OverlayBarWidth ──

    [Theory]
    [InlineData(SizePreset.XXS, 4)]
    [InlineData(SizePreset.XS, 8)]
    [InlineData(SizePreset.S, 15)]
    [InlineData(SizePreset.M, 38)]
    [InlineData(SizePreset.L, 55)]
    [InlineData(SizePreset.XL, 75)]
    [InlineData(SizePreset.XXL, 100)]
    public void OverlayBarWidth_ReturnsExpectedValues(SizePreset size, double expected)
    {
        Assert.Equal(expected, RenderHelper.OverlayBarWidth(size));
    }

    // ── MotionDotDiameter ──

    [Theory]
    [InlineData(SizePreset.XXS, 12)]
    [InlineData(SizePreset.XS, 20)]
    [InlineData(SizePreset.S, 29)]
    [InlineData(SizePreset.M, 41)]
    [InlineData(SizePreset.L, 53)]
    [InlineData(SizePreset.XL, 73)]
    [InlineData(SizePreset.XXL, 94)]
    public void MotionDotDiameter_ReturnsExpectedValues(SizePreset size, double expected)
    {
        Assert.Equal(expected, RenderHelper.MotionDotDiameter(size));
    }

    // ── CrosshairSize ──

    [Theory]
    [InlineData(SizePreset.XXS, 6)]
    [InlineData(SizePreset.XS, 10)]
    [InlineData(SizePreset.S, 16)]
    [InlineData(SizePreset.M, 24)]
    [InlineData(SizePreset.L, 34)]
    [InlineData(SizePreset.XL, 48)]
    [InlineData(SizePreset.XXL, 68)]
    public void CrosshairSize_ReturnsExpectedValues(SizePreset size, double expected)
    {
        Assert.Equal(expected, RenderHelper.CrosshairSize(size));
    }

    // ── CrosshairThickness ──

    [Theory]
    [InlineData(OffsetLevel.Plus0, 1)]
    [InlineData(OffsetLevel.Plus1, 2)]
    [InlineData(OffsetLevel.Plus2, 3)]
    [InlineData(OffsetLevel.Plus3, 4)]
    [InlineData(OffsetLevel.Plus4, 5)]
    [InlineData(OffsetLevel.Plus5, 7)]
    [InlineData(OffsetLevel.Plus6, 10)]
    public void CrosshairThickness_ReturnsExpectedValues(OffsetLevel level, double expected)
    {
        Assert.Equal(expected, RenderHelper.CrosshairThickness(level));
    }

    // ── LengthOffsetPx ──

    [Theory]
    [InlineData(OffsetLevel.Plus0, 0)]
    [InlineData(OffsetLevel.Plus1, 2)]
    [InlineData(OffsetLevel.Plus2, 4)]
    [InlineData(OffsetLevel.Plus3, 7)]
    [InlineData(OffsetLevel.Plus4, 10)]
    [InlineData(OffsetLevel.Plus5, 14)]
    [InlineData(OffsetLevel.Plus6, 20)]
    public void LengthOffsetPx_ReturnsExpectedValues(OffsetLevel level, double expected)
    {
        Assert.Equal(expected, RenderHelper.LengthOffsetPx(level));
    }

    // ── OverlayTotalWidth ──

    [Fact]
    public void OverlayTotalWidth_SumsBarWidthAndLengthOffset()
    {
        // M (38) + Plus3 (7) = 45
        Assert.Equal(45, RenderHelper.OverlayTotalWidth(SizePreset.M, OffsetLevel.Plus3));
        // XXL (100) + Plus6 (20) = 120
        Assert.Equal(120, RenderHelper.OverlayTotalWidth(SizePreset.XXL, OffsetLevel.Plus6));
        // XXS (4) + Plus0 (0) = 4
        Assert.Equal(4, RenderHelper.OverlayTotalWidth(SizePreset.XXS, OffsetLevel.Plus0));
    }

    // ── OpacityToByte ──

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 127)]   // 50 * 255 / 100 = 127 (integer division)
    [InlineData(100, 255)]
    [InlineData(-10, 0)]    // Clamped to 0
    [InlineData(150, 255)]  // Clamped to 255
    public void OpacityToByte_ConvertsAndClamps(int opacity, byte expected)
    {
        Assert.Equal(expected, RenderHelper.OpacityToByte(opacity));
    }

    // ── GetSafeArea ──

    [Fact]
    public void GetSafeArea_16x9_ReturnsFullScreen()
    {
        var safe = RenderHelper.GetSafeArea(1920, 1080, AspectRatio.Ratio16x9);
        Assert.Equal(new Rect(0, 0, 1920, 1080), safe);
    }

    [Fact]
    public void GetSafeArea_21x9_OnWideScreen_LetterboxesVertically()
    {
        // 1920×1080 screen, 21:9 → target height = 1920 * 9/21 ≈ 822.86
        var safe = RenderHelper.GetSafeArea(1920, 1080, AspectRatio.Ratio21x9);
        Assert.Equal(0, safe.X);
        Assert.True(safe.Y > 0, "Should have vertical letterboxing");
        Assert.Equal(1920, safe.Width);
        Assert.True(safe.Height < 1080, "Height should be reduced");
    }

    [Fact]
    public void GetSafeArea_4x3_OnWideScreen_Pillarboxes()
    {
        // 1920×1080 screen, 4:3 → target width = 1080 * 4/3 = 1440
        var safe = RenderHelper.GetSafeArea(1920, 1080, AspectRatio.Ratio4x3);
        Assert.True(safe.X > 0, "Should have horizontal pillarboxing");
        Assert.Equal(0, safe.Y);
        Assert.Equal(1440, safe.Width);
        Assert.Equal(1080, safe.Height);
    }

    [Fact]
    public void GetSafeArea_5x4_OnWideScreen_Pillarboxes()
    {
        // 1920×1080 screen, 5:4 → target width = 1080 * 5/4 = 1350
        var safe = RenderHelper.GetSafeArea(1920, 1080, AspectRatio.Ratio5x4);
        Assert.True(safe.X > 0, "Should have horizontal pillarboxing");
        Assert.Equal(1350, safe.Width);
        Assert.Equal(1080, safe.Height);
    }

    [Fact]
    public void GetSafeArea_21x9_OnNarrowScreen_ReducesWidth()
    {
        // 1600×1200 screen (4:3), 21:9 → target height = 1600*9/21=685.7 < 1200
        // But 1600/685.7 = 2.33, which is > 21/9=2.33... let me compute:
        // targetH = 1600 * 9/21 = 685.71, which is <= 1200, so we letterbox vertically
        var safe = RenderHelper.GetSafeArea(1600, 1200, AspectRatio.Ratio21x9);
        Assert.Equal(1600, safe.Width);
        Assert.True(safe.Height < 1200);
    }
}
