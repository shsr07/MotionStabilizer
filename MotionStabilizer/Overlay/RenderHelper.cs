using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MotionStabilizer.Models;
using MotionStabilizer.Services;

namespace MotionStabilizer.Overlay;

/// <summary>
/// Central rendering helper. Converts config enums to actual pixel dimensions
/// and builds WPF Shape elements for the overlay, crosshair, and clock.
/// </summary>
public static class RenderHelper
{
    // ── Size → pixel mappings ──────────────────────────────────────────

    /// <summary>Overlay bar width (px) — the thickness of each edge bar.
    /// Default (M) ≈ 38px ≈ 1cm at 96 DPI.</summary>
    public static double OverlayBarWidth(SizePreset s) => s switch
    {
        SizePreset.XXS => 4,
        SizePreset.XS => 8,
        SizePreset.S => 15,
        SizePreset.M => 38,
        SizePreset.L => 55,
        SizePreset.XL => 75,
        SizePreset.XXL => 100,
        _ => 38
    };

    /// <summary>Total bar width including length offset.</summary>
    public static double OverlayTotalWidth(SizePreset s, OffsetLevel len) =>
        OverlayBarWidth(s) + LengthOffsetPx(len);

    /// <summary>
    /// Thickness (粗细) of each pole bar for the Pole shape, per size preset.
    /// Anchored at M = 70 px (the original pole spec).
    /// </summary>
    public static double PoleThickness(SizePreset s) => s switch
    {
        SizePreset.XXS => 25,
        SizePreset.XS => 38,
        SizePreset.S => 52,
        SizePreset.M => 70,
        SizePreset.L => 90,
        SizePreset.XL => 115,
        SizePreset.XXL => 145,
        _ => 70
    };

    /// <summary>Diameter of each motion cue dot in device-independent pixels.</summary>
    public static double MotionDotDiameter(SizePreset s) => s switch
    {
        SizePreset.XXS => 12,
        SizePreset.XS => 20,
        SizePreset.S => 29,
        SizePreset.M => 41,
        SizePreset.L => 53,
        SizePreset.XL => 73,
        SizePreset.XXL => 94,
        _ => 41
    };

    /// <summary>
    /// Width of a single motion-dot zone as a fraction of the monitor width.
    /// Single source of truth: OverlayWindow builds MotionZone rectangles from
    /// this ratio and the renderer consumes the resulting widths, so the value
    /// must never be duplicated as a literal in either place.
    /// </summary>
    public const float MotionZoneWidthRatio = 0.12f;

    /// <summary>Length offset in px.</summary>
    public static double LengthOffsetPx(OffsetLevel l) => l switch
    {
        OffsetLevel.Plus0 => 0,
        OffsetLevel.Plus1 => 2,
        OffsetLevel.Plus2 => 4,
        OffsetLevel.Plus3 => 7,
        OffsetLevel.Plus4 => 10,
        OffsetLevel.Plus5 => 14,
        OffsetLevel.Plus6 => 20,
        _ => 0
    };

    /// <summary>Crosshair overall size (diameter/width in px).</summary>
    public static double CrosshairSize(SizePreset s) => s switch
    {
        SizePreset.XXS => 6,
        SizePreset.XS => 10,
        SizePreset.S => 16,
        SizePreset.M => 24,
        SizePreset.L => 34,
        SizePreset.XL => 48,
        SizePreset.XXL => 68,
        _ => 16
    };

    /// <summary>Crosshair line thickness (px).</summary>
    public static double CrosshairThickness(OffsetLevel t) => t switch
    {
        OffsetLevel.Plus0 => 1,
        OffsetLevel.Plus1 => 2,
        OffsetLevel.Plus2 => 3,
        OffsetLevel.Plus3 => 4,
        OffsetLevel.Plus4 => 5,
        OffsetLevel.Plus5 => 7,
        OffsetLevel.Plus6 => 10,
        _ => 1
    };

    // ── Aspect Ratio safe area ─────────────────────────────────────────

    /// <summary>
    /// Calculate the safe area rectangle based on aspect ratio.
    /// 16:9 fills a 16:9 screen; 21:9 letterboxes vertically.
    /// </summary>
    public static Rect GetSafeArea(double screenW, double screenH, AspectRatio ar)
    {
        if (ar == AspectRatio.Ratio16x9)
        {
            return new Rect(0, 0, screenW, screenH);
        }
        if (ar == AspectRatio.Ratio21x9)
        {
            // 21:9 → keep width, reduce height to maintain 21:9 aspect
            double targetH = screenW * 9.0 / 21.0;
            if (targetH <= screenH)
            {
                double yOffset = (screenH - targetH) / 2;
                return new Rect(0, yOffset, screenW, targetH);
            }
            // Screen is narrower than 21:9 → reduce width instead
            double targetW = screenH * 21.0 / 9.0;
            double xOffset = (screenW - targetW) / 2;
            return new Rect(xOffset, 0, targetW, screenH);
        }
        if (ar == AspectRatio.Ratio4x3)
        {
            // 4:3 → pillarbox: reduce width to maintain 4:3 aspect
            double targetW = screenH * 4.0 / 3.0;
            if (targetW <= screenW)
            {
                double xOffset = (screenW - targetW) / 2;
                return new Rect(xOffset, 0, targetW, screenH);
            }
            // Screen is narrower than 4:3 → reduce height instead
            double targetH = screenW * 3.0 / 4.0;
            double yOffset = (screenH - targetH) / 2;
            return new Rect(0, yOffset, screenW, targetH);
        }
        if (ar == AspectRatio.Ratio5x4)
        {
            // 5:4 → pillarbox: reduce width to maintain 5:4 aspect
            double targetW = screenH * 5.0 / 4.0;
            if (targetW <= screenW)
            {
                double xOffset = (screenW - targetW) / 2;
                return new Rect(xOffset, 0, targetW, screenH);
            }
            // Screen is narrower than 5:4 → reduce height instead
            double targetH = screenW * 4.0 / 5.0;
            double yOffset = (screenH - targetH) / 2;
            return new Rect(0, yOffset, screenW, targetH);
        }
        return new Rect(0, 0, screenW, screenH);
    }

    // ── Overlay shape builders ─────────────────────────────────────────

    /// <summary>
    /// Build all shapes for the edge overlay within the given monitor area.
    /// Shape dimensions (DIP at 96 DPI) are scaled by <paramref name="monDpiScale"/> /
    /// <paramref name="windowScale"/> so they appear at the correct physical size on
    /// the target monitor, regardless of the WPF window's own DPI.
    /// </summary>
    /// <param name="monitorBounds">Monitor bounds in canvas (window-DPI) coordinates.</param>
    /// <param name="windowScale">DPI scale of the WPF overlay window (canvas coordinate system).</param>
    /// <param name="monDpiScale">Per-monitor DPI scale of the target monitor.</param>
    public static List<Shape> BuildOverlayShapes(OverlayConfig cfg, Rect monitorBounds,
        double windowScale = 0, double monDpiScale = 0)
    {
        var shapes = new List<Shape>();
        var color = cfg.GetColor();

        // Scale shape dimensions from 96-DPI DIP to the monitor's physical size,
        // expressed in canvas (window-DPI) DIP.
        double dpiRatio = (windowScale > 0 && monDpiScale > 0)
            ? monDpiScale / windowScale
            : 1.0;
        double sizePx = OverlayBarWidth(cfg.Size) * dpiRatio;
        double lengthPx = LengthOffsetPx(cfg.Length) * dpiRatio;

        // Compute safe area within this monitor
        var safe = GetSafeArea(monitorBounds.Width, monitorBounds.Height, cfg.AspectRatio);
        Rect drawArea = new Rect(
            monitorBounds.X + safe.X,
            monitorBounds.Y + safe.Y,
            safe.Width,
            safe.Height
        );

        // In Window mode, follow the foreground window only if it's on this monitor
        if (cfg.Mode == DisplayMode.Window)
        {
            var fwRect = Win32Interop.GetForegroundWindowRect();
            if (fwRect.HasValue)
            {
                // Convert foreground window rect to canvas (window-DPI) coordinates
                double scale = windowScale > 0 ? windowScale : Win32Interop.GetDpiScale();
                double vsX = Win32Interop.GetVirtualScreenX() / scale;
                double vsY = Win32Interop.GetVirtualScreenY() / scale;
                var r = fwRect.Value;
                double fwCenterX = (r.Left + r.Right) / 2.0 / scale - vsX;
                double fwCenterY = (r.Top + r.Bottom) / 2.0 / scale - vsY;

                if (fwCenterX >= monitorBounds.X && fwCenterX < monitorBounds.X + monitorBounds.Width &&
                    fwCenterY >= monitorBounds.Y && fwCenterY < monitorBounds.Y + monitorBounds.Height)
                {
                    drawArea = new Rect(r.Left / scale - vsX, r.Top / scale - vsY,
                        (r.Right - r.Left) / scale, (r.Bottom - r.Top) / scale);
                }
                else
                {
                    // Foreground window is not on this monitor
                    return shapes;
                }
            }
        }

        if (cfg.Split == SplitScreen.Vertical)
        {
            double halfW = drawArea.Width / 2;
            shapes.AddRange(BuildSingleOverlay(cfg, new Rect(drawArea.X, drawArea.Y, halfW, drawArea.Height), sizePx, lengthPx, dpiRatio, color));
            shapes.AddRange(BuildSingleOverlay(cfg, new Rect(drawArea.X + halfW, drawArea.Y, halfW, drawArea.Height), sizePx, lengthPx, dpiRatio, color));
        }
        else if (cfg.Split == SplitScreen.Horizontal)
        {
            double halfH = drawArea.Height / 2;
            shapes.AddRange(BuildSingleOverlay(cfg, new Rect(drawArea.X, drawArea.Y, drawArea.Width, halfH), sizePx, lengthPx, dpiRatio, color));
            shapes.AddRange(BuildSingleOverlay(cfg, new Rect(drawArea.X, drawArea.Y + halfH, drawArea.Width, halfH), sizePx, lengthPx, dpiRatio, color));
        }
        else
        {
            shapes.AddRange(BuildSingleOverlay(cfg, drawArea, sizePx, lengthPx, dpiRatio, color));
        }

        return shapes;
    }

    /// <summary>
    /// Add a filled rectangle plus — when outlines are on — an outline ring
    /// built from four strips that sit entirely OUTSIDE the fill.
    /// The ring must never extend under the fill: fills are translucent (the
    /// opacity slider), so a larger underlay rectangle would darken the whole
    /// fill area (white fill over a black underlay renders grey) and make
    /// outlined rectangles inconsistent with the stroked paths (pole tip,
    /// dome, flag), whose fills sit directly on the background.
    /// Individual sides can be excluded (outlineTop/Bottom/Left/Right) for
    /// composite shapes whose parts join flush — the pole skips the side
    /// where its triangular tip attaches, so no seam line crosses the shape.
    /// </summary>
    private static void AddRect(List<Shape> result, Brush fill, Brush? outline, double outlineW,
        double x, double y, double w, double h,
        bool outlineTop = true, bool outlineBottom = true, bool outlineLeft = true, bool outlineRight = true)
    {
        if (outline != null && outlineW > 0)
        {
            // Four strips around the fill. The horizontal strips span the full
            // outline width so they cover the corners — no gaps, no overlaps,
            // therefore no double-blended seams between strips.
            if (outlineTop)
                AddRectStrip(result, outline, x - outlineW, y - outlineW, w + outlineW * 2, outlineW);
            if (outlineBottom)
                AddRectStrip(result, outline, x - outlineW, y + h, w + outlineW * 2, outlineW);
            if (outlineLeft)
                AddRectStrip(result, outline, x - outlineW, y, outlineW, h);
            if (outlineRight)
                AddRectStrip(result, outline, x + w, y, outlineW, h);
        }

        var rect = new Rectangle { Fill = fill, Width = w, Height = h };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        result.Add(rect);
    }

    private static void AddRectStrip(List<Shape> result, Brush outline, double x, double y, double w, double h)
    {
        var strip = new Rectangle { Fill = outline, Width = w, Height = h };
        Canvas.SetLeft(strip, x);
        Canvas.SetTop(strip, y);
        result.Add(strip);
    }

    private static List<Shape> BuildSingleOverlay(OverlayConfig cfg, Rect area, double sizePx, double lengthPx, double dpiRatio, Color color)
    {
        var result = new List<Shape>();
        double x = area.X;
        double y = area.Y;
        double w = area.Width;
        double h = area.Height;
        double cx = x + w / 2;
        double cy = y + h / 2;

        Brush brushFor(EdgeSide side) => new SolidColorBrush(
            Color.FromArgb(OpacityToByte(cfg.GetEdgeOpacity(side)), color.R, color.G, color.B));

        // Outline is shared with the motion dots — same colour, same hotkey, same
        // thickness. Off by default; when on it costs one extra shape per
        // rectangle and one stroke per path.
        bool outlineOn = cfg.OverlayOutlineEnabled;
        double outlineW = outlineOn ? Math.Clamp(cfg.OverlayOutlineWidth, 0.5, 5.0) * dpiRatio : 0;
        var outlineColor = cfg.GetOutlineColor();
        Brush? outlineBrushFor(EdgeSide side) => outlineOn
            ? new SolidColorBrush(Color.FromArgb(
                OpacityToByte(cfg.GetEdgeOpacity(side)), outlineColor.R, outlineColor.G, outlineColor.B))
            : null;

        switch (cfg.Shape)
        {
            case OverlayShape.Pole:
                // ── Four poles sticking out from each screen edge toward the
                //     screen center, centered on their edge, with a pointed
                //     triangular tip at the inward end.
                //     Size preset → thickness (粗细); Length offset → length (长短).
                //     Base lengths at +0: the fixed spec values (400 px vertical,
                //     750 px horizontal) when they fit; otherwise 80% of the axis
                //     maximum, so the Length slider stays responsive on every
                //     aspect ratio and DPI scale. Length +6 maps to
                //     the axis maximum (half the axis minus half the thickness,
                //     i.e. the poles reach just short of the screen center);
                //     levels 0..6 interpolate linearly in between. The maxima are
                //     computed per monitor, so every aspect ratio is handled. ──
                double poleThick = Math.Max(PoleThickness(cfg.Size) * dpiRatio, 2);
                double tipLen = poleThick; // triangular tip height = thickness

                double maxLenH = Math.Max(w / 2 - poleThick / 2, tipLen);
                double maxLenV = Math.Max(h / 2 - poleThick / 2, tipLen);
                double baseLenH = Math.Max(Math.Min(750 * dpiRatio, maxLenH * 0.8), tipLen);
                double baseLenV = Math.Max(Math.Min(400 * dpiRatio, maxLenV * 0.8), tipLen);

                // Length level 0..6 → interpolation factor 0..1
                double t = (int)cfg.Length / 6.0;
                double lenH = baseLenH + (maxLenH - baseLenH) * t;
                double lenV = baseLenV + (maxLenV - baseLenV) * t;

                // Top edge — vertical pole pointing down into the screen
                if (cfg.IsEdgeVisible(EdgeSide.Top))
                {
                    AddRect(result, brushFor(EdgeSide.Top), outlineBrushFor(EdgeSide.Top), outlineW,
                        cx - poleThick / 2, y, poleThick, lenV - tipLen,
                        outlineBottom: false);
                    // Open figure starting at the base so the stroke draws only
                    // the two slants — the joint with the body stays clean.
                    result.Add(MakeTriangle(brushFor(EdgeSide.Top), outlineBrushFor(EdgeSide.Top), outlineW,
                        cx - poleThick / 2, y + lenV - tipLen,
                        cx, y + lenV,
                        cx + poleThick / 2, y + lenV - tipLen,
                        closed: false));
                }

                // Bottom edge — vertical pole pointing up into the screen
                if (cfg.IsEdgeVisible(EdgeSide.Bottom))
                {
                    AddRect(result, brushFor(EdgeSide.Bottom), outlineBrushFor(EdgeSide.Bottom), outlineW,
                        cx - poleThick / 2, y + h - (lenV - tipLen), poleThick, lenV - tipLen,
                        outlineTop: false);
                    result.Add(MakeTriangle(brushFor(EdgeSide.Bottom), outlineBrushFor(EdgeSide.Bottom), outlineW,
                        cx - poleThick / 2, y + h - lenV + tipLen,
                        cx, y + h - lenV,
                        cx + poleThick / 2, y + h - lenV + tipLen,
                        closed: false));
                }

                // Left edge — horizontal pole pointing right into the screen
                if (cfg.IsEdgeVisible(EdgeSide.Left))
                {
                    AddRect(result, brushFor(EdgeSide.Left), outlineBrushFor(EdgeSide.Left), outlineW,
                        x, cy - poleThick / 2, lenH - tipLen, poleThick,
                        outlineRight: false);
                    result.Add(MakeTriangle(brushFor(EdgeSide.Left), outlineBrushFor(EdgeSide.Left), outlineW,
                        x + lenH - tipLen, cy - poleThick / 2,
                        x + lenH, cy,
                        x + lenH - tipLen, cy + poleThick / 2,
                        closed: false));
                }

                // Right edge — horizontal pole pointing left into the screen
                if (cfg.IsEdgeVisible(EdgeSide.Right))
                {
                    AddRect(result, brushFor(EdgeSide.Right), outlineBrushFor(EdgeSide.Right), outlineW,
                        x + w - (lenH - tipLen), cy - poleThick / 2, lenH - tipLen, poleThick,
                        outlineLeft: false);
                    result.Add(MakeTriangle(brushFor(EdgeSide.Right), outlineBrushFor(EdgeSide.Right), outlineW,
                        x + w - lenH + tipLen, cy - poleThick / 2,
                        x + w - lenH, cy,
                        x + w - lenH + tipLen, cy + poleThick / 2,
                        closed: false));
                }
                break;

            case OverlayShape.Box:
                // ── Four centered rectangular bars, one on each edge ──
                // Size  → thickness (perpendicular to edge)
                // Length → length along edge
                // Box is scaled 1.5× to match Flag visual size
                double boxThick = Math.Max(sizePx * 1.5, 2);
                double boxLen = Math.Max(sizePx * 2 * 1.5 + lengthPx * 8 * 1.5, boxThick * 2);

                // Top edge — horizontal rectangle centered on top
                if (cfg.IsEdgeVisible(EdgeSide.Top))
                {
                    AddRect(result, brushFor(EdgeSide.Top), outlineBrushFor(EdgeSide.Top), outlineW,
                        cx - boxLen / 2, y, boxLen, boxThick);
                }

                // Bottom edge — horizontal rectangle centered on bottom
                if (cfg.IsEdgeVisible(EdgeSide.Bottom))
                {
                    AddRect(result, brushFor(EdgeSide.Bottom), outlineBrushFor(EdgeSide.Bottom), outlineW,
                        cx - boxLen / 2, y + h - boxThick, boxLen, boxThick);
                }

                // Left edge — vertical rectangle centered on left
                if (cfg.IsEdgeVisible(EdgeSide.Left))
                {
                    AddRect(result, brushFor(EdgeSide.Left), outlineBrushFor(EdgeSide.Left), outlineW,
                        x, cy - boxLen / 2, boxThick, boxLen);
                }

                // Right edge — vertical rectangle centered on right
                if (cfg.IsEdgeVisible(EdgeSide.Right))
                {
                    AddRect(result, brushFor(EdgeSide.Right), outlineBrushFor(EdgeSide.Right), outlineW,
                        x + w - boxThick, cy - boxLen / 2, boxThick, boxLen);
                }
                break;

            case OverlayShape.Dome:
                // ── Four half-ellipses, one centered on each edge ──
                // Size   → bulge depth (perpendicular to edge, into screen)
                // Length → width along edge (flat side length)
                // Dome is scaled 1.5× to match Flag visual size
                double domeDepth = Math.Max(sizePx * 1.5, 8);
                double domeWidth = Math.Max(sizePx * 1.5 + lengthPx * 5 * 1.5, domeDepth);

                // Top edge — half-ellipse bulging downward (into screen)
                if (cfg.IsEdgeVisible(EdgeSide.Top))
                    result.Add(MakeHalfEllipse(brushFor(EdgeSide.Top), outlineBrushFor(EdgeSide.Top), outlineW,
                        cx, y, domeWidth, domeDepth, SweepDirection.Counterclockwise, false));

                // Bottom edge — half-ellipse bulging upward (into screen)
                if (cfg.IsEdgeVisible(EdgeSide.Bottom))
                    result.Add(MakeHalfEllipse(brushFor(EdgeSide.Bottom), outlineBrushFor(EdgeSide.Bottom), outlineW,
                        cx, y + h, domeWidth, domeDepth, SweepDirection.Clockwise, false));

                // Left edge — half-ellipse bulging rightward (into screen)
                if (cfg.IsEdgeVisible(EdgeSide.Left))
                    result.Add(MakeHalfEllipse(brushFor(EdgeSide.Left), outlineBrushFor(EdgeSide.Left), outlineW,
                        x, cy, domeWidth, domeDepth, SweepDirection.Clockwise, true));

                // Right edge — half-ellipse bulging leftward (into screen)
                if (cfg.IsEdgeVisible(EdgeSide.Right))
                    result.Add(MakeHalfEllipse(brushFor(EdgeSide.Right), outlineBrushFor(EdgeSide.Right), outlineW,
                        x + w, cy, domeWidth, domeDepth, SweepDirection.Counterclockwise, true));
                break;

            case OverlayShape.Flag:
                // ── Four isosceles triangles at each edge center, apex pointing to screen center ──
                // Size   → height (perpendicular to edge, into screen)
                // Length → base width (along edge)
                double triHeight = Math.Max(sizePx * 4, 60);
                double triBase = Math.Max(sizePx * 2.5 + lengthPx * 8, 40);

                // Top edge triangle — base on top edge, apex pointing down toward center
                if (cfg.IsEdgeVisible(EdgeSide.Top))
                    result.Add(MakeTriangle(brushFor(EdgeSide.Top), outlineBrushFor(EdgeSide.Top), outlineW,
                        cx - triBase / 2, y,
                        cx + triBase / 2, y,
                        cx, y + triHeight));

                // Bottom edge triangle — base on bottom edge, apex pointing up toward center
                if (cfg.IsEdgeVisible(EdgeSide.Bottom))
                    result.Add(MakeTriangle(brushFor(EdgeSide.Bottom), outlineBrushFor(EdgeSide.Bottom), outlineW,
                        cx - triBase / 2, y + h,
                        cx + triBase / 2, y + h,
                        cx, y + h - triHeight));

                // Left edge triangle — base on left edge, apex pointing right toward center
                if (cfg.IsEdgeVisible(EdgeSide.Left))
                    result.Add(MakeTriangle(brushFor(EdgeSide.Left), outlineBrushFor(EdgeSide.Left), outlineW,
                        x, cy - triBase / 2,
                        x, cy + triBase / 2,
                        x + triHeight, cy));

                // Right edge triangle — base on right edge, apex pointing left toward center
                if (cfg.IsEdgeVisible(EdgeSide.Right))
                    result.Add(MakeTriangle(brushFor(EdgeSide.Right), outlineBrushFor(EdgeSide.Right), outlineW,
                        x + w, cy - triBase / 2,
                        x + w, cy + triBase / 2,
                        x + w - triHeight, cy));
                break;

            case OverlayShape.DotMatrix:
                // ── Static N×N grid of reference dots, evenly distributed across
                //     the draw area. Split and per-edge visibility do not apply —
                //     a full-screen grid has no edges. ──
                BuildDotMatrix(result, cfg, area, dpiRatio, color);
                break;
        }
        return result;
    }

    // ── Static dot matrix ──────────────────────────────────────────────

    /// <summary>
    /// Horizontal inset of the outermost dot centres for the 4×4 reference
    /// grid, as a fraction of the area width — 17.5% in the reference
    /// screenshot. Denser grids derive their margins from this ratio (see
    /// <see cref="ComputeDotMatrixCenters"/>).
    /// </summary>
    public const double DotMatrixMarginXRatio = 0.175;

    /// <summary>
    /// Vertical inset of the outermost dot centres for the 4×4 reference
    /// grid, as a fraction of the area height — 7.75% in the reference
    /// screenshot.
    /// </summary>
    public const double DotMatrixMarginYRatio = 0.0775;

    /// <summary>
    /// Edge-margin-to-spacing ratios preserved from the measured 4×4
    /// reference (17.5%W margin vs 21.67%W spacing → margin ≈ 0.808 ×
    /// spacing horizontally; 7.75%H vs 28.17%H → ≈ 0.275 vertically).
    /// Every density keeps its grid's ratio, so denser grids shrink the
    /// edge margins in the same proportion as the spacing and the pattern
    /// stays self-similar instead of crowding against fixed margins.
    /// </summary>
    public const double DotMatrixMarginPerSpacingX = DotMatrixMarginXRatio * 3 / (1 - 2 * DotMatrixMarginXRatio);
    public const double DotMatrixMarginPerSpacingY = DotMatrixMarginYRatio * 3 / (1 - 2 * DotMatrixMarginYRatio);

    /// <summary>
    /// Draw-area height the dot-matrix core diameter is specified against: a
    /// configured diameter of 10 is exactly 10 px on a 1080p-high draw area
    /// and scales proportionally everywhere else (see <see cref="DotMatrixCoreDiameterPx"/>).
    /// </summary>
    public const double DotMatrixReferenceHeight = 1080.0;

    /// <summary>Number of dots per side for a dot-matrix grid preset.</summary>
    public static int DotMatrixDotCount(DotMatrixGrid grid) => grid switch
    {
        DotMatrixGrid.Grid5x5 => 5,
        DotMatrixGrid.Grid6x6 => 6,
        _ => 4
    };

    /// <summary>
    /// Horizontal spacing change per 列间距 (column-spacing) level, as a
    /// fraction of the reference spacing. The middle level (+3) is the
    /// measured reference layout; each step scales the spacing by 10%, so
    /// the usable range is 0.7×–1.3× — wide enough to feel responsive while
    /// keeping the outer dot centres on-screen even at 6×6.
    /// </summary>
    public const double DotMatrixSpacingStepPerLevel = 0.1;

    /// <summary>
    /// Centre positions of the N×N dot matrix inside a draw area of the given
    /// size, relative to its top-left corner. Pure geometry — no Win32 calls
    /// and no element building — so the layout is unit-testable in isolation.
    ///
    /// Horizontally, <paramref name="spacingLevel"/> scales the reference
    /// column spacing around its middle value (+3 = the measured reference
    /// layout); the edge margins absorb the difference symmetrically, so the
    /// spacing between dots and between the outer columns and the screen
    /// edges always moves together. Vertically, dots are evenly spaced
    /// between margins that keep the reference 4×4 pattern's
    /// margin-to-spacing ratio at every density. The layout follows whatever
    /// rectangle is passed in, so resolution, aspect ratio and window mode
    /// are handled by the caller's draw area.
    /// </summary>
    public static List<Point> ComputeDotMatrixCenters(DotMatrixGrid grid, OffsetLevel spacingLevel, double width, double height)
    {
        var centers = new List<Point>();
        int n = DotMatrixDotCount(grid);
        if (n < 2 || width <= 0 || height <= 0) return centers;

        double refSpacingX = width / (n - 1 + 2 * DotMatrixMarginPerSpacingX);
        double spacingX = refSpacingX * (1 + ((int)spacingLevel - 3) * DotMatrixSpacingStepPerLevel);
        double marginX = (width - (n - 1) * spacingX) / 2;

        double stepY = height / (n - 1 + 2 * DotMatrixMarginPerSpacingY);
        double y0 = DotMatrixMarginPerSpacingY * stepY;

        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col < n; col++)
            {
                centers.Add(new Point(marginX + col * spacingX, y0 + row * stepY));
            }
        }
        return centers;
    }

    /// <summary>
    /// On-screen core diameter for the dot matrix, in canvas DIP: the configured
    /// value is interpreted against a 1080p-high draw area and scaled with the
    /// actual area height, so the grid keeps the same relative dot size at any
    /// resolution (physical px = value × areaHeightPx / 1080, DPI-independent).
    /// </summary>
    public static double DotMatrixCoreDiameterPx(OverlayConfig cfg, double areaHeight)
    {
        double dia = cfg.DotMatrixCoreDiameter * areaHeight / DotMatrixReferenceHeight;
        return Math.Max(dia, 2.0); // never let extreme ratios shrink dots to invisibility
    }

    /// <summary>
    /// Build the static dot-matrix shapes for one draw area. Shares the overlay
    /// colour (the F9 cycle) and the outline settings with the other shapes.
    /// Every dot is a solid, hard-edged disc; the soft glow never alters the
    /// dot body — it only draws a bloom behind the outermost columns' dots.
    /// The halo is one shared brush; every halo dot renders with the identical
    /// gradient, whose inner stop matches the core rim so the bloom starts
    /// seamlessly at the dot edge.
    /// </summary>
    private static void BuildDotMatrix(List<Shape> result, OverlayConfig cfg, Rect area, double dpiRatio, Color color)
    {
        var centers = ComputeDotMatrixCenters(cfg.DotMatrixGrid, cfg.DotMatrixSpacingLevel, area.Width, area.Height);
        if (centers.Count == 0) return;

        int n = DotMatrixDotCount(cfg.DotMatrixGrid);
        byte alpha = OpacityToByte(cfg.Opacity);
        double dia = DotMatrixCoreDiameterPx(cfg, area.Height);

        bool outlineOn = cfg.OverlayOutlineEnabled;
        // The dot matrix keeps its own outline width so the ring can differ
        // per shape (both default to 1px); the toggle and colour are shared.
        double outlineW = outlineOn ? Math.Clamp(cfg.DotMatrixOutlineWidth, 0.5, 5.0) * dpiRatio : 0;
        var outlineColor = cfg.GetOutlineColor();
        var outlineBrush = outlineOn
            ? new SolidColorBrush(Color.FromArgb(alpha, outlineColor.R, outlineColor.G, outlineColor.B))
            : null;

        Brush? haloBrush = null;
        if (cfg.DotMatrixSoftGlow)
        {
            // The glow is a separate backdrop layer that never touches the dot
            // body: the solid core is drawn on top and covers the gradient's
            // centre, and the gradient is dimmer than the dot everywhere the
            // glow is visible — 40% of the dot opacity at the core rim, then
            // a smooth falloff — so the crisp dot always reads on top of the
            // bloom.
            haloBrush = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(alpha, color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb((byte)(alpha * 0.4), color.R, color.G, color.B), 1.0 / 3.0),
                    new GradientStop(Color.FromArgb((byte)(alpha * 0.18), color.R, color.G, color.B), 0.66),
                    new GradientStop(Colors.Transparent, 1)
                }
            };
        }

        var coreBrush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));

        // Halo spans 3× the core diameter (reference: ~6px core, ~20px halo at 1080p).
        double haloDia = dia * 3;

        for (int i = 0; i < centers.Count; i++)
        {
            int col = i % n; // centers are row-major
            bool haloColumn = col == 0 || col == n - 1;

            // Halo first, core on top: the solid disc always covers the
            // gradient's centre, so the glow can never alter the dot itself.
            if (haloBrush != null && haloColumn)
            {
                var halo = new Ellipse { Width = haloDia, Height = haloDia, Fill = haloBrush };
                Canvas.SetLeft(halo, area.X + centers[i].X - haloDia / 2);
                Canvas.SetTop(halo, area.Y + centers[i].Y - haloDia / 2);
                result.Add(halo);
            }

            // Stroke is centred on the rim: the visible ring weight then
            // matches the outline strips (config value = visible line width).
            var core = new Ellipse
            {
                Width = dia,
                Height = dia,
                Fill = coreBrush,
                Stroke = outlineBrush,
                StrokeThickness = outlineBrush != null ? outlineW : 0
            };
            Canvas.SetLeft(core, area.X + centers[i].X - dia / 2);
            Canvas.SetTop(core, area.Y + centers[i].Y - dia / 2);
            result.Add(core);
        }
    }

    /// <summary>Create a filled half-ellipse (semi-elliptical disc).
    /// The flat side is flush with the screen edge at point (edgeX, edgeY).</summary>
    /// <param name="brush">Fill brush</param>
    /// <param name="edgeX">X of the center of the flat side on the screen edge</param>
    /// <param name="edgeY">Y of the center of the flat side on the screen edge</param>
    /// <param name="alongEdgeRadius">Half-length of the flat side (along the screen edge)</param>
    /// <param name="bulgeDepth">How far the arc bulges from the edge (perpendicular, into screen)</param>
    /// <param name="sweep">Arc sweep direction</param>
    /// <param name="isVerticalEdge">True if on a left/right edge (flat side is vertical)</param>
    private static System.Windows.Shapes.Path MakeHalfEllipse(
        Brush brush, Brush? outline, double outlineW,
        double edgeX, double edgeY,
        double alongEdgeRadius, double bulgeDepth,
        SweepDirection sweep, bool isVerticalEdge)
    {
        double ar = Math.Max(alongEdgeRadius, 2);
        double bd = Math.Max(bulgeDepth, 2);
        Point start, end;
        Size arcSize;

        if (!isVerticalEdge)
        {
            // Horizontal edge (top or bottom) — flat side is horizontal
            start = new Point(edgeX - ar, edgeY);
            end = new Point(edgeX + ar, edgeY);
            arcSize = new Size(ar, bd);
        }
        else
        {
            // Vertical edge (left or right) — flat side is vertical
            start = new Point(edgeX, edgeY - ar);
            end = new Point(edgeX, edgeY + ar);
            arcSize = new Size(bd, ar);
        }

        return new System.Windows.Shapes.Path
        {
            Fill = brush,
            Stroke = outline,
            // Strokes are centred on the edge, so use twice the width to expose
            // the same w outside as the rectangle underlay does.
            StrokeThickness = outline != null ? outlineW : 0,
            Data = new PathGeometry
            {
                Figures = { new PathFigure
                {
                    StartPoint = start,
                    IsClosed = true,
                    Segments =
                    {
                        new ArcSegment
                        {
                            Point = end,
                            Size = arcSize,
                            IsLargeArc = false,
                            SweepDirection = sweep
                        }
                    }
                }}
            }
        };
    }

    /// <summary>
    /// Create a filled triangle from three points. With <paramref name="closed"/>
    /// set to false the figure stays open: the fill is still solid (WPF closes
    /// open figures implicitly for filling), but the stroke skips the implicit
    /// base edge — used by the pole tip so no outline seam crosses the joint
    /// where the triangle meets the pole's rectangular body.
    /// </summary>
    private static System.Windows.Shapes.Path MakeTriangle(Brush brush, Brush? outline, double outlineW,
        double x1, double y1, double x2, double y2, double x3, double y3, bool closed = true)
    {
        return new System.Windows.Shapes.Path
        {
            Fill = brush,
            Stroke = outline,
            // Strokes are centred on the edge: the visible band weight then
            // matches the outline strips (config value = visible line width).
            StrokeThickness = outline != null ? outlineW : 0,
            Data = new PathGeometry
            {
                Figures = { new PathFigure
                {
                    StartPoint = new Point(x1, y1),
                    IsClosed = closed,
                    Segments =
                    {
                        new LineSegment(new Point(x2, y2), true),
                        new LineSegment(new Point(x3, y3), true)
                    }
                }}
            }
        };
    }

    // ── Crosshair shape builders ───────────────────────────────────────

    /// <summary>
    /// Build all shapes for the crosshair within the given monitor area.
    /// Shape dimensions are scaled by <paramref name="monDpiScale"/> / <paramref name="windowScale"/>
    /// for correct physical sizing on mixed-DPI multi-monitor setups.
    /// </summary>
    /// <param name="monitorBounds">Monitor bounds in canvas (window-DPI) coordinates.</param>
    /// <param name="windowScale">DPI scale of the WPF overlay window.</param>
    /// <param name="monDpiScale">Per-monitor DPI scale of the target monitor.</param>
    public static List<Shape> BuildCrosshairShapes(CrosshairConfig cfg, Rect monitorBounds,
        double windowScale = 0, double monDpiScale = 0)
    {
        var shapes = new List<Shape>();
        if (!cfg.IsVisible) return shapes;

        var color = cfg.GetColor();
        var brush = new SolidColorBrush(Color.FromArgb(OpacityToByte(cfg.Opacity), color.R, color.G, color.B));

        double dpiRatio = (windowScale > 0 && monDpiScale > 0)
            ? monDpiScale / windowScale
            : 1.0;
        double size = CrosshairSize(cfg.Size) * dpiRatio;
        double thick = CrosshairThickness(cfg.Thickness) * dpiRatio;

        // Outline: flanking bands that sit entirely OUTSIDE every element —
        // the outline colour never lands under the translucent strokes, so the
        // crosshair keeps blending with the game exactly as it does with the
        // outline off (a backdrop copy would turn translucent strokes grey).
        Brush? outlineBrush = null;
        if (cfg.OutlineEnabled)
        {
            var outlineColor = cfg.GetOutlineColor();
            outlineBrush = new SolidColorBrush(
                Color.FromArgb(OpacityToByte(cfg.Opacity), outlineColor.R, outlineColor.G, outlineColor.B));
        }
        double outlineW = cfg.OutlineEnabled
            ? Math.Clamp(cfg.OutlineWidth, 0.1, 3.0) * dpiRatio
            : 0;

        double monW = monitorBounds.Width;
        double monH = monitorBounds.Height;

        // Apply aspect ratio safe area within this monitor
        var safe = GetSafeArea(monW, monH, cfg.AspectRatio);
        double safeCx = monitorBounds.X + safe.X + safe.Width / 2;
        double safeCy = monitorBounds.Y + safe.Y + safe.Height / 2;

        // Center + offset
        double cx = safeCx + cfg.PositionX;
        double cy = safeCy + cfg.PositionY;

        var positions = new List<(double px, double py)>();
        if (cfg.Split == SplitScreen.Vertical)
        {
            double halfW = safe.Width / 2;
            positions.Add((monitorBounds.X + safe.X + halfW / 2 + cfg.PositionX, cy));
            positions.Add((monitorBounds.X + safe.X + halfW + halfW / 2 + cfg.PositionX, cy));
        }
        else if (cfg.Split == SplitScreen.Horizontal)
        {
            double halfH = safe.Height / 2;
            positions.Add((cx, monitorBounds.Y + safe.Y + halfH / 2 + cfg.PositionY));
            positions.Add((cx, monitorBounds.Y + safe.Y + halfH + halfH / 2 + cfg.PositionY));
        }
        else
        {
            positions.Add((cx, cy));
        }

        foreach (var (px, py) in positions)
        {
            // Flanking outline bands first (purely outside the strokes), then
            // the coloured crosshair on top.
            if (outlineBrush != null)
            {
                switch (cfg.Shape)
                {
                    case CrosshairShape.Circle:
                        shapes.Add(FlankingRingOutline(px, py, size / 2 - thick / 2, size / 2 + thick / 2, outlineW, outlineBrush));
                        break;
                    case CrosshairShape.Cross:
                        shapes.Add(FlankingCrossOutline(px, py, thick / 2, size / 2, outlineW, outlineBrush));
                        break;
                    case CrosshairShape.Diamond:
                        shapes.Add(FlankingDiamondOutline(px, py, size / 2, outlineW, outlineBrush));
                        break;
                }
            }
            shapes.AddRange(BuildSingleCrosshair(cfg.Shape, px, py, size, thick, brush));
        }

        return shapes;
    }

    // ── Crosshair outline geometry ──
    // Each helper builds a band of the given width that flanks a crosshair
    // element without covering it, so the translucent strokes keep rendering
    // exactly as they do with the outline off.

    /// <summary>Band around a ring: four concentric ellipses filled EvenOdd.
    /// <paramref name="rInner"/>/<paramref name="rOuter"/> are the coloured
    /// ring's inner/outer edges; the band flanks them inside and out.</summary>
    private static Shape FlankingRingOutline(double cx, double cy, double rInner, double rOuter, double band, Brush brush)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        foreach (var r in new[] { rOuter + band, rOuter, rInner, rInner - band })
        {
            if (r > 0)
                group.Children.Add(new EllipseGeometry(new Point(cx, cy), r, r));
        }
        return new System.Windows.Shapes.Path { Data = group, Fill = brush };
    }

    /// <summary>Band around a plus-shaped cross: the dilated 12-gon minus the
    /// cross itself, filled EvenOdd — the band flanks both arms and their
    /// ends without entering them.</summary>
    private static Shape FlankingCrossOutline(double cx, double cy, double armHalf, double lenHalf, double band, Brush brush)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(CrossPolygonGeometry(cx, cy, armHalf + band, lenHalf + band));
        group.Children.Add(CrossPolygonGeometry(cx, cy, armHalf, lenHalf));
        return new System.Windows.Shapes.Path { Data = group, Fill = brush };
    }

    /// <summary>Band around a diamond: the dilated rhombus minus the rhombus
    /// itself, filled EvenOdd. Dilating the edges outward by <paramref
    /// name="band"/> scales the half-diagonals by 1 + band·√2 / half.</summary>
    private static Shape FlankingDiamondOutline(double cx, double cy, double half, double band, Brush brush)
    {
        double scale = 1 + band * Math.Sqrt(2) / half;
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(RhombusGeometry(cx, cy, half * scale));
        group.Children.Add(RhombusGeometry(cx, cy, half));
        return new System.Windows.Shapes.Path { Data = group, Fill = brush };
    }

    private static Geometry CrossPolygonGeometry(double cx, double cy, double armHalf, double lenHalf)
    {
        var fig = new PathFigure { StartPoint = new Point(cx - lenHalf, cy - armHalf), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(cx - armHalf, cy - armHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx - armHalf, cy - lenHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx + armHalf, cy - lenHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx + armHalf, cy - armHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx + lenHalf, cy - armHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx + lenHalf, cy + armHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx + armHalf, cy + armHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx + armHalf, cy + lenHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx - armHalf, cy + lenHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx - armHalf, cy + armHalf), true));
        fig.Segments.Add(new LineSegment(new Point(cx - lenHalf, cy + armHalf), true));
        var geo = new PathGeometry { FillRule = FillRule.EvenOdd };
        geo.Figures.Add(fig);
        return geo;
    }

    private static Geometry RhombusGeometry(double cx, double cy, double half)
    {
        var fig = new PathFigure { StartPoint = new Point(cx, cy - half), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(cx + half, cy), true));
        fig.Segments.Add(new LineSegment(new Point(cx, cy + half), true));
        fig.Segments.Add(new LineSegment(new Point(cx - half, cy), true));
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return geo;
    }

    private static List<Shape> BuildSingleCrosshair(CrosshairShape shape, double cx, double cy, double size, double thick, Brush brush)
    {
        var result = new List<Shape>();
        double half = size / 2;

        switch (shape)
        {
            case CrosshairShape.Circle:
                var circle = new Ellipse
                {
                    Stroke = brush,
                    StrokeThickness = thick,
                    Width = size,
                    Height = size
                };
                Canvas.SetLeft(circle, cx - half);
                Canvas.SetTop(circle, cy - half);
                result.Add(circle);
                // Centre dot only while the ring's inner hole can hold it —
                // once the ring collapses around the centre (very thick ring
                // on a small size) the hole is already filled by the ring's
                // inner edge, and stacking the translucent dot there would
                // brighten the centre.
                if (thick / 2 <= half - thick / 2)
                {
                    var dot = new Ellipse
                    {
                        Fill = brush,
                        Width = Math.Max(1, thick),
                        Height = Math.Max(1, thick)
                    };
                    Canvas.SetLeft(dot, cx - thick / 2);
                    Canvas.SetTop(dot, cy - thick / 2);
                    result.Add(dot);
                }
                break;

            case CrosshairShape.Cross:
                // One filled 12-gon instead of two overlapping translucent lines
                // plus a centre dot: overlapping translucent shapes stack their
                // alpha, which rendered the centre brighter than the arms at
                // low opacity. A single polygon keeps the opacity uniform.
                result.Add(new System.Windows.Shapes.Path
                {
                    Fill = brush,
                    Data = CrossPolygonGeometry(cx, cy, thick / 2, half)
                });
                break;

            case CrosshairShape.Diamond:
                // Diamond with stroked segments — isStroked must be true
                var diamond = new System.Windows.Shapes.Path
                {
                    Stroke = brush,
                    StrokeThickness = thick,
                    Data = new PathGeometry
                    {
                        Figures =
                        {
                            new PathFigure
                            {
                                StartPoint = new Point(cx, cy - half),
                                IsClosed = true,
                                Segments =
                                {
                                    new LineSegment(new Point(cx + half, cy), true),
                                    new LineSegment(new Point(cx, cy + half), true),
                                    new LineSegment(new Point(cx - half, cy), true)
                                }
                            }
                        }
                    }
                };
                result.Add(diamond);
                break;
        }
        return result;
    }

    // ── Utility ────────────────────────────────────────────────────────

    /// <summary>
    /// Convert a 0-100 opacity percentage to a 0-255 alpha byte.
    /// </summary>
    public static byte OpacityToByte(int opacity)
    {
        opacity = Math.Clamp(opacity, 0, 100);
        return (byte)(opacity * 255 / 100);
    }

    private static Line CreateLine(double x1, double y1, double x2, double y2, double thick, Brush brush)
    {
        return new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            StrokeThickness = thick,
            Stroke = brush
        };
    }
}
