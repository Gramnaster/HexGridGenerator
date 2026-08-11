using System.Drawing;
using HexGrid.Core.Layout;
using HexGrid.Core.Units;

namespace HexGrid.Core.Scene;

/// <summary>Turns settings plus a solved <see cref="GridLayout"/> into layered draw items.</summary>
public static class SceneBuilder
{
    public static DrawScene Build(GridSettings s, GridLayout layout)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(layout);

        var scale = new UnitScale(s.Unit, s.Dpi);
        var scene = new DrawScene
        {
            WidthPx = layout.CanvasWidthPx,
            HeightPx = layout.CanvasHeightPx,
            WidthMm = layout.CanvasWidthMm,
            HeightMm = layout.CanvasHeightMm,
            Dpi = s.Dpi,
            ClipBounds = layout.ClipBounds,
            GridType = s.GridType,
        };

        AddCells(scene, s, layout, scale);
        AddCenterDots(scene, s, layout, scale);
        AddCellLabels(scene, s, layout, scale);
        AddEdgeLabels(scene, s, layout, scale);
        AddFrame(scene, s, layout, scale);

        return scene;
    }

    // -------------------------------------------------------------------- cells

    private static void AddCells(DrawScene scene, GridSettings s, GridLayout layout, UnitScale scale)
    {
        Color stroke = Paint.WithOpacity(s.LineColor, s.LineOpacity);
        Color fill = Paint.WithOpacity(s.HexFillColor, s.HexFillOpacity);
        double thickness = scale.ToPx(s.LineThickness);

        if (!Paint.IsInvisible(fill))
        {
            SceneLayer fillLayer = scene.Layer(LayerKind.HexFill);
            foreach (GridCell cell in layout.Cells)
            {
                fillLayer.Items.Add(new PathItem(cell.Vertices, Closed: true, Stroke: null, 0, fill));
            }
        }

        if (!Paint.IsInvisible(stroke) && thickness > 0)
        {
            SceneLayer gridLayer = scene.Layer(LayerKind.HexGrid);

            // Adjacent cells share an edge. Drawing whole polygons would stroke every internal edge
            // twice, which at less than full opacity makes internal edges darker than the outer ones
            // and thickens them under antialiasing. Emit each edge exactly once instead.
            var seen = new HashSet<(long, long, long, long)>(layout.Cells.Count * 3);

            // S3267 asks for this to become a LINQ Select/Where chain. Deliberately not applied:
            // this runs on every live-preview rebuild over every cell x its edges (thousands of
            // iterations on large grids), and the CSharp/CLAUDE.md performance doctrine is explicit
            // that LINQ is avoided on hot paths because of enumerator/closure allocation. S3267 also
            // has a documented history of false positives on loops with a side-effecting filter
            // (seen.Add doubles as membership test and record, which HashSet<T> has no LINQ-idiomatic
            // equivalent for without materializing an intermediate sequence). See
            // https://github.com/SonarSource/sonar-dotnet/issues/8356 and the Sonar Community thread
            // "False positives on S3267: These loops can not be simplified using LINQ".
#pragma warning disable S3267
            foreach (GridCell cell in layout.Cells)
            {
                int vertexCount = cell.Vertices.Length;
                for (int i = 0; i < vertexCount; i++)
                {
                    PointF a = cell.Vertices[i];
                    PointF b = cell.Vertices[(i + 1) % vertexCount];
                    if (seen.Add(EdgeKey(a, b)))
                    {
                        gridLayer.Items.Add(new PathItem([a, b], Closed: false, stroke, thickness, Fill: null));
                    }
                }
            }
#pragma warning restore S3267
        }
    }

    /// <summary>
    /// Direction-independent identity for an edge, quantised to a tenth of a pixel so the two hexes
    /// sharing it agree despite independently computed trigonometry.
    /// </summary>
    private static (long, long, long, long) EdgeKey(PointF a, PointF b)
    {
        long ax = Q(a.X), ay = Q(a.Y), bx = Q(b.X), by = Q(b.Y);
        return ax < bx || (ax == bx && ay <= by) ? (ax, ay, bx, by) : (bx, by, ax, ay);

        // MA0193: explicit mode for consistency with the rest of the codebase. It's inconsequential
        // here specifically: both hexes sharing this edge quantize independently, and real trig
        // output essentially never lands on an exact tie at one-tenth-of-a-pixel granularity, so
        // ToEven vs AwayFromZero doesn't change whether the two sides agree.
        static long Q(float v) => (long)Math.Round(v * 10.0, MidpointRounding.AwayFromZero);
    }

    private static void AddCenterDots(DrawScene scene, GridSettings s, GridLayout layout, UnitScale scale)
    {
        if (!s.ShowCenterDots)
        {
            return;
        }

        Color dot = Paint.WithOpacity(s.DotColor, s.DotOpacity);
        double radius = scale.ToPx(s.DotRadius);
        if (Paint.IsInvisible(dot) || radius <= 0)
        {
            return;
        }

        SceneLayer layer = scene.Layer(LayerKind.CenterDots);
        foreach (GridCell cell in layout.Cells)
        {
            layer.Items.Add(new CircleItem(cell.Center, radius, dot));
        }
    }

    private static void AddCellLabels(DrawScene scene, GridSettings s, GridLayout layout, UnitScale scale)
    {
        if (!s.ShowHexLabels)
        {
            return;
        }

        Color color = Paint.WithOpacity(s.HexLabelColor, s.HexLabelOpacity);
        double fontPx = scale.PointsToPx(s.HexLabelFontSize);
        if (Paint.IsInvisible(color) || fontPx <= 0)
        {
            return;
        }

        SceneLayer layer = scene.Layer(LayerKind.HexLabels);

        // HexLabelMargin is the gap from the label to the near edge, not from the centre, so the
        // inset from centre is the remaining distance after that gap is subtracted from the half-height.
        double inset = layout.CellHeightPx * (0.5 - (Math.Clamp(s.HexLabelMargin, 0, 50) / 100.0));

        // Centred labels would sit right on top of the centre dot and become unreadable, so when a
        // dot is present the label is lifted to rest just clear of it. With no dot there is nothing
        // to avoid and the label is centred properly.
        double dotClearance = s.ShowCenterDots ? scale.ToPx(s.DotRadius) + (fontPx * 0.2) : 0;

        foreach (GridCell cell in layout.Cells)
        {
            (float y, TextBaseline baseline) = s.HexLabelPosition switch
            {
                HexLabelPosition.Top => ((float)(cell.Center.Y - inset), TextBaseline.Top),
                HexLabelPosition.Bottom => ((float)(cell.Center.Y + inset), TextBaseline.Bottom),
                HexLabelPosition.Center when dotClearance > 0 => ((float)(cell.Center.Y - dotClearance), TextBaseline.Bottom),
                HexLabelPosition.Center => (cell.Center.Y, TextBaseline.Middle),
                _ when dotClearance > 0 => ((float)(cell.Center.Y - dotClearance), TextBaseline.Bottom),
                _ => (cell.Center.Y, TextBaseline.Middle),
            };

            layer.Items.Add(new TextItem(
                cell.Label,
                new PointF(cell.Center.X, y),
                TextAnchor.Middle,
                baseline,
                s.FontFamily,
                fontPx,
                s.HexLabelBold,
                color));
        }
    }

    // ------------------------------------------------------------- edge labels

    /// <summary>
    /// Coordinate labels sit in the band OUTSIDE the frame rule, aligned to the column and row centres.
    /// </summary>
    private static void AddEdgeLabels(DrawScene scene, GridSettings s, GridLayout layout, UnitScale scale)
    {
        if (s.MarginalLabelSides == LabelSides.None)
        {
            return;
        }

        Color color = s.MarginalColor;
        double fontPx = scale.PointsToPx(s.MarginalFontSize);
        double pad = scale.ToPx(s.LabelPadding);
        double half = (s.BorderStyle == MapBorderStyle.None ? 0 : scale.ToPx(s.BorderThickness)) / 2.0;
        if (Paint.IsInvisible(color) || fontPx <= 0)
        {
            return;
        }

        SceneLayer layer = scene.Layer(LayerKind.EdgeLabels);
        RectangleF f = layout.FrameBounds;
        double gap = half + pad;

        void Emit(string text, double x, double y, TextAnchor anchor, TextBaseline baseline) =>
            layer.Items.Add(new TextItem(
                text, new PointF((float)x, (float)y), anchor, baseline,
                s.FontFamily, fontPx, s.MarginalBold, color));

        if (s.MarginalLabelSides.HasFlag(LabelSides.Top))
        {
            for (int c = 0; c < layout.Columns; c++)
            {
                Emit(layout.ColumnLabels[c], layout.ColumnCenterXs[c], f.Top - gap, TextAnchor.Middle, TextBaseline.Bottom);
            }
        }

        if (s.MarginalLabelSides.HasFlag(LabelSides.Bottom))
        {
            for (int c = 0; c < layout.Columns; c++)
            {
                Emit(layout.ColumnLabels[c], layout.ColumnCenterXs[c], f.Bottom + gap, TextAnchor.Middle, TextBaseline.Top);
            }
        }

        if (s.MarginalLabelSides.HasFlag(LabelSides.Left))
        {
            for (int r = 0; r < layout.Rows; r++)
            {
                Emit(layout.RowLabels[r], f.Left - gap, layout.RowCenterYs[r], TextAnchor.End, TextBaseline.Middle);
            }
        }

        if (s.MarginalLabelSides.HasFlag(LabelSides.Right))
        {
            for (int r = 0; r < layout.Rows; r++)
            {
                Emit(layout.RowLabels[r], f.Right + gap, layout.RowCenterYs[r], TextAnchor.Start, TextBaseline.Middle);
            }
        }
    }

    // ------------------------------------------------------------------- frame

    private static void AddFrame(DrawScene scene, GridSettings s, GridLayout layout, UnitScale scale)
    {
        if (s.BorderStyle == MapBorderStyle.None)
        {
            return;
        }

        double t = scale.ToPx(s.BorderThickness);
        if (t <= 0 || Paint.IsInvisible(s.BorderColor))
        {
            return;
        }

        // Drawn last of the map elements, so its inner half covers the clipped hex edges.
        scene.Layer(LayerKind.Border).Items.Add(new RectItem(layout.FrameBounds, s.BorderColor, t, Fill: null));
    }

}
