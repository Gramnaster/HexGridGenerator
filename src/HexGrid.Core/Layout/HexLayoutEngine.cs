using System.Drawing;
using HexGrid.Core.Labels;
using HexGrid.Core.Units;

namespace HexGrid.Core.Layout;

/// <summary>
/// Hex-specific geometry: sizing the hex from the requested rows/columns or width, and laying out
/// hex cells within an already-solved clip rect. The canvas/frame/label-gutter framing shared with
/// the square engine lives in <see cref="GridLayoutEngine"/>, which calls into this class.
/// </summary>
public static class HexLayoutEngine
{
    private static readonly double Sqrt3 = Math.Sqrt(3.0);

    /// <summary>
    /// The resolved per-hex spacing and the position of the first (top-left) hex centre, bundled so
    /// downstream steps (S107: was 11 loose parameters on <see cref="BuildCells"/>) take one cohesive
    /// value instead of unpacking and re-passing the same handful of numbers individually.
    /// </summary>
    private readonly record struct GridGeometry(
        bool Flat,
        int Columns,
        int Rows,
        double RadiusPx,
        double ColSpacing,
        double RowSpacing,
        double FirstX,
        double FirstY,
        double SpanX,
        double SpanY);

    /// <summary>Solves the hex radius and resulting column/row counts for one convergence pass.</summary>
    internal static (int Columns, int Rows, double CellWidthPx, double CellHeightPx, double? RadiusPx) Solve(
        GridSettings s, UnitScale scale, RectangleF clip)
    {
        bool flat = s.HexOrientation == HexOrientation.FlatTop;

        double radiusPx = s.SizingMode == GridSizingMode.AutoFitRowsColumns
            ? FitRadius(flat, Math.Max(1, s.Columns), Math.Max(1, s.Rows), clip.Width, clip.Height)
            : RadiusFromHexWidth(flat, Math.Max(1e-6, scale.ToPx(s.HexWidth)));

        if (radiusPx <= 0 || double.IsNaN(radiusPx) || double.IsInfinity(radiusPx))
        {
            throw new InvalidOperationException("The requested hex size does not resolve to a usable grid.");
        }

        (int columns, int rows) = CoverCounts(flat, radiusPx, clip.Width, clip.Height);
        double widthPx = flat ? 2 * radiusPx : Sqrt3 * radiusPx;
        double heightPx = flat ? Sqrt3 * radiusPx : 2 * radiusPx;
        return (columns, rows, widthPx, heightPx, radiusPx);
    }

    /// <summary>Builds the final hex cells once the convergence loop in <see cref="GridLayoutEngine"/> has settled.</summary>
    internal static (IReadOnlyList<GridCell> Cells, double[] ColumnCenterXs, double[] RowCenterYs, RectangleF GridBounds) BuildCells(
        GridSettings s, UnitScale scale, int columns, int rows, double radiusPx, RectangleF clip,
        string[] columnLabels, string[] rowLabels, string separator)
    {
        bool flat = s.HexOrientation == HexOrientation.FlatTop;
        GridGeometry g = ComputeOrigin(s, scale, flat, columns, rows, radiusPx, clip);

        var cells = new List<GridCell>(g.Columns * g.Rows);
        var columnCenterXs = new double[g.Columns];
        var rowCenterYs = new double[g.Rows];

        for (int c = 0; c < g.Columns; c++)
        {
            columnCenterXs[c] = g.FirstX + (c * g.ColSpacing);
        }

        for (int r = 0; r < g.Rows; r++)
        {
            rowCenterYs[r] = g.FirstY + (r * g.RowSpacing);
        }

        for (int c = 0; c < g.Columns; c++)
        {
            for (int r = 0; r < g.Rows; r++)
            {
                double cx = columnCenterXs[c] + (!g.Flat && r % 2 == 1 ? g.ColSpacing / 2.0 : 0);
                double cy = rowCenterYs[r] + (g.Flat && c % 2 == 1 ? g.RowSpacing / 2.0 : 0);

                cells.Add(new GridCell
                {
                    Column = c,
                    Row = r,
                    Center = new PointF((float)cx, (float)cy),
                    Vertices = Vertices(cx, cy, g.RadiusPx, g.Flat),
                    Label = CoordinateLabeller.Combine(columnLabels[c], rowLabels[r], separator),
                });
            }
        }

        return (cells, columnCenterXs, rowCenterYs, ComputeGridBounds(g));
    }

    // ---------------------------------------------------------------- geometry

    /// <summary>Spacing between hex centres and the position of the first (top-left) hex centre.</summary>
    private static GridGeometry ComputeOrigin(
        GridSettings s, UnitScale scale, bool flat, int columns, int rows, double radiusPx, RectangleF clip)
    {
        double colSpacing = flat ? 1.5 * radiusPx : Sqrt3 * radiusPx;
        double rowSpacing = flat ? Sqrt3 * radiusPx : 1.5 * radiusPx;

        // Distance covered by the hex CENTRES, which is what gets centred inside the map area.
        double spanX = ((columns - 1) * colSpacing) + (!flat && rows > 1 ? colSpacing / 2.0 : 0);
        double spanY = ((rows - 1) * rowSpacing) + (flat && columns > 1 ? rowSpacing / 2.0 : 0);

        double firstX = clip.Left + ((clip.Width - spanX) / 2.0) + scale.ToPx(s.GridOffsetX);
        double firstY = clip.Top + ((clip.Height - spanY) / 2.0) + scale.ToPx(s.GridOffsetY);

        return new GridGeometry(flat, columns, rows, radiusPx, colSpacing, rowSpacing, firstX, firstY, spanX, spanY);
    }

    private static RectangleF ComputeGridBounds(GridGeometry g)
    {
        double halfW = g.Flat ? g.RadiusPx : g.ColSpacing / 2.0;
        double halfH = g.Flat ? g.RowSpacing / 2.0 : g.RadiusPx;
        return RectangleF.FromLTRB(
            (float)(g.FirstX - halfW),
            (float)(g.FirstY - halfH),
            (float)(g.FirstX + g.SpanX + halfW),
            (float)(g.FirstY + g.SpanY + halfH));
    }

    private static PointF[] Vertices(double cx, double cy, double r, bool flatTop)
    {
        var pts = new PointF[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 180.0 * (flatTop ? 60 * i : (60 * i) - 30);
            pts[i] = new PointF(
                (float)(cx + (r * Math.Cos(angle))),
                (float)(cy + (r * Math.Sin(angle))));
        }

        return pts;
    }

    /// <summary>
    /// Largest circumradius for which the requested column and row counts still span the map area.
    /// Measured centre to centre, because the outermost hexes are meant to overhang and be clipped.
    /// </summary>
    private static double FitRadius(bool flatTop, int cols, int rows, double availW, double availH)
    {
        (double byWidth, double byHeight) = RadiusComponents(flatTop, cols, rows, availW, availH);
        double r = Math.Min(byWidth, byHeight);
        return double.IsInfinity(r) ? Math.Min(availW, availH) / 2.0 : r;
    }

    /// <summary>The radius each axis alone would imply. <see cref="FitRadius"/> takes the smaller.</summary>
    private static (double ByWidth, double ByHeight) RadiusComponents(
        bool flatTop, int cols, int rows, double availW, double availH)
    {
        if (flatTop)
        {
            double byWidth = cols > 1 ? availW / (1.5 * (cols - 1)) : double.PositiveInfinity;
            double vSteps = (rows - 1) + (cols > 1 ? 0.5 : 0);
            double byHeight = vSteps > 0 ? availH / (Sqrt3 * vSteps) : double.PositiveInfinity;
            return (byWidth, byHeight);
        }

        double hSteps = (cols - 1) + (rows > 1 ? 0.5 : 0);
        double byWidthPointy = hSteps > 0 ? availW / (Sqrt3 * hSteps) : double.PositiveInfinity;
        double byHeightPointy = rows > 1 ? availH / (1.5 * (rows - 1)) : double.PositiveInfinity;
        return (byWidthPointy, byHeightPointy);
    }

    /// <summary>
    /// In AutoFitRowsColumns mode, Columns and Rows share one resolved hex radius: whichever axis
    /// implies the smaller radius wins, so the other has no effect on the grid until it would imply
    /// an equally small radius. Reports which axis currently drives the grid and the value the other
    /// one needs to reach to start affecting it, given the current settings and the already-solved
    /// clip area.
    /// </summary>
    /// <remarks>
    /// The threshold is exact for the formulas above modulo one approximation: it holds the clip
    /// area fixed at its current, already-converged value, even though crossing a digit/letter-count
    /// boundary (e.g. 9 to 10 rows) would nudge the label gutter, and so the clip area, slightly.
    /// </remarks>
    public static (bool ColumnsBound, int OtherAxisThreshold) SizingBindingHint(GridSettings s, RectangleF clip)
    {
        ArgumentNullException.ThrowIfNull(s);

        bool flat = s.HexOrientation == HexOrientation.FlatTop;
        int columns = Math.Max(2, s.Columns);
        int rows = Math.Max(2, s.Rows);
        double availW = clip.Width;
        double availH = clip.Height;

        (double byWidth, double byHeight) = RadiusComponents(flat, columns, rows, availW, availH);
        bool columnsBound = byWidth <= byHeight;

        double threshold = columnsBound
            ? RowsNeededToBind(flat, columns, availW, availH)
            : ColumnsNeededToBind(flat, rows, availW, availH);

        return (columnsBound, (int)Math.Ceiling(threshold));
    }

    /// <summary>Smallest Columns at which byWidth(Columns) would tie byHeight(current Rows).</summary>
    private static double ColumnsNeededToBind(bool flatTop, int rows, double availW, double availH) =>
        flatTop
            ? 1 + (availW * Sqrt3 * (rows - 0.5) / (1.5 * availH))
            : 0.5 + (availW * 1.5 * (rows - 1) / (Sqrt3 * availH));

    /// <summary>Smallest Rows at which byHeight(Rows) would tie byWidth(current Columns).</summary>
    private static double RowsNeededToBind(bool flatTop, int columns, double availW, double availH) =>
        flatTop
            ? 0.5 + (availH * 1.5 * (columns - 1) / (Sqrt3 * availW))
            : 1 + (availH * Sqrt3 * (columns - 0.5) / (1.5 * availW));

    private static double RadiusFromHexWidth(bool flatTop, double hexWidthPx) =>
        flatTop ? hexWidthPx / 2.0 : hexWidthPx / Sqrt3;

    /// <summary>
    /// How many columns and rows of radius r it takes to place a centre across the whole map area.
    /// The first and last centres sit inside the area; their hexes overhang it.
    /// </summary>
    private static (int Columns, int Rows) CoverCounts(bool flatTop, double r, double availW, double availH)
    {
        double colSpacing = flatTop ? 1.5 * r : Sqrt3 * r;
        double rowSpacing = flatTop ? Sqrt3 * r : 1.5 * r;

        // The auto-fit radius is solved so the requested count lands exactly on the boundary, which
        // rounds the wrong way often enough to matter. Nudge before flooring.
        const double Tolerance = 1e-6;

        if (flatTop)
        {
            int cols = Steps(availW, colSpacing);
            double usable = availH - (cols > 1 ? rowSpacing / 2.0 : 0);
            return (cols, Steps(usable, rowSpacing));
        }
        else
        {
            int rows = Steps(availH, rowSpacing);
            double usable = availW - (rows > 1 ? colSpacing / 2.0 : 0);
            return (Steps(usable, colSpacing), rows);
        }

        static int Steps(double available, double spacing) =>
            Math.Max(1, (int)Math.Floor((available / spacing) + Tolerance) + 1);
    }
}
