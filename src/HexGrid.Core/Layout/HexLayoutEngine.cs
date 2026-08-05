using System.Drawing;
using HexGrid.Core.Labels;
using HexGrid.Core.Units;

namespace HexGrid.Core.Layout;

/// <summary>
/// Solves the grid geometry for a set of settings. Knows nothing about SVG, GDI+ or any other renderer.
/// </summary>
/// <remarks>
/// Working outward from the canvas edge: safe margin, coordinate-label band, frame rule, map area.
/// The grid always fills the map area — every hex centre lands inside it and the outermost hexes
/// overhang and get clipped — so the grid meets the frame on all four sides with no gap.
/// </remarks>
public static class HexLayoutEngine
{
    private static readonly double Sqrt3 = Math.Sqrt(3.0);

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
        double framePx = s.BorderStyle == MapBorderStyle.None ? 0 : Math.Max(0, scale.ToPx(s.BorderThickness));
        double insetPx = Math.Max(0, scale.ToPx(s.GridInset));
        double labelPadPx = Math.Max(0, scale.ToPx(s.LabelPadding));
        double marginalFontPx = scale.PointsToPx(s.MarginalFontSize);

        RectangleF safeRect = Deflate(
            new RectangleF(0, 0, (float)canvasWpx, (float)canvasHpx), safePx, safePx, safePx, safePx);

        if (safeRect.Width <= 0 || safeRect.Height <= 0)
        {
            throw new InvalidOperationException("The safe margin consumes the whole canvas. Reduce it or enlarge the canvas.");
        }

        bool flat = s.HexOrientation == HexOrientation.FlatTop;

        // The label gutter depends on how many characters the labels run to, which depends on the
        // row and column counts, which depend on the gutter. Three passes converge.
        int columns = Math.Max(1, s.Columns);
        int rows = Math.Max(1, s.Rows);
        double radiusPx = 0;
        RectangleF frameBounds = safeRect;
        RectangleF clip = safeRect;

        for (int pass = 0; pass < 3; pass++)
        {
            (string[] colLabels, string[] rowLabels) = CoordinateLabeller.BuildAxes(
                columns, rows, s.LabelScheme, s.CoordinateOrigin, s.SkipLettersIO, s.ZeroPadNumbers);
            (_, int rowChars) = CoordinateLabeller.MaxLabelLengths(colLabels, rowLabels);

            double horizontal = labelPadPx + rowChars * marginalFontPx * AverageGlyphWidthRatio;
            double vertical = labelPadPx + marginalFontPx * LineHeightRatio;
            double half = framePx / 2.0;

            frameBounds = Deflate(
                safeRect,
                (s.MarginalLabelSides.HasFlag(LabelSides.Left) ? horizontal : 0) + half,
                (s.MarginalLabelSides.HasFlag(LabelSides.Top) ? vertical : 0) + half,
                (s.MarginalLabelSides.HasFlag(LabelSides.Right) ? horizontal : 0) + half,
                (s.MarginalLabelSides.HasFlag(LabelSides.Bottom) ? vertical : 0) + half);

            clip = Deflate(frameBounds, insetPx, insetPx, insetPx, insetPx);

            if (clip.Width <= 0 || clip.Height <= 0)
            {
                throw new InvalidOperationException(
                    "The margins, frame and edge labels leave no room for the grid. Reduce the label font size, padding or margins.");
            }

            radiusPx = s.SizingMode == GridSizingMode.AutoFitRowsColumns
                ? FitRadius(flat, Math.Max(1, s.Columns), Math.Max(1, s.Rows), clip.Width, clip.Height)
                : RadiusFromHexWidth(flat, Math.Max(1e-6, scale.ToPx(s.HexWidth)));

            if (radiusPx <= 0 || double.IsNaN(radiusPx) || double.IsInfinity(radiusPx))
            {
                throw new InvalidOperationException("The requested hex size does not resolve to a usable grid.");
            }

            (columns, rows) = CoverCounts(flat, radiusPx, clip.Width, clip.Height);
        }

        double colSpacing = flat ? 1.5 * radiusPx : Sqrt3 * radiusPx;
        double rowSpacing = flat ? Sqrt3 * radiusPx : 1.5 * radiusPx;

        // Distance covered by the hex CENTRES, which is what gets centred inside the map area.
        double spanX = (columns - 1) * colSpacing + (!flat && rows > 1 ? colSpacing / 2.0 : 0);
        double spanY = (rows - 1) * rowSpacing + (flat && columns > 1 ? rowSpacing / 2.0 : 0);

        double firstX = clip.Left + (clip.Width - spanX) / 2.0 + scale.ToPx(s.GridOffsetX);
        double firstY = clip.Top + (clip.Height - spanY) / 2.0 + scale.ToPx(s.GridOffsetY);

        (string[] columnLabels, string[] rowLabelsFinal) = CoordinateLabeller.BuildAxes(
            columns, rows, s.LabelScheme, s.CoordinateOrigin, s.SkipLettersIO, s.ZeroPadNumbers);

        var cells = new List<HexCell>(columns * rows);
        var columnCenterXs = new double[columns];
        var rowCenterYs = new double[rows];

        for (int c = 0; c < columns; c++)
        {
            columnCenterXs[c] = firstX + c * colSpacing;
        }

        for (int r = 0; r < rows; r++)
        {
            rowCenterYs[r] = firstY + r * rowSpacing;
        }

        for (int c = 0; c < columns; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                double cx = columnCenterXs[c] + (!flat && r % 2 == 1 ? colSpacing / 2.0 : 0);
                double cy = rowCenterYs[r] + (flat && c % 2 == 1 ? rowSpacing / 2.0 : 0);

                cells.Add(new HexCell
                {
                    Column = c,
                    Row = r,
                    Center = new PointF((float)cx, (float)cy),
                    Vertices = Vertices(cx, cy, radiusPx, flat),
                    Label = CoordinateLabeller.Combine(columnLabels[c], rowLabelsFinal[r], s.CoordinateSeparator),
                });
            }
        }

        double halfW = flat ? radiusPx : colSpacing / 2.0;
        double halfH = flat ? rowSpacing / 2.0 : radiusPx;
        var gridBounds = RectangleF.FromLTRB(
            (float)(firstX - halfW),
            (float)(firstY - halfH),
            (float)(firstX + spanX + halfW),
            (float)(firstY + spanY + halfH));

        return new GridLayout
        {
            CanvasWidthPx = canvasWpx,
            CanvasHeightPx = canvasHpx,
            CanvasWidthMm = canvasWmm,
            CanvasHeightMm = canvasHmm,
            Columns = columns,
            Rows = rows,
            HexRadiusPx = radiusPx,
            HexWidthPx = flat ? 2 * radiusPx : Sqrt3 * radiusPx,
            HexHeightPx = flat ? Sqrt3 * radiusPx : 2 * radiusPx,
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

    // ---------------------------------------------------------------- geometry

    private static PointF[] Vertices(double cx, double cy, double r, bool flatTop)
    {
        var pts = new PointF[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 180.0 * (flatTop ? 60 * i : 60 * i - 30);
            pts[i] = new PointF(
                (float)(cx + r * Math.Cos(angle)),
                (float)(cy + r * Math.Sin(angle)));
        }

        return pts;
    }

    /// <summary>
    /// Largest circumradius for which the requested column and row counts still span the map area.
    /// Measured centre to centre, because the outermost hexes are meant to overhang and be clipped.
    /// </summary>
    private static double FitRadius(bool flatTop, int cols, int rows, double availW, double availH)
    {
        double byWidth, byHeight;

        if (flatTop)
        {
            byWidth = cols > 1 ? availW / (1.5 * (cols - 1)) : double.PositiveInfinity;
            double vSteps = (rows - 1) + (cols > 1 ? 0.5 : 0);
            byHeight = vSteps > 0 ? availH / (Sqrt3 * vSteps) : double.PositiveInfinity;
        }
        else
        {
            double hSteps = (cols - 1) + (rows > 1 ? 0.5 : 0);
            byWidth = hSteps > 0 ? availW / (Sqrt3 * hSteps) : double.PositiveInfinity;
            byHeight = rows > 1 ? availH / (1.5 * (rows - 1)) : double.PositiveInfinity;
        }

        double r = Math.Min(byWidth, byHeight);
        return double.IsInfinity(r) ? Math.Min(availW, availH) / 2.0 : r;
    }

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
            Math.Max(1, (int)Math.Floor(available / spacing + Tolerance) + 1);
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
