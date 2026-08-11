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
    internal static (int Columns, int Rows, double CellWidthPx, double CellHeightPx, double? RadiusPx) Solve(
        GridSettings s, UnitScale scale, RectangleF clip)
    {
        int reqCols = Math.Max(1, s.Columns);
        int reqRows = Math.Max(1, s.Rows);

        (int columns, int rows, double side) = s.SizingMode == GridSizingMode.AutoFitRowsColumns
            ? SolveFromCounts(s.AutoFitSquares, reqCols, reqRows, clip)
            : SolveFromSize(s.AutoFitSquares, Math.Max(1e-6, scale.ToPx(s.SquareSize)), clip);

        if (side <= 0 || double.IsNaN(side) || double.IsInfinity(side))
        {
            throw new InvalidOperationException("The requested square size does not resolve to a usable grid.");
        }

        return (columns, rows, side, side, null);
    }

    /// <summary>AutoFitRowsColumns: the requested column/row counts drive the side length.</summary>
    private static (int Columns, int Rows, double Side) SolveFromCounts(
        bool autoFit, int reqCols, int reqRows, RectangleF clip)
    {
        if (autoFit)
        {
            // Fit the walls: the whole reqCols x reqRows block must fit inside the map area, so the
            // side is whichever axis is tighter. No clipping - the counts requested are the counts drawn.
            double side = Math.Min(clip.Width / reqCols, clip.Height / reqRows);
            return (reqCols, reqRows, side);
        }

        // Fill the walls, same shape as the hex grid: centres span the map area edge to edge and the
        // outermost squares overhang it and are clipped.
        double byWidth = reqCols > 1 ? clip.Width / (reqCols - 1) : double.PositiveInfinity;
        double byHeight = reqRows > 1 ? clip.Height / (reqRows - 1) : double.PositiveInfinity;
        double side2 = Math.Min(byWidth, byHeight);
        side2 = double.IsInfinity(side2) ? Math.Min(clip.Width, clip.Height) : side2;
        return (Steps(clip.Width, side2), Steps(clip.Height, side2), side2);
    }

    /// <summary>FixedHexWidth: the square size setting drives the column/row counts.</summary>
    private static (int Columns, int Rows, double Side) SolveFromSize(bool autoFit, double side, RectangleF clip) =>
        autoFit
            ? (Math.Max(1, FloorCount(clip.Width, side)), Math.Max(1, FloorCount(clip.Height, side)), side)
            : (Steps(clip.Width, side), Steps(clip.Height, side), side);

    /// <summary>Builds the final square cells once the convergence loop in <see cref="GridLayoutEngine"/> has settled.</summary>
    internal static (IReadOnlyList<GridCell> Cells, double[] ColumnCenterXs, double[] RowCenterYs, RectangleF GridBounds) BuildCells(
        GridSettings s, UnitScale scale, SquareFit fit, string[] columnLabels, string[] rowLabels, string separator)
    {
        double spanX = (fit.Columns - 1) * fit.Side;
        double spanY = (fit.Rows - 1) * fit.Side;
        double half = fit.Side / 2.0;
        (double firstX, double firstY) = ResolveBlockOrigin(s, scale, fit);

        var cells = new List<GridCell>(fit.Columns * fit.Rows);
        var columnCenterXs = new double[fit.Columns];
        var rowCenterYs = new double[fit.Rows];

        for (int c = 0; c < fit.Columns; c++)
        {
            columnCenterXs[c] = firstX + (c * fit.Side);
        }

        for (int r = 0; r < fit.Rows; r++)
        {
            rowCenterYs[r] = firstY + (r * fit.Side);
        }

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
                    Vertices = Vertices(cx, cy, fit.Side),
                    Label = CoordinateLabeller.Combine(columnLabels[c], rowLabels[r], separator),
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
    private static (double FirstX, double FirstY) ResolveBlockOrigin(GridSettings s, UnitScale scale, SquareFit fit)
    {
        RectangleF clip = fit.Clip;

        if (!s.AutoFitSquares)
        {
            double spanX = (fit.Columns - 1) * fit.Side;
            double spanY = (fit.Rows - 1) * fit.Side;
            double fillX = clip.Left + ((clip.Width - spanX) / 2.0) + scale.ToPx(s.GridOffsetX);
            double fillY = clip.Top + ((clip.Height - spanY) / 2.0) + scale.ToPx(s.GridOffsetY);
            return (fillX, fillY);
        }

        double half = fit.Side / 2.0;
        bool originLeft = s.CoordinateOrigin is CoordinateOrigin.TopLeft or CoordinateOrigin.BottomLeft;
        bool originTop = s.CoordinateOrigin is CoordinateOrigin.TopLeft or CoordinateOrigin.TopRight;
        bool flushX = s.FlushAxis is FlushAxis.Horizontal or FlushAxis.Both;
        bool flushY = s.FlushAxis is FlushAxis.Vertical or FlushAxis.Both;

        double firstX = BlockOrigin(clip.Left, clip.Width, fit.Columns * fit.Side, half, flushX, originLeft) + scale.ToPx(s.GridOffsetX);
        double firstY = BlockOrigin(clip.Top, clip.Height, fit.Rows * fit.Side, half, flushY, originTop) + scale.ToPx(s.GridOffsetY);
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

    private static int FloorCount(double available, double side) =>
        (int)Math.Floor((available / side) + Tolerance);

    /// <summary>
    /// How many columns and rows of side <paramref name="spacing"/> it takes to place a centre across
    /// the whole map area. Mirrors <see cref="HexLayoutEngine"/>'s CoverCounts. The first and last
    /// centres sit inside the area; their squares overhang it.
    /// </summary>
    private static int Steps(double available, double spacing) =>
        Math.Max(1, (int)Math.Floor((available / spacing) + Tolerance) + 1);

    /// <summary>
    /// Square counterpart to <see cref="HexLayoutEngine.SizingBindingHint"/>, for AutoFitRowsColumns
    /// with AutoFitSquares on: side = min(clipW/Columns, clipH/Rows), so whichever axis implies the
    /// smaller side wins and the other is inert until it would tie. Reports which axis currently
    /// drives the grid and the value the other one needs to reach to start affecting it.
    /// </summary>
    public static (bool ColumnsBound, int OtherAxisThreshold) SizingBindingHint(GridSettings s, RectangleF clip)
    {
        ArgumentNullException.ThrowIfNull(s);

        int columns = Math.Max(1, s.Columns);
        int rows = Math.Max(1, s.Rows);
        double byWidth = clip.Width / columns;
        double byHeight = clip.Height / rows;
        bool columnsBound = byWidth <= byHeight;

        double threshold = columnsBound
            ? clip.Height / byWidth
            : clip.Width / byHeight;

        return (columnsBound, (int)Math.Ceiling(threshold));
    }

    // How far from the requested count RecommendFit is willing to search on each axis. Exact zero
    // gap needs Columns/Rows to exactly equal clip.Width/clip.Height, which for arbitrary canvas
    // sizes and margins is a coincidence, not something to count on - but nearby whole-number pairs
    // are a genuine Diophantine approximation problem (best rational approximation of the aspect
    // ratio with a bounded denominator), and brute-forcing a small window around the request solves
    // it exactly rather than guessing at one nudge. A wider window would usually find an even
    // smaller gap, but at a Columns x Rows far enough from the request to defeat the point of asking
    // for roughly that many cells.
    private const int SearchWindow = 20;

    // Below this, the leftover is sub-pixel at any real print DPI - i.e. not actually visible - so
    // it is reported as no gap rather than a residual size.
    private const double NoGapTolerancePx = 0.5;

    /// <summary>
    /// AutoFitSquares centres whichever axis is bound by <see cref="SizingBindingHint"/> and leaves
    /// the other axis's leftover space as a margin. Searches a window of nearby whole (Columns, Rows)
    /// pairs - varying columns and matching the tightest rows via <see cref="MatchRows"/>, then vice
    /// versa via <see cref="MatchColumns"/> - and returns whichever candidate found, including the
    /// requested counts themselves, leaves the smallest gap.
    /// </summary>
    public static SquareFitSuggestion RecommendFit(GridSettings s, RectangleF clip)
    {
        ArgumentNullException.ThrowIfNull(s);

        int reqCols = Math.Max(1, s.Columns);
        int reqRows = Math.Max(1, s.Rows);
        SquareFitCandidate current = EvaluateCandidate(reqCols, reqRows, clip);
        SquareFitCandidate best = current;

        for (int cols = Math.Max(1, reqCols - SearchWindow); cols <= reqCols + SearchWindow; cols++)
        {
            best = Tighter(best, MatchRows(cols, clip));
        }

        for (int rows = Math.Max(1, reqRows - SearchWindow); rows <= reqRows + SearchWindow; rows++)
        {
            best = Tighter(best, MatchColumns(rows, clip));
        }

        bool hasTighterFit = best.GapPx < current.GapPx - Tolerance;
        var suggestion = new SquareFitSuggestion(hasTighterFit, best.Columns, best.Rows, best.SidePx, best.GapPx, current.GapPx);
        return suggestion.GapPx < NoGapTolerancePx ? suggestion with { GapPx = 0 } : suggestion;
    }

    /// <summary>For a fixed column count, the row count (floor or ceiling of the exact ratio) that ties the fit.</summary>
    private static SquareFitCandidate MatchRows(int columns, RectangleF clip)
    {
        double idealRows = clip.Height * columns / clip.Width;
        return Tighter(
            EvaluateCandidate(columns, Math.Max(1, (int)Math.Floor(idealRows)), clip),
            EvaluateCandidate(columns, Math.Max(1, (int)Math.Ceiling(idealRows)), clip));
    }

    /// <summary>For a fixed row count, the column count (floor or ceiling of the exact ratio) that ties the fit.</summary>
    private static SquareFitCandidate MatchColumns(int rows, RectangleF clip)
    {
        double idealCols = clip.Width * rows / clip.Height;
        return Tighter(
            EvaluateCandidate(Math.Max(1, (int)Math.Floor(idealCols)), rows, clip),
            EvaluateCandidate(Math.Max(1, (int)Math.Ceiling(idealCols)), rows, clip));
    }

    private static SquareFitCandidate Tighter(SquareFitCandidate a, SquareFitCandidate b) =>
        b.GapPx < a.GapPx ? b : a;

    /// <summary>Side and total leftover gap (on whichever axis isn't bound) for one candidate (Columns, Rows) pair.</summary>
    private static SquareFitCandidate EvaluateCandidate(int columns, int rows, RectangleF clip)
    {
        double byWidth = clip.Width / columns;
        double byHeight = clip.Height / rows;
        double side = Math.Min(byWidth, byHeight);
        double gapPx = byWidth <= byHeight ? clip.Height - (rows * side) : clip.Width - (columns * side);
        return new SquareFitCandidate(columns, rows, side, gapPx);
    }

    // MA0008 wants an explicit StructLayoutAttribute; see CanvasSpec.cs for the rationale for Auto
    // over Sequential/Explicit - this is a plain value type from a UI hint calculation, not a hot
    // path or interop boundary.
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct SquareFitCandidate(int Columns, int Rows, double SidePx, double GapPx);

    // Columns, Rows, Side and Clip always travel together as the outcome of a solved pass - bundled
    // here so BuildCells/ResolveBlockOrigin take one parameter instead of four, staying under the
    // analyzer's parameter-count limit. Internal, not private: GridLayoutEngine constructs one to
    // call BuildCells. MA0008 wants an explicit StructLayoutAttribute; see CanvasSpec.cs for the
    // rationale for Auto over Sequential/Explicit - not a hot path or interop boundary.
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct SquareFit(int Columns, int Rows, double Side, RectangleF Clip);
}
