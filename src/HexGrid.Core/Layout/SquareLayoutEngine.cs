using System.Drawing;
using System.Runtime.InteropServices;
using HexGrid.Core.Labels;
using HexGrid.Core.Units;

namespace HexGrid.Core.Layout;

/// <summary>
/// Square-specific geometry. Squares tile the map area exactly, so unlike hexes they can be sized
/// to leave a clean margin instead of clipping a partial cell - that is what AutoFitSquares controls.
/// The canvas/frame/label-gutter framing shared with the hex engine lives in
/// <see cref="GridLayoutEngine"/>, which calls into this class.
/// </summary>
public static class SquareLayoutEngine
{
    private const double Tolerance = 1e-6;

    /// <summary>Solves the square side and resulting column/row counts for one convergence pass.</summary>
    internal static CellFit Solve(
        GridSettings s, UnitScale scale, RectangleF clip)
    {
        int reqCols = Math.Max(1, s.Columns);
        int reqRows = Math.Max(1, s.Rows);
        double gapX = Math.Max(0.0, scale.ToPx(s.CellGapX));
        double gapY = Math.Max(0.0, scale.ToPx(s.CellGapY));

        (int columns, int rows, double side) = s.SizingMode == GridSizingMode.AutoFitRowsColumns
            ? SolveFromCounts(s.AutoFitSquares, reqCols, reqRows, clip, gapX, gapY)
            : SolveFromSize(s.AutoFitSquares, Math.Max(1e-6, scale.ToPx(s.SquareSize)), clip, gapX, gapY);

        if (side <= 0 || double.IsNaN(side) || double.IsInfinity(side))
        {
            throw new InvalidOperationException("The requested square size does not resolve to a usable grid.");
        }

        return new CellFit(columns, rows, side, side, RadiusPx: null);
    }

    /// <summary>
    /// AutoFitRowsColumns: the requested column/row counts drive the side length exactly as if there
    /// were no gap - the square's own size never shrinks for the gap. Gap X/Gap Y then widen the
    /// pitch used to count how many of that exact-sized square actually fit in the map area, so a
    /// large enough gap can mean fewer whole squares than requested (see <see cref="FloorFit"/>).
    /// </summary>
    private static (int Columns, int Rows, double Side) SolveFromCounts(
        bool autoFit, int reqCols, int reqRows, RectangleF clip, double gapX, double gapY)
    {
        if (autoFit)
        {
            // Fit the walls: the whole reqCols x reqRows block must fit inside the map area, so the
            // side is whichever axis is tighter. No clipping - the counts requested are a target.
            // Gap can bring the actual count below it (FloorFit), never above.
            double side = Math.Min(clip.Width / reqCols, clip.Height / reqRows);
            int columns = Math.Min(reqCols, FloorFit(clip.Width, side, gapX));
            int rows = Math.Min(reqRows, FloorFit(clip.Height, side, gapY));
            return (columns, rows, side);
        }

        // Fill the walls, same shape as the hex grid: centres span the map area edge to edge and the
        // outermost squares overhang it and are clipped. The square's own size still comes from the
        // gap-free pitch implied by the requested counts; Gap only widens the pitch used to place them.
        double byWidth = reqCols > 1 ? clip.Width / (reqCols - 1) : double.PositiveInfinity;
        double byHeight = reqRows > 1 ? clip.Height / (reqRows - 1) : double.PositiveInfinity;
        double side2 = Math.Min(byWidth, byHeight);
        side2 = double.IsInfinity(side2) ? Math.Min(clip.Width, clip.Height) : side2;
        return (GridAxis.CoverCount(clip.Width, side2 + gapX), GridAxis.CoverCount(clip.Height, side2 + gapY), side2);
    }

    /// <summary>
    /// FixedHexWidth: the configured square size drives the column/row counts and is drawn exactly as
    /// entered, regardless of Gap. Gap X/Gap Y widen the pitch used to count how many fit.
    /// </summary>
    private static (int Columns, int Rows, double Side) SolveFromSize(bool autoFit, double side, RectangleF clip, double gapX, double gapY) =>
        autoFit
            ? (Math.Max(1, FloorFit(clip.Width, side, gapX)), Math.Max(1, FloorFit(clip.Height, side, gapY)), side)
            : (GridAxis.CoverCount(clip.Width, side + gapX), GridAxis.CoverCount(clip.Height, side + gapY), side);

    /// <summary>Builds the final square cells once the convergence loop in <see cref="GridLayoutEngine"/> has settled.</summary>
    internal static (IReadOnlyList<GridCell> Cells, double[] ColumnCenterXs, double[] RowCenterYs, RectangleF GridBounds) BuildCells(
        GridSettings s, UnitScale scale, CellFit fit, RectangleF clip, string[] columnLabels, string[] rowLabels)
    {
        // The square is always drawn at its fitted side - unaffected by the gap - so it can never become
        // a rectangle. Gap X/Gap Y only widen the pitch used to place its centre, independently per axis.
        double side = fit.CellWidthPx;
        double pitchX = side + Math.Max(0.0, scale.ToPx(s.CellGapX));
        double pitchY = side + Math.Max(0.0, scale.ToPx(s.CellGapY));
        double spanX = (fit.Columns - 1) * pitchX;
        double spanY = (fit.Rows - 1) * pitchY;
        double half = side / 2.0;
        (double firstX, double firstY) = ResolveBlockOrigin(s, scale, fit, clip, pitchX, pitchY);

        var cells = new List<GridCell>(fit.Columns * fit.Rows);
        double[] columnCenterXs = GridAxis.Centres(firstX, pitchX, fit.Columns);
        double[] rowCenterYs = GridAxis.Centres(firstY, pitchY, fit.Rows);

        for (int c = 0; c < fit.Columns; c++)
        {
            for (int r = 0; r < fit.Rows; r++)
            {
                double cx = columnCenterXs[c];
                double cy = rowCenterYs[r];

                cells.Add(new GridCell
                {
                    Column = c,
                    Row = r,
                    Center = new PointF((float)cx, (float)cy),
                    Vertices = Vertices(cx, cy, side),
                    Label = CoordinateLabeller.Combine(columnLabels[c], rowLabels[r], s.CoordinateSeparator),
                });
            }
        }

        var gridBounds = RectangleF.FromLTRB(
            (float)(firstX - half),
            (float)(firstY - half),
            (float)(firstX + spanX + half),
            (float)(firstY + spanY + half));

        return (cells, columnCenterXs, rowCenterYs, gridBounds);
    }

    /// <summary>
    /// AutoFitSquares centres the whole block by default: leftover space becomes an even margin.
    /// FlushAxis instead pushes that block toward the side of the axis away from CoordinateOrigin, so
    /// the origin side sits flush with no gap and the whole leftover lands on the far side. Off
    /// centres the span of cell CENTRES instead, so the outermost squares overhang the clip and are
    /// cut, matching HexLayoutEngine.ComputeOrigin's "fill the walls" behaviour.
    /// </summary>
    private static (double FirstX, double FirstY) ResolveBlockOrigin(
        GridSettings s, UnitScale scale, CellFit fit, RectangleF clip, double pitchX, double pitchY)
    {
        double spanX = (fit.Columns - 1) * pitchX;
        double spanY = (fit.Rows - 1) * pitchY;

        if (!s.AutoFitSquares)
        {
            double fillX = GridAxis.CentredStart(clip.Left, clip.Width, spanX) + scale.ToPx(s.GridOffsetX);
            double fillY = GridAxis.CentredStart(clip.Top, clip.Height, spanY) + scale.ToPx(s.GridOffsetY);
            return (fillX, fillY);
        }

        double side = fit.CellWidthPx;
        double half = side / 2.0;
        bool flushX = s.FlushAxis.FlushesHorizontally();
        bool flushY = s.FlushAxis.FlushesVertically();

        double firstX = BlockOrigin(clip.Left, clip.Width, spanX + side, half, flushX, s.CoordinateOrigin.IsLeft()) + scale.ToPx(s.GridOffsetX);
        double firstY = BlockOrigin(clip.Top, clip.Height, spanY + side, half, flushY, s.CoordinateOrigin.IsTop()) + scale.ToPx(s.GridOffsetY);
        return (firstX, firstY);
    }

    /// <summary>
    /// Centre of the first (leftmost/topmost) cell along one axis of an AutoFitSquares block. Not
    /// flushed: the leftover between the block and the clip is split evenly on both sides, as before.
    /// Flushed: the block's edge on <paramref name="towardStart"/>'s side sits exactly on the clip
    /// edge (no gap there) and the whole leftover is pushed to the far side instead.
    /// </summary>
    private static double BlockOrigin(double clipStart, double clipSize, double blockSize, double half, bool flush, bool towardStart)
    {
        if (!flush)
        {
            return clipStart + ((clipSize - blockSize) / 2.0) + half;
        }

        return towardStart ? clipStart + half : clipStart + clipSize - blockSize + half;
    }

    private static PointF[] Vertices(double cx, double cy, double side)
    {
        float half = (float)(side / 2.0);
        float x = (float)cx;
        float y = (float)cy;
        return
        [
            new PointF(x - half, y - half),
            new PointF(x + half, y - half),
            new PointF(x + half, y + half),
            new PointF(x - half, y + half),
        ];
    }

    /// <summary>
    /// How many <paramref name="side"/>-sized squares, each pair separated by <paramref name="gap"/>,
    /// fit within <paramref name="available"/>: the largest n such that n*side + (n-1)*gap &lt;=
    /// available. Reduces to the old gap-free floor(available / side) when gap is 0.
    /// </summary>
    private static int FloorFit(double available, double side, double gap) =>
        (int)Math.Floor(((available + gap) / (side + gap)) + Tolerance);
}
