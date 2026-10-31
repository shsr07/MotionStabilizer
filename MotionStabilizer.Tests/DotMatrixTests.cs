using MotionStabilizer.Overlay;
using MotionStabilizer.Models;
using System.Windows;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for the static dot matrix (OverlayShape.DotMatrix): grid size
/// mapping, the percentage-based layout geometry, resolution scaling of the
/// core diameter, and the new config defaults.
/// </summary>
public class DotMatrixTests
{
    // ── DotMatrixDotCount ──

    [Theory]
    [InlineData(DotMatrixGrid.Grid4x4, 4)]
    [InlineData(DotMatrixGrid.Grid5x5, 5)]
    [InlineData(DotMatrixGrid.Grid6x6, 6)]
    public void DotMatrixDotCount_ReturnsExpectedValues(DotMatrixGrid grid, int expected)
    {
        Assert.Equal(expected, RenderHelper.DotMatrixDotCount(grid));
    }

    // ── ComputeDotMatrixCenters ──

    [Theory]
    [InlineData(DotMatrixGrid.Grid4x4, 16)]
    [InlineData(DotMatrixGrid.Grid5x5, 25)]
    [InlineData(DotMatrixGrid.Grid6x6, 36)]
    public void ComputeDotMatrixCenters_ReturnsGridCount(DotMatrixGrid grid, int expected)
    {
        Assert.Equal(expected, RenderHelper.ComputeDotMatrixCenters(grid, OffsetLevel.Plus3, 1920, 1080).Count);
    }

    [Fact]
    public void ComputeDotMatrixCenters_4x4At1080p_MatchesReferenceScreenshot()
    {
        // The middle column-spacing level (+3) reproduces the measured
        // reference layout exactly.
        var centers = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus3, 1920, 1080);

        // Positions measured from the reference game screenshot at 1920×1080:
        // 336px horizontal margin (17.5%W), 83.7px vertical margin (7.75%H),
        // 416px column spacing and ~304px row spacing.
        Assert.Equal(336.0, centers[0].X, 6);
        Assert.Equal(83.7, centers[0].Y, 6);
        Assert.Equal(336.0 + 416.0, centers[1].X, 6);
        Assert.Equal(1584.0, centers[15].X, 6);
        Assert.Equal(996.3, centers[15].Y, 6);
    }

    [Fact]
    public void ComputeDotMatrixCenters_SpacingIsUniform()
    {
        const int n = 6;
        var centers = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid6x6, OffsetLevel.Plus3, 2560, 1440);

        double stepX = centers[1].X - centers[0].X;
        double stepY = centers[n].Y - centers[0].Y;
        Assert.True(stepX > 0);
        Assert.True(stepY > 0);

        for (int i = 0; i < centers.Count; i++)
        {
            int row = i / n, col = i % n;
            Assert.Equal(centers[0].X + col * stepX, centers[i].X, 6);
            Assert.Equal(centers[0].Y + row * stepY, centers[i].Y, 6);
        }
    }

    [Fact]
    public void ComputeDotMatrixCenters_IsMirroredAroundAreaCenter()
    {
        const double w = 1920, h = 1080;
        foreach (var grid in new[] { DotMatrixGrid.Grid4x4, DotMatrixGrid.Grid5x5, DotMatrixGrid.Grid6x6 })
        {
            var centers = RenderHelper.ComputeDotMatrixCenters(grid, OffsetLevel.Plus3, w, h);
            int n = RenderHelper.DotMatrixDotCount(grid);
            for (int k = 0; k < n; k++)
            {
                // Horizontal mirror within the first row, vertical mirror within
                // the first column — margins must be symmetric on both axes.
                Assert.Equal(w - centers[k].X, centers[n - 1 - k].X, 6);
                Assert.Equal(h - centers[k * n].Y, centers[(n - 1 - k) * n].Y, 6);
            }
        }
    }

    [Fact]
    public void ComputeDotMatrixCenters_KeepsReferenceMarginRatioAtEveryDensity()
    {
        // The 4×4 reference at 1080p: 336px margin vs 416px spacing horizontally,
        // 83.7px vs 304.2px vertically. Denser grids must preserve that
        // margin-to-spacing ratio, so the pattern stays self-similar while the
        // outer columns and rows move toward the screen edges.
        const double ratioX = 336.0 / 416.0;
        const double ratioY = 83.7 / 304.2;
        foreach (var grid in new[] { DotMatrixGrid.Grid4x4, DotMatrixGrid.Grid5x5, DotMatrixGrid.Grid6x6 })
        {
            int n = RenderHelper.DotMatrixDotCount(grid);
            var centers = RenderHelper.ComputeDotMatrixCenters(grid, OffsetLevel.Plus3, 1920, 1080);
            double stepX = centers[1].X - centers[0].X;
            double stepY = centers[n].Y - centers[0].Y; // row 1, col 0
            Assert.True(stepX > 0);
            Assert.True(stepY > 0);
            Assert.Equal(ratioX, centers[0].X / stepX, 4);
            Assert.Equal(ratioY, centers[0].Y / stepY, 4);
        }
    }

    [Fact]
    public void ComputeDotMatrixCenters_EdgeMarginsShrinkAsGridDensifies()
    {
        // Fixed margins would leave denser grids crowded toward the middle with
        // the same empty edges — the outer columns must move inward instead.
        var at4 = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus3, 1920, 1080);
        var at5 = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid5x5, OffsetLevel.Plus3, 1920, 1080);
        var at6 = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid6x6, OffsetLevel.Plus3, 1920, 1080);
        Assert.True(at5[0].X < at4[0].X);
        Assert.True(at6[0].X < at5[0].X);
        Assert.True(at5[0].Y < at4[0].Y);
        Assert.True(at6[0].Y < at5[0].Y);

        // Concrete 1080p values of the self-similar layout.
        Assert.Equal(276.16, at5[0].X, 2);
        Assert.Equal(65.31, at5[0].Y, 2);
        Assert.Equal(234.42, at6[0].X, 2);
        Assert.Equal(53.54, at6[0].Y, 1);
    }

    [Fact]
    public void ComputeDotMatrixCenters_SpacingLevelScalesColumnsAndEdgesTogether()
    {
        // 列间距 (+3 default) scales the column spacing 10% per level around the
        // reference layout; the edge margins absorb the difference so both move
        // together and the grid stays centred.
        var at0 = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus0, 1920, 1080);
        var at3 = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus3, 1920, 1080);
        var at6 = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus6, 1920, 1080);

        double step0 = at0[1].X - at0[0].X;
        double step3 = at3[1].X - at3[0].X;
        double step6 = at6[1].X - at6[0].X;

        // Spacing: 0.7× / 1.0× / 1.3× of the reference 416px.
        Assert.Equal(416.0 * 0.7, step0, 2);
        Assert.Equal(416.0, step3, 2);
        Assert.Equal(416.0 * 1.3, step6, 2);

        // Edge margins move in the opposite direction, symmetric per level.
        Assert.Equal(523.2, at0[0].X, 2);
        Assert.Equal(148.8, at6[0].X, 2);
        Assert.Equal(1920.0 - at0[15].X, at0[0].X, 2);

        // Vertical layout is unaffected by the column-spacing level.
        Assert.Equal(at3[0].Y, at0[0].Y, 6);
        Assert.Equal(at3[0].Y, at6[0].Y, 6);
    }

    [Fact]
    public void ComputeDotMatrixCenters_HigherSpacingLevelsKeepDotsOnScreen()
    {
        // Even at +6 (widest) the outer dot centres must stay inside the area
        // for every supported density — the margins never go negative.
        foreach (var grid in new[] { DotMatrixGrid.Grid4x4, DotMatrixGrid.Grid5x5, DotMatrixGrid.Grid6x6 })
        {
            var centers = RenderHelper.ComputeDotMatrixCenters(grid, OffsetLevel.Plus6, 1920, 1080);
            foreach (var c in centers)
            {
                Assert.InRange(c.X, 0, 1920);
                Assert.InRange(c.Y, 0, 1080);
            }
        }
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-5, 1080)]
    public void ComputeDotMatrixCenters_DegenerateArea_ReturnsEmpty(double w, double h)
    {
        Assert.Empty(RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus3, w, h));
    }

    [Fact]
    public void ComputeDotMatrixCenters_ScaleRelativelyWithArea()
    {
        // The layout is percentage-based: 4K must produce the exact same
        // relative positions as 1080p, and an ultrawide must stretch only
        // horizontally — that is the resolution/aspect adaptation contract.
        var at1080 = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus3, 1920, 1080);
        var at4k = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus3, 3840, 2160);
        var ultrawide = RenderHelper.ComputeDotMatrixCenters(DotMatrixGrid.Grid4x4, OffsetLevel.Plus3, 3440, 1440);

        for (int i = 0; i < at1080.Count; i++)
        {
            Assert.Equal(at1080[i].X / 1920, at4k[i].X / 3840, 6);
            Assert.Equal(at1080[i].Y / 1080, at4k[i].Y / 2160, 6);
            Assert.Equal(at1080[i].X / 1920, ultrawide[i].X / 3440, 6);
            Assert.Equal(at1080[i].Y / 1080, ultrawide[i].Y / 1440, 6);
        }
    }

    // ── DotMatrixCoreDiameterPx ──

    [Fact]
    public void DotMatrixCoreDiameterPx_ScalesWithAreaHeight()
    {
        var cfg = new OverlayConfig { DotMatrixCoreDiameter = 6 };
        Assert.Equal(6.0, RenderHelper.DotMatrixCoreDiameterPx(cfg, 1080), 6);
        Assert.Equal(8.0, RenderHelper.DotMatrixCoreDiameterPx(cfg, 1440), 6);
        Assert.Equal(12.0, RenderHelper.DotMatrixCoreDiameterPx(cfg, 2160), 6);
    }

    [Fact]
    public void DotMatrixCoreDiameterPx_ClampsToMinimum()
    {
        var cfg = new OverlayConfig { DotMatrixCoreDiameter = 3 };
        // 3 × 400/1080 ≈ 1.11 → clamped so extreme ratios keep dots visible
        Assert.Equal(2.0, RenderHelper.DotMatrixCoreDiameterPx(cfg, 400), 6);
    }

    // ── Config defaults ──

    [Fact]
    public void DotMatrixConfig_HasExpectedDefaults()
    {
        var cfg = new OverlayConfig();
        Assert.Equal(DotMatrixGrid.Grid4x4, cfg.DotMatrixGrid);
        Assert.True(cfg.DotMatrixSoftGlow);
        Assert.Equal(10.0, cfg.DotMatrixCoreDiameter, 6);
        Assert.Equal(1.0, cfg.DotMatrixOutlineWidth, 6);
        // Every shape's outline now defaults to 1px.
        Assert.Equal(1.0, cfg.OverlayOutlineWidth, 6);
        // All overlays default to a white custom colour with a black outline
        // enabled out of the box.
        Assert.Equal(ColorPreset.Custom, cfg.ColorPreset);
        Assert.Equal("#FFFFFF", cfg.CustomColorHex);
        Assert.True(cfg.OverlayOutlineEnabled);
        Assert.Equal(OutlineColorPreset.Black, cfg.OverlayOutlineColorPreset);
    }
}
