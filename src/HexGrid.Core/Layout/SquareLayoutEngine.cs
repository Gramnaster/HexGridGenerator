using System.Drawing;
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
        GridSettings s, UnitScale scale, int columns, int rows, double side, RectangleF clip,
        string[] columnLabels, string[] rowLabels, string separator)
    {
        double spanX = (columns - 1) * side;
        double spanY = (rows - 1) * side;

        // AutoFitSquares centres the whole block (leftover space becomes an even margin); off centres
        // the span of cell CENTRES instead, so the outermost squares overhang the clip and are cut,
        // matching HexLayoutEngine.ComputeOrigin's "fill the walls" behaviour.
        double firstX = s.AutoFitSquares
            ? clip.Left + ((clip.Width - (columns * side)) / 2.0) + (side / 2.0) + scale.ToPx(s.GridOffsetX)
            : clip.Left + ((clip.Width - spanX) / 2.0) + scale.ToPx(s.GridOffsetX);

        double firstY = s.AutoFitSquares
            ? clip.Top + ((clip.Height - (rows * side)) / 2.0) + (side / 2.0) + scale.ToPx(s.GridOffsetY)
            : clip.Top + ((clip.Height - spanY) / 2.0) + scale.ToPx(s.GridOffsetY);

        var cells = new List<GridCell>(columns * rows);
        var columnCenterXs = new double[columns];
        var rowCenterYs = new double[rows];

        for (int c = 0; c < columns; c++)
        {
            columnCenterXs[c] = firstX + (c * side);
        }

        for (int r = 0; r < rows; r++)
        {
            rowCenterYs[r] = firstY + (r * side);
        }

        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                double cx = columnCenterXs[c];
                double cy = rowCenterYs[r];

                cells.Add(new GridCell
                {
                    Column = c,
                    Row = r,
                    Center = new PointF((float)cx, (float)cy),
                    Vertices = Vertices(cx, cy, side),
                    Label = CoordinateLabeller.Combine(columnLabels[c], rowLabels[r], separator),
                });
            }
        }

        double half = side / 2.0;
        var gridBounds = RectangleF.FromLTRB(
            (float)(firstX - half),
            (float)(firstY - half),
            (float)(firstX + spanX + half),
            (float)(firstY + spanY + half));

        return (cells, columnCenterXs, rowCenterYs, gridBounds);
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
}
