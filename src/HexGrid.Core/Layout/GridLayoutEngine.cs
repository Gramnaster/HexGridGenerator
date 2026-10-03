using System.Drawing;
using HexGrid.Core.Labels;
using HexGrid.Core.Settings;
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
    public static GridLayout Build(GridSettings s)
    {
        ArgumentNullException.ThrowIfNull(s);

        var scale = new UnitScale(s.Unit, s.Dpi);
        (double canvasWpx, double canvasHpx, double canvasWmm, double canvasHmm) = ResolveCanvas(s, scale);

        double framePx = FrameRuleWidthPx(s, scale);
        (CellFit fit, RectangleF frameBounds, RectangleF clip) =
            SolveGrid(s, scale, SafeRect(s, scale, canvasWpx, canvasHpx), framePx);

        (string[] columnLabels, string[] rowLabels) = BuildAxes(s, fit.Columns, fit.Rows);

        (IReadOnlyList<GridCell> cells, double[] columnCenterXs, double[] rowCenterYs, RectangleF gridBounds) = s.GridType switch
        {
            GridType.Square => SquareLayoutEngine.BuildCells(s, scale, fit, clip, columnLabels, rowLabels),
            GridType.Hex => HexLayoutEngine.BuildCells(s, scale, fit, clip, columnLabels, rowLabels),
            _ => throw new ArgumentOutOfRangeException(nameof(s), s.GridType, message: null),
        };

        RectangleF nominalClip = clip;
        double insetPx = Math.Max(0, scale.ToPx(s.GridInset));
        (frameBounds, clip) = ShrinkFrameToFlushedGrid(s, insetPx, frameBounds, clip, gridBounds);

        return new GridLayout
        {
            CanvasWidthPx = canvasWpx,
            CanvasHeightPx = canvasHpx,
            CanvasWidthMm = canvasWmm,
            CanvasHeightMm = canvasHmm,
            Columns = fit.Columns,
            Rows = fit.Rows,
            CellRadiusPx = fit.RadiusPx,
            CellWidthPx = fit.CellWidthPx,
            CellHeightPx = fit.CellHeightPx,
            FrameRuleWidthPx = framePx,
            FrameBounds = frameBounds,
            ClipBounds = clip,
            NominalClipBounds = nominalClip,
            GridBounds = gridBounds,
            Cells = cells,
            ColumnCenterXs = columnCenterXs,
            RowCenterYs = rowCenterYs,
            ColumnLabels = columnLabels,
            RowLabels = rowLabels,
        };
    }

    /// <summary>
    /// Solves only what <see cref="Build"/> solves before placing cells: the cell counts and size, and
    /// the map area they were sized for (<see cref="GridLayout.NominalClipBounds"/>). For callers that
    /// need to try many candidate settings exactly as Build would resolve them, without paying for
    /// cells, labels and frame geometry each time.
    /// </summary>
    internal static (CellFit Fit, RectangleF NominalClip) SolveFit(GridSettings s)
    {
        var scale = new UnitScale(s.Unit, s.Dpi);
        (double canvasWpx, double canvasHpx, _, _) = ResolveCanvas(s, scale);
        (CellFit fit, _, RectangleF clip) =
            SolveGrid(s, scale, SafeRect(s, scale, canvasWpx, canvasHpx), FrameRuleWidthPx(s, scale));
        return (fit, clip);
    }

    private static RectangleF SafeRect(GridSettings s, UnitScale scale, double canvasWpx, double canvasHpx)
    {
        double safePx = Math.Max(0, scale.ToPx(s.SafeMargin));
        RectangleF safeRect = Deflate(
            new RectangleF(0, 0, (float)canvasWpx, (float)canvasHpx), safePx, safePx, safePx, safePx);

        if (safeRect.Width <= 0 || safeRect.Height <= 0)
        {
            throw new InvalidOperationException("The safe margin consumes the whole canvas. Reduce it or enlarge the canvas.");
        }

        return safeRect;
    }

    /// <summary>
    /// Solves columns, rows, cell size and the frame/clip rectangles by converging on the label
    /// gutter width. The label gutter depends on how many characters the labels run to, which
    /// depends on the row and column counts, which depend on the gutter. Three passes converge.
    /// </summary>
    private static (CellFit Fit, RectangleF FrameBounds, RectangleF ClipBounds) SolveGrid(
        GridSettings s, UnitScale scale, RectangleF safeRect, double framePx)
    {
        double insetPx = Math.Max(0, scale.ToPx(s.GridInset));
        double labelPadPx = Math.Max(0, scale.ToPx(s.LabelPadding));
        double marginalFontPx = scale.PointsToPx(s.MarginalFontSize);

        var fit = new CellFit(Math.Max(1, s.Columns), Math.Max(1, s.Rows), CellWidthPx: 0, CellHeightPx: 0, RadiusPx: null);
        RectangleF frameBounds = safeRect;
        RectangleF clip = safeRect;

        for (int pass = 0; pass < 3; pass++)
        {
            int rowChars = CoordinateLabeller.MaxRowLabelLength(fit.Rows, s.LabelScheme, s.SkipLettersIO);

            double horizontal = labelPadPx + TextMetrics.EstimateWidthPx(rowChars, marginalFontPx);
            double vertical = labelPadPx + TextMetrics.EstimateHeightPx(marginalFontPx);

            frameBounds = ComputeFrameBounds(s, safeRect, framePx, horizontal, vertical);
            clip = Deflate(frameBounds, insetPx, insetPx, insetPx, insetPx);

            if (clip.Width <= 0 || clip.Height <= 0)
            {
                throw new InvalidOperationException(
                    "The margins, frame and edge labels leave no room for the grid. Reduce the label font size, padding or margins.");
            }

            fit = s.GridType switch
            {
                GridType.Square => SquareLayoutEngine.Solve(s, scale, clip),
                GridType.Hex => HexLayoutEngine.Solve(s, scale, clip),
                _ => throw new ArgumentOutOfRangeException(nameof(s), s.GridType, message: null),
            };
        }

        return (fit, frameBounds, clip);
    }

    private static (string[] ColumnLabels, string[] RowLabels) BuildAxes(GridSettings s, int columns, int rows) =>
        CoordinateLabeller.BuildAxes(columns, rows, s.LabelScheme, s.CoordinateOrigin, s.SkipLettersIO, s.ZeroPadNumbers);

    /// <summary>Stroke width of the frame rule, or 0 when no border is drawn.</summary>
    private static double FrameRuleWidthPx(GridSettings s, UnitScale scale) =>
        s.BorderStyle == MapBorderStyle.None ? 0 : Math.Max(0, scale.ToPx(s.BorderThickness));

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

        float left = clip.Left;
        float top = clip.Top;
        float right = clip.Right;
        float bottom = clip.Bottom;

        if (s.FlushAxis.FlushesVertically())
        {
            if (s.CoordinateOrigin.IsTop())
            {
                bottom = gridBounds.Bottom;
            }
            else
            {
                top = gridBounds.Top;
            }
        }

        if (s.FlushAxis.FlushesHorizontally())
        {
            if (s.CoordinateOrigin.IsLeft())
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
        if (s.PageOrientation == PageOrientation.Portrait)
        {
            (px, py) = (py, px);
        }

        return (px, py, UnitScale.PxToMm(px, s.Dpi), UnitScale.PxToMm(py, s.Dpi));
    }
}
