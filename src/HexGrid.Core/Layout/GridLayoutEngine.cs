using System.Drawing;
using HexGrid.Core.Labels;
using HexGrid.Core.Units;

namespace HexGrid.Core.Layout;

/// <summary>
/// Solves the grid geometry for a set of settings. Knows nothing about SVG, GDI+ or any other
/// renderer. Owns the canvas/frame/label-gutter framing shared by every grid type, and dispatches
/// the shape-specific sizing and cell placement to <see cref="HexLayoutEngine"/> or
/// <see cref="SquareLayoutEngine"/>.
/// </summary>
/// <remarks>
/// Working outward from the canvas edge: safe margin, coordinate-label band, frame rule, map area.
/// Hex grids always fill the map area (every hex centre lands inside it and the outermost hexes
/// overhang and get clipped), so the grid meets the frame on all four sides with no gap. Square
/// grids do the same unless <see cref="GridSettings.AutoFitSquares"/> is on, in which case whole
/// squares are centred in the map area and the leftover slack becomes an even margin instead.
/// </remarks>
public static class GridLayoutEngine
{
    /// <summary>Rough advance width of one glyph as a fraction of the font size, for gutter reservation.</summary>
    private const double AverageGlyphWidthRatio = 0.62;

    /// <summary>Cap-height-ish line box as a fraction of the font size.</summary>
    private const double LineHeightRatio = 1.0;

    public static GridLayout Build(GridSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var scale = new UnitScale(s.Unit, s.Dpi);
        (double canvasWpx, double canvasHpx, double canvasWmm, double canvasHmm) = ResolveCanvas(s, scale);

        double safePx = Math.Max(0, scale.ToPx(s.SafeMargin));
        RectangleF safeRect = Deflate(
            new RectangleF(0, 0, (float)canvasWpx, (float)canvasHpx), safePx, safePx, safePx, safePx);

        if (safeRect.Width <= 0 || safeRect.Height <= 0)
        {
            throw new InvalidOperationException("The safe margin consumes the whole canvas. Reduce it or enlarge the canvas.");
        }

        (int columns, int rows, double sizePx, double? radiusPx, RectangleF frameBounds, RectangleF clip) =
            SolveGrid(s, scale, safeRect);

        (string[] columnLabels, string[] rowLabelsFinal) = CoordinateLabeller.BuildAxes(
            columns, rows, s.LabelScheme, s.CoordinateOrigin, s.SkipLettersIO, s.ZeroPadNumbers);

        (IReadOnlyList<GridCell> cells, double[] columnCenterXs, double[] rowCenterYs, RectangleF gridBounds, double cellWidthPx, double cellHeightPx) =
            BuildCells(s, scale, columns, rows, sizePx, radiusPx, clip, columnLabels, rowLabelsFinal, s.CoordinateSeparator);

        double insetPx = Math.Max(0, scale.ToPx(s.GridInset));
        (frameBounds, clip) = ShrinkFrameToFlushedGrid(s, insetPx, frameBounds, clip, gridBounds);

        return new GridLayout
        {
            CanvasWidthPx = canvasWpx,
            CanvasHeightPx = canvasHpx,
            CanvasWidthMm = canvasWmm,
            CanvasHeightMm = canvasHmm,
            Columns = columns,
            Rows = rows,
            CellRadiusPx = radiusPx,
            CellWidthPx = cellWidthPx,
            CellHeightPx = cellHeightPx,
            FrameBounds = frameBounds,
            ClipBounds = clip,
            GridBounds = gridBounds,
            Cells = cells,
            ColumnCenterXs = columnCenterXs,
            RowCenterYs = rowCenterYs,
            ColumnLabels = columnLabels,
            RowLabels = rowLabelsFinal,
        };
    }

    /// <summary>
    /// Solves columns, rows, cell size and the frame/clip rectangles by converging on the label
    /// gutter width. The label gutter depends on how many characters the labels run to, which
    /// depends on the row and column counts, which depend on the gutter. Three passes converge.
    /// </summary>
    private static (int Columns, int Rows, double SizePx, double? RadiusPx, RectangleF FrameBounds, RectangleF ClipBounds) SolveGrid(
        GridSettings s, UnitScale scale, RectangleF safeRect)
    {
        double framePx = s.BorderStyle == MapBorderStyle.None ? 0 : Math.Max(0, scale.ToPx(s.BorderThickness));
        double insetPx = Math.Max(0, scale.ToPx(s.GridInset));
        double labelPadPx = Math.Max(0, scale.ToPx(s.LabelPadding));
        double marginalFontPx = scale.PointsToPx(s.MarginalFontSize);

        int columns = Math.Max(1, s.Columns);
        int rows = Math.Max(1, s.Rows);
        double sizePx = 0;
        double? radiusPx = null;
        RectangleF frameBounds = safeRect;
        RectangleF clip = safeRect;

        for (int pass = 0; pass < 3; pass++)
        {
            (string[] colLabels, string[] rowLabels) = CoordinateLabeller.BuildAxes(
                columns, rows, s.LabelScheme, s.CoordinateOrigin, s.SkipLettersIO, s.ZeroPadNumbers);
            (_, int rowChars) = CoordinateLabeller.MaxLabelLengths(colLabels, rowLabels);

            double horizontal = labelPadPx + (rowChars * marginalFontPx * AverageGlyphWidthRatio);
            double vertical = labelPadPx + (marginalFontPx * LineHeightRatio);

            frameBounds = ComputeFrameBounds(s, safeRect, framePx, horizontal, vertical);
            clip = Deflate(frameBounds, insetPx, insetPx, insetPx, insetPx);

            if (clip.Width <= 0 || clip.Height <= 0)
            {
                throw new InvalidOperationException(
                    "The margins, frame and edge labels leave no room for the grid. Reduce the label font size, padding or margins.");
            }

            double widthPx;
            (columns, rows, widthPx, _, radiusPx) = s.GridType switch
            {
                GridType.Square => SquareLayoutEngine.Solve(s, scale, clip),
                _ => HexLayoutEngine.Solve(s, scale, clip),
            };
            sizePx = widthPx;
        }

        return (columns, rows, sizePx, radiusPx, frameBounds, clip);
    }

    private static (IReadOnlyList<GridCell> Cells, double[] ColumnCenterXs, double[] RowCenterYs, RectangleF GridBounds, double CellWidthPx, double CellHeightPx) BuildCells(
        GridSettings s, UnitScale scale, int columns, int rows, double sizePx, double? radiusPx, RectangleF clip,
        string[] columnLabels, string[] rowLabels, string separator)
    {
        if (s.GridType == GridType.Square)
        {
            var (cells, columnCenterXs, rowCenterYs, gridBounds) =
                SquareLayoutEngine.BuildCells(s, scale, columns, rows, sizePx, clip, columnLabels, rowLabels, separator);
            return (cells, columnCenterXs, rowCenterYs, gridBounds, sizePx, sizePx);
        }

        bool flat = s.HexOrientation == HexOrientation.FlatTop;
        double resolvedRadiusPx = radiusPx!.Value;
        var (hexCells, hexColumnCenterXs, hexRowCenterYs, hexGridBounds) =
            HexLayoutEngine.BuildCells(s, scale, columns, rows, resolvedRadiusPx, clip, columnLabels, rowLabels, separator);
        double heightPx = flat ? Math.Sqrt(3.0) * resolvedRadiusPx : 2 * resolvedRadiusPx;
        return (hexCells, hexColumnCenterXs, hexRowCenterYs, hexGridBounds, sizePx, heightPx);
    }

    /// <summary>Deflates the safe-margin rect by the frame rule and whichever edge-label gutters are enabled.</summary>
    private static RectangleF ComputeFrameBounds(
        GridSettings s, RectangleF safeRect, double framePx, double horizontal, double vertical)
    {
        double half = framePx / 2.0;
        return Deflate(
            safeRect,
            (s.MarginalLabelSides.HasFlag(LabelSides.Left) ? horizontal : 0) + half,
            (s.MarginalLabelSides.HasFlag(LabelSides.Top) ? vertical : 0) + half,
            (s.MarginalLabelSides.HasFlag(LabelSides.Right) ? horizontal : 0) + half,
            (s.MarginalLabelSides.HasFlag(LabelSides.Bottom) ? vertical : 0) + half);
    }

    /// <summary>
    /// AutoFitSquares + FlushAxis pushes the grid's leftover slack entirely to the side away from
    /// CoordinateOrigin (see SquareLayoutEngine.ResolveBlockOrigin), but SolveGrid sizes the frame
    /// for the nominal map area, not the grid's actual footprint - so that leftover still shows up
    /// as dead space between the grid and the frame rule. This re-derives the map area (clip) on the
    /// flushed-away side to touch the grid exactly, then re-inflates the frame from that using the
    /// same clip-to-frame relationship SolveGrid used, just applied to the real footprint instead of
    /// the nominal one. The border and the edge-label band (both driven by FrameBounds) then hug the
    /// actual grid with exactly GridInset of space, not the leftover. Hex grids and squares that
    /// aren't both AutoFitSquares and flushed are returned unchanged.
    /// </summary>
    private static (RectangleF FrameBounds, RectangleF ClipBounds) ShrinkFrameToFlushedGrid(
        GridSettings s, double insetPx, RectangleF frameBounds, RectangleF clip, RectangleF gridBounds)
    {
        if (s.GridType != GridType.Square || !s.AutoFitSquares || s.FlushAxis == FlushAxis.None)
        {
            return (frameBounds, clip);
        }

        bool originLeft = s.CoordinateOrigin is CoordinateOrigin.TopLeft or CoordinateOrigin.BottomLeft;
        bool originTop = s.CoordinateOrigin is CoordinateOrigin.TopLeft or CoordinateOrigin.TopRight;
        float left = clip.Left;
        float top = clip.Top;
        float right = clip.Right;
        float bottom = clip.Bottom;

        if (s.FlushAxis is FlushAxis.Vertical or FlushAxis.Both)
        {
            if (originTop)
            {
                bottom = gridBounds.Bottom;
            }
            else
            {
                top = gridBounds.Top;
            }
        }

        if (s.FlushAxis is FlushAxis.Horizontal or FlushAxis.Both)
        {
            if (originLeft)
            {
                right = gridBounds.Right;
            }
            else
            {
                left = gridBounds.Left;
            }
        }

        RectangleF newClip = RectangleF.FromLTRB(left, top, right, bottom);
        RectangleF newFrame = Deflate(newClip, -insetPx, -insetPx, -insetPx, -insetPx);
        return (newFrame, newClip);
    }

    private static RectangleF Deflate(RectangleF r, double left, double top, double right, double bottom) =>
        RectangleF.FromLTRB(
            (float)(r.Left + left),
            (float)(r.Top + top),
            (float)(r.Right - right),
            (float)(r.Bottom - bottom));

    // ------------------------------------------------------------------ canvas

    private static (double WidthPx, double HeightPx, double WidthMm, double HeightMm) ResolveCanvas(
        GridSettings s, UnitScale scale)
    {
        if (s.Preset == CanvasPreset.Custom)
        {
            double wPx = scale.ToPx(s.CustomWidth);
            double hPx = scale.ToPx(s.CustomHeight);
            return (wPx, hPx, UnitScale.PxToMm(wPx, s.Dpi), UnitScale.PxToMm(hPx, s.Dpi));
        }

        CanvasSpec spec = CanvasPresets.Resolve(s.Preset);
        if (spec.IsPaper)
        {
            double wMm = spec.WidthMm!.Value;
            double hMm = spec.HeightMm!.Value;
            if (s.PageOrientation == PageOrientation.Landscape)
            {
                (wMm, hMm) = (hMm, wMm);
            }

            return (UnitScale.MmToPx(wMm, s.Dpi), UnitScale.MmToPx(hMm, s.Dpi), wMm, hMm);
        }

        double px = spec.WidthPx!.Value;
        double py = spec.HeightPx!.Value;
        return (px, py, UnitScale.PxToMm(px, s.Dpi), UnitScale.PxToMm(py, s.Dpi));
    }
}
