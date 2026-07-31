using MotionStabilizer.Models;
using System.ComponentModel;
using System.Windows.Media;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for config model classes: color parsing, edge visibility,
/// edge opacity, and clock font handling.
/// </summary>
public class ConfigModelTests
{
    // ── OverlayConfig.GetColor ──

    [Fact]
    public void OverlayConfig_GetColor_Red()
    {
        var cfg = new OverlayConfig { ColorPreset = ColorPreset.Red };
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), cfg.GetColor());
    }

    [Fact]
    public void OverlayConfig_GetColor_Green()
    {
        var cfg = new OverlayConfig { ColorPreset = ColorPreset.Green };
        Assert.Equal(Color.FromRgb(0x00, 0xFF, 0x00), cfg.GetColor());
    }

    [Fact]
    public void OverlayConfig_GetColor_Blue()
    {
        var cfg = new OverlayConfig { ColorPreset = ColorPreset.Blue };
        Assert.Equal(Color.FromRgb(0x00, 0x99, 0xFF), cfg.GetColor());
    }

    [Fact]
    public void OverlayConfig_GetColor_Custom_ValidHex()
    {
        var cfg = new OverlayConfig { ColorPreset = ColorPreset.Custom, CustomColorHex = "#FF8800" };
        var color = cfg.GetColor();
        Assert.Equal(0xFF, color.R);
        Assert.Equal(0x88, color.G);
        Assert.Equal(0x00, color.B);
    }

    [Fact]
    public void OverlayConfig_GetColor_Custom_InvalidHex_FallsBackToGreen()
    {
        var cfg = new OverlayConfig { ColorPreset = ColorPreset.Custom, CustomColorHex = "not-a-color" };
        Assert.Equal(Color.FromRgb(0x00, 0xFF, 0x00), cfg.GetColor());
    }

    // ── OverlayConfig.IsEdgeVisible ──

    [Fact]
    public void OverlayConfig_IsEdgeVisible_AllVisible()
    {
        var cfg = new OverlayConfig
        {
            EdgeTopVisible = true,
            EdgeBottomVisible = true,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };
        Assert.True(cfg.IsEdgeVisible(EdgeSide.Top));
        Assert.True(cfg.IsEdgeVisible(EdgeSide.Bottom));
        Assert.True(cfg.IsEdgeVisible(EdgeSide.Left));
        Assert.True(cfg.IsEdgeVisible(EdgeSide.Right));
    }

    [Fact]
    public void OverlayConfig_IsEdgeVisible_OnlyLeftAndRight()
    {
        var cfg = new OverlayConfig
        {
            EdgeTopVisible = false,
            EdgeBottomVisible = false,
            EdgeLeftVisible = true,
            EdgeRightVisible = true
        };
        Assert.False(cfg.IsEdgeVisible(EdgeSide.Top));
        Assert.False(cfg.IsEdgeVisible(EdgeSide.Bottom));
        Assert.True(cfg.IsEdgeVisible(EdgeSide.Left));
        Assert.True(cfg.IsEdgeVisible(EdgeSide.Right));
    }

    // ── OverlayConfig.GetEdgeOpacity ──

    [Fact]
    public void OverlayConfig_GetEdgeOpacity_UniformMode_ReturnsGlobalOpacity()
    {
        var cfg = new OverlayConfig
        {
            OpacityMode = EdgeOpacityMode.Uniform,
            Opacity = 75,
            EdgeTopOpacity = 30,
            EdgeBottomOpacity = 40
        };
        Assert.Equal(75, cfg.GetEdgeOpacity(EdgeSide.Top));
        Assert.Equal(75, cfg.GetEdgeOpacity(EdgeSide.Bottom));
        Assert.Equal(75, cfg.GetEdgeOpacity(EdgeSide.Left));
        Assert.Equal(75, cfg.GetEdgeOpacity(EdgeSide.Right));
    }

    [Fact]
    public void OverlayConfig_GetEdgeOpacity_PerEdgeMode_ReturnsPerEdgeOpacity()
    {
        var cfg = new OverlayConfig
        {
            OpacityMode = EdgeOpacityMode.PerEdge,
            Opacity = 75,
            EdgeTopOpacity = 30,
            EdgeBottomOpacity = 40,
            EdgeLeftOpacity = 50,
            EdgeRightOpacity = 60
        };
        Assert.Equal(30, cfg.GetEdgeOpacity(EdgeSide.Top));
        Assert.Equal(40, cfg.GetEdgeOpacity(EdgeSide.Bottom));
        Assert.Equal(50, cfg.GetEdgeOpacity(EdgeSide.Left));
        Assert.Equal(60, cfg.GetEdgeOpacity(EdgeSide.Right));
    }

    // ── CrosshairConfig.GetColor ──

    [Fact]
    public void CrosshairConfig_GetColor_Red()
    {
        var cfg = new CrosshairConfig { ColorPreset = ColorPreset.Red };
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), cfg.GetColor());
    }

    [Fact]
    public void CrosshairConfig_GetColor_Custom_InvalidHex_FallsBackToRed()
    {
        var cfg = new CrosshairConfig { ColorPreset = ColorPreset.Custom, CustomColorHex = "!!!" };
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), cfg.GetColor());
    }

    // ── ClockConfig ──

    [Fact]
    public void ClockConfig_GetRenderFontFamily_Outline_ReturnsConsolas()
    {
        var cfg = new ClockConfig { FontFamily = "Outline" };
        Assert.Equal("Consolas", cfg.GetRenderFontFamily());
    }

    [Fact]
    public void ClockConfig_GetRenderFontFamily_Custom_ReturnsAsIs()
    {
        var cfg = new ClockConfig { FontFamily = "Arial" };
        Assert.Equal("Arial", cfg.GetRenderFontFamily());
    }

    [Fact]
    public void ClockConfig_IsOutlineFont_True_WhenOutline()
    {
        var cfg = new ClockConfig { FontFamily = "Outline" };
        Assert.True(cfg.IsOutlineFont);
    }

    [Fact]
    public void ClockConfig_IsOutlineFont_False_WhenNotOutline()
    {
        var cfg = new ClockConfig { FontFamily = "Arial" };
        Assert.False(cfg.IsOutlineFont);
    }

    [Fact]
    public void ClockConfig_GetColor_ValidHex()
    {
        var cfg = new ClockConfig { ColorHex = "#FF0000" };
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), cfg.GetColor());
    }

    [Fact]
    public void ClockConfig_GetColor_InvalidHex_FallsBackToWhite()
    {
        var cfg = new ClockConfig { ColorHex = "invalid" };
        Assert.Equal(Colors.White, cfg.GetColor());
    }
}
