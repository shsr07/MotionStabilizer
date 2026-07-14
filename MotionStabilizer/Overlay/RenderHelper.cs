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

    // ── Overlay shape builders ─────────────────────────────────────────

    /// <summary>Build all shapes for the edge overlay within the given area.</summary>
    public static List<Shape> BuildOverlayShapes(OverlayConfig cfg, Rect area, double screenW, double screenH)
    {
        var shapes = new List<Shape>();
        var color = cfg.GetColor();
        var brush = new SolidColorBrush(Color.FromArgb(OpacityToByte(cfg.Opacity), color.R, color.G, color.B));
        double sizePx = OverlayBarWidth(cfg.Size);
        double lengthPx = LengthOffsetPx(cfg.Length);

        // Apply aspect ratio safe area
        var safe = GetSafeArea(screenW, screenH, cfg.AspectRatio);

        // In Window mode, detect the foreground window (the active game) and follow its bounds.
        // In Stretch mode, use the full screen safe area.
        Rect drawArea;
        if (cfg.Mode == DisplayMode.Window)
        {
            var fwRect = Win32Interop.GetForegroundWindowRect();
            if (fwRect.HasValue)
            {
                var r = fwRect.Value;
                drawArea = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            }
            else
            {
                drawArea = safe;
            }
        }
        else
        {
            drawArea = safe;
        }

        if (cfg.Split == SplitScreen.Vertical)
        {
            double halfW = drawArea.Width / 2;
            var leftRect = new Rect(drawArea.X, drawArea.Y, halfW, drawArea.Height);
            var rightRect = new Rect(drawArea.X + halfW, drawArea.Y, halfW, drawArea.Height);
            shapes.AddRange(BuildSingleOverlay(cfg.Shape, leftRect, sizePx, lengthPx, brush));
            shapes.AddRange(BuildSingleOverlay(cfg.Shape, rightRect, sizePx, lengthPx, brush));
        }
        else if (cfg.Split == SplitScreen.Horizontal)
        {
            double halfH = drawArea.Height / 2;
            var topRect = new Rect(drawArea.X, drawArea.Y, drawArea.Width, halfH);
            var botRect = new Rect(drawArea.X, drawArea.Y + halfH, drawArea.Width, halfH);
            shapes.AddRange(BuildSingleOverlay(cfg.Shape, topRect, sizePx, lengthPx, brush));
            shapes.AddRange(BuildSingleOverlay(cfg.Shape, botRect, sizePx, lengthPx, brush));
        }
        else
        {
            shapes.AddRange(BuildSingleOverlay(cfg.Shape, drawArea, sizePx, lengthPx, brush));
        }

        return shapes;
    }

    private static List<Shape> BuildSingleOverlay(OverlayShape shape, Rect area, double sizePx, double lengthPx, Brush brush)
    {
        var result = new List<Shape>();
        double x = area.X;
        double y = area.Y;
        double w = area.Width;
        double h = area.Height;
        double cx = x + w / 2;
        double cy = y + h / 2;

        switch (shape)
        {
            case OverlayShape.Box:
                // ── Four centered rectangular bars, one on each edge ──
                // Size  → thickness (perpendicular to edge)
                // Length → length along edge
                double boxThick = Math.Max(sizePx, 2);
                double boxLen = Math.Max(sizePx * 2 + lengthPx * 8, boxThick * 2);

                // Top edge — horizontal rectangle centered on top
                var topRect = new Rectangle { Fill = brush, Width = boxLen, Height = boxThick };
                Canvas.SetLeft(topRect, cx - boxLen / 2);
                Canvas.SetTop(topRect, y);
                result.Add(topRect);

                // Bottom edge — horizontal rectangle centered on bottom
                var botRect = new Rectangle { Fill = brush, Width = boxLen, Height = boxThick };
                Canvas.SetLeft(botRect, cx - boxLen / 2);
                Canvas.SetTop(botRect, y + h - boxThick);
                result.Add(botRect);

                // Left edge — vertical rectangle centered on left
                var leftRect = new Rectangle { Fill = brush, Width = boxThick, Height = boxLen };
                Canvas.SetLeft(leftRect, x);
                Canvas.SetTop(leftRect, cy - boxLen / 2);
                result.Add(leftRect);

                // Right edge — vertical rectangle centered on right
                var rightRect = new Rectangle { Fill = brush, Width = boxThick, Height = boxLen };
                Canvas.SetLeft(rightRect, x + w - boxThick);
                Canvas.SetTop(rightRect, cy - boxLen / 2);
                result.Add(rightRect);
                break;

            case OverlayShape.Dome:
                // ── Four half-ellipses, one centered on each edge ──
                // Size   → bulge depth (perpendicular to edge, into screen)
                // Length → width along edge (flat side length)
                double domeDepth = Math.Max(sizePx, 8);
                double domeWidth = Math.Max(sizePx + lengthPx * 5, domeDepth);

                // Top edge — half-ellipse bulging downward (into screen)
                result.Add(MakeHalfEllipse(brush, cx, y, domeWidth, domeDepth,
                    SweepDirection.Counterclockwise, false));

                // Bottom edge — half-ellipse bulging upward (into screen)
                result.Add(MakeHalfEllipse(brush, cx, y + h, domeWidth, domeDepth,
                    SweepDirection.Clockwise, false));

                // Left edge — half-ellipse bulging rightward (into screen)
                result.Add(MakeHalfEllipse(brush, x, cy, domeWidth, domeDepth,
                    SweepDirection.Clockwise, true));

                // Right edge — half-ellipse bulging leftward (into screen)
                result.Add(MakeHalfEllipse(brush, x + w, cy, domeWidth, domeDepth,
                    SweepDirection.Counterclockwise, true));
                break;

            case OverlayShape.Flag:
                // ── Four isosceles triangles at each edge center, apex pointing to screen center ──
                // Size   → height (perpendicular to edge, into screen)
                // Length → base width (along edge)
                double triHeight = Math.Max(sizePx * 4, 60);
                double triBase = Math.Max(sizePx * 2.5 + lengthPx * 8, 40);

                // Top edge triangle — base on top edge, apex pointing down toward center
                result.Add(MakeTriangle(brush,
                    cx - triBase / 2, y,
                    cx + triBase / 2, y,
                    cx, y + triHeight));

                // Bottom edge triangle — base on bottom edge, apex pointing up toward center
                result.Add(MakeTriangle(brush,
                    cx - triBase / 2, y + h,
                    cx + triBase / 2, y + h,
                    cx, y + h - triHeight));

                // Left edge triangle — base on left edge, apex pointing right toward center
                result.Add(MakeTriangle(brush,
                    x, cy - triBase / 2,
                    x, cy + triBase / 2,
                    x + triHeight, cy));

                // Right edge triangle — base on right edge, apex pointing left toward center
                result.Add(MakeTriangle(brush,
                    x + w, cy - triBase / 2,
                    x + w, cy + triBase / 2,
                    x + w - triHeight, cy));
                break;
        }
        return result;
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
        Brush brush, double edgeX, double edgeY,
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

    /// <summary>Create a filled triangle from three points.</summary>
    private static System.Windows.Shapes.Path MakeTriangle(Brush brush,
        double x1, double y1, double x2, double y2, double x3, double y3)
    {
        return new System.Windows.Shapes.Path
        {
            Fill = brush,
            Data = new PathGeometry
            {
                Figures = { new PathFigure
                {
                    StartPoint = new Point(x1, y1),
                    IsClosed = true,
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

    /// <summary>Build all shapes for the crosshair at center + offset.</summary>
    public static List<Shape> BuildCrosshairShapes(CrosshairConfig cfg, double screenW, double screenH)
    {
        var shapes = new List<Shape>();
        if (!cfg.IsVisible) return shapes;

        var color = cfg.GetColor();
        var brush = new SolidColorBrush(Color.FromArgb(OpacityToByte(cfg.Opacity), color.R, color.G, color.B));
        double size = CrosshairSize(cfg.Size);
        double thick = CrosshairThickness(cfg.Thickness);

        // Center + offset
        double cx = screenW / 2 + cfg.PositionX;
        double cy = screenH / 2 + cfg.PositionY;

        // Apply aspect ratio adjustment for position
        if (cfg.AspectRatio == AspectRatio.Ratio21x9)
        {
            var safe = GetSafeArea(screenW, screenH, cfg.AspectRatio);
            cx = safe.X + safe.Width / 2 + cfg.PositionX;
            cy = safe.Y + safe.Height / 2 + cfg.PositionY;
        }

        var positions = new List<(double px, double py)>();
        if (cfg.Split == SplitScreen.Vertical)
        {
            positions.Add((screenW / 4 + cfg.PositionX, cy));
            positions.Add((screenW * 3 / 4 + cfg.PositionX, cy));
        }
        else if (cfg.Split == SplitScreen.Horizontal)
        {
            positions.Add((cx, screenH / 4 + cfg.PositionY));
            positions.Add((cx, screenH * 3 / 4 + cfg.PositionY));
        }
        else
        {
            positions.Add((cx, cy));
        }

        foreach (var (px, py) in positions)
        {
            shapes.AddRange(BuildSingleCrosshair(cfg.Shape, px, py, size, thick, brush));
        }

        return shapes;
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
                var dot = new Ellipse
                {
                    Fill = brush,
                    Width = Math.Max(1, thick),
                    Height = Math.Max(1, thick)
                };
                Canvas.SetLeft(dot, cx - thick / 2);
                Canvas.SetTop(dot, cy - thick / 2);
                result.Add(dot);
                break;

            case CrosshairShape.Cross:
                result.Add(CreateLine(cx - half, cy, cx + half, cy, thick, brush));
                result.Add(CreateLine(cx, cy - half, cx, cy + half, thick, brush));
                var cdot = new Ellipse
                {
                    Fill = brush,
                    Width = Math.Max(2, thick),
                    Height = Math.Max(2, thick)
                };
                Canvas.SetLeft(cdot, cx - thick / 2);
                Canvas.SetTop(cdot, cy - thick / 2);
                result.Add(cdot);
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
