using System.Globalization;
using HexGrid.Core.Settings;
using HexGrid.Core.Layout;
using HexGrid.Core.Units;

namespace HexGrid.App;

/// <summary>
/// Builds the status-bar summary for a solved layout: canvas, cell counts and size, plus the
/// sizing hint for whichever axis currently drives the grid. Pure string formatting with no
/// controls involved, kept out of <see cref="MainForm"/> so the form only owns UI wiring.
/// </summary>
internal static class StatusText
{
    public static string Describe(GridSettings settings, GridLayout layout)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(layout);

        var scale = new UnitScale(settings.Unit, settings.Dpi);
        string unit = UnitSuffix(settings.Unit);

        string canvas = settings.Preset == CanvasPreset.Custom
            ? "Custom"
            : CanvasPresets.ShortName(settings.Preset) + OrientationSuffix();

        string OrientationSuffix() =>
            CanvasPresets.Resolve(settings.Preset).IsPaper
                ? " " + settings.PageOrientation.ToString().ToLowerInvariant()
                : string.Empty;

        bool hexGrid = settings.GridType != GridType.Square;
        string sizingHint = SizingHint(settings, layout, hexGrid);
        string cellsWord = hexGrid ? "hexes" : "squares";
        string sizeLine = hexGrid
            ? F($"hex {layout.CellWidthPx:0.#} × {layout.CellHeightPx:0.#} px ") +
              F($"({scale.FromPx(layout.CellWidthPx):0.##} {unit} wide)  ·  ")
            : F($"square {layout.CellWidthPx:0.#} px ") +
              F($"({scale.FromPx(layout.CellWidthPx):0.##} {unit} side)  ·  ");

        // MA0076: number formatting here is deliberately locale-aware (this is status text read by a
        // local interactive user, not machine-parsed output, unlike SvgRenderer's InvariantCulture,
        // which is a file-format requirement). CurrentCulture is spelled out explicitly rather than
        // left implicit, via the FormattableString overload so each interpolated line still reads
        // cleanly instead of a wall of string.Create(...) calls.
        return
            F($"{canvas}  ·  {layout.CanvasWidthPx:0} × {layout.CanvasHeightPx:0} px @ {settings.Dpi} dpi ") +
            F($"({layout.CanvasWidthMm:0.#} × {layout.CanvasHeightMm:0.#} mm)  ·  ") +
            F($"{layout.Columns} × {layout.Rows} {cellsWord}") + sizingHint + F($"  ·  ") +
            sizeLine +
            F($"{layout.Cells.Count} cells");
    }

    // Hex: AutoFitRowsColumns only. Columns and Rows share one resolved hex size, so whichever axis
    // implies the smaller size wins and the other is inert; hexes always fill the frame by
    // overhanging and clipping, so there is no gap to report, just which axis currently matters.
    //
    // Square + AutoFitSquares: unlike hex, the non-binding axis leaves a real, visible margin
    // instead of overhanging - see SquareFitAdvisor.RecommendFit. In AutoFitRowsColumns mode this
    // reports the tightest achievable (Columns, Rows) and its leftover gap; in FixedHexWidth mode
    // it reports the square size, near the current one, that fits tightest.
    private static string SizingHint(GridSettings settings, GridLayout layout, bool hexGrid)
    {
        if (hexGrid)
        {
            return settings.SizingMode == GridSizingMode.AutoFitRowsColumns
                ? HexSizingHint(settings, layout)
                : string.Empty;
        }

        if (!settings.AutoFitSquares)
        {
            return string.Empty;
        }

        return settings.SizingMode == GridSizingMode.AutoFitRowsColumns
            ? SquareFitHint(settings, layout)
            : SquareRecommendedSizeHint(settings, layout);
    }

    private static string HexSizingHint(GridSettings settings, GridLayout layout)
    {
        (bool columnsBound, int threshold) = HexLayoutEngine.SizingBindingHint(settings, layout.NominalClipBounds);
        return columnsBound
            ? F($"  ·  Rows need ≥ {threshold} to matter")
            : F($"  ·  Columns need ≥ {threshold} to matter");
    }

    private static string SquareFitHint(GridSettings settings, GridLayout layout)
    {
        var scale = new UnitScale(settings.Unit, settings.Dpi);
        SquareFitSuggestion fit = SquareFitAdvisor.RecommendFit(settings, layout);

        string now = DescribeLeftover(settings, scale, fit.CurrentLeftover);
        if (now.Length == 0)
        {
            return string.Empty;
        }

        if (!fit.HasTighterFit)
        {
            return F($"  ·  {now} (canvas doesn't divide evenly by {settings.Columns} × {settings.Rows})");
        }

        string after = DescribeLeftover(settings, scale, fit.Leftover);
        return after.Length == 0
            ? F($"  ·  {now} - try {fit.Columns} × {fit.Rows} for no gap")
            : F($"  ·  {now} - try {fit.Columns} × {fit.Rows} for {after}");
    }

    /// <summary>
    /// One phrase per axis with a visible leftover, e.g. "≈3.2 mm gap left and right". A centred axis
    /// splits its leftover between both sides; a flushed axis puts all of it on the side away from
    /// CoordinateOrigin. Empty when neither axis has a visible leftover.
    /// </summary>
    private static string DescribeLeftover(GridSettings settings, UnitScale scale, SquareLeftover leftover)
    {
        string unit = UnitSuffix(settings.Unit);
        string horizontal = AxisGap(leftover.XPx, settings.FlushAxis.FlushesHorizontally(), verticalAxis: false, "left and right");
        string vertical = AxisGap(leftover.YPx, settings.FlushAxis.FlushesVertically(), verticalAxis: true, "top and bottom");

        if (horizontal.Length == 0 || vertical.Length == 0)
        {
            return horizontal + vertical;
        }

        return horizontal + ", " + vertical;

        string AxisGap(double px, bool flushed, bool verticalAxis, string bothSides)
        {
            double shown = scale.FromPx(flushed ? px : px / 2.0);
            if (shown < 0.05)
            {
                return string.Empty;
            }

            string where = flushed ? "on the " + FarSideName(settings, verticalAxis) : bothSides;
            return F($"≈{shown:0.##} {unit} gap {where}");
        }
    }

    /// <summary>The side that receives the whole leftover margin when FlushAxis is on for this axis: opposite CoordinateOrigin.</summary>
    private static string FarSideName(GridSettings settings, bool verticalAxis)
    {
        if (verticalAxis)
        {
            return settings.CoordinateOrigin.IsTop() ? "bottom" : "top";
        }

        return settings.CoordinateOrigin.IsLeft() ? "right" : "left";
    }

    private static string SquareRecommendedSizeHint(GridSettings settings, GridLayout layout)
    {
        var scale = new UnitScale(settings.Unit, settings.Dpi);
        SquareFitSuggestion fit = SquareFitAdvisor.RecommendFit(settings, layout);

        // "0.##" matches SquareFitAdvisor.SuggestedSideDecimals, which already rounded the side
        // down to this precision so the value shown is the value that fits.
        double recommendedSize = scale.FromPx(fit.SidePx);

        return fit.Leftover.TotalPx <= 0
            ? F($"  ·  {fit.Columns} × {fit.Rows} squares at {recommendedSize:0.##} {UnitSuffix(settings.Unit)} side gives no gap")
            : F($"  ·  {fit.Columns} × {fit.Rows} squares fit tightest at {recommendedSize:0.##} {UnitSuffix(settings.Unit)} side");
    }

    private static string UnitSuffix(LengthUnit unit) => unit switch
    {
        LengthUnit.Pixels => "px",
        LengthUnit.Millimeters => "mm",
        LengthUnit.Centimeters => "cm",
        LengthUnit.Inches => "in",
        _ => "px",
    };

    // MA0076: explicit CurrentCulture - this is status text read by a local interactive user, not
    // machine-parsed output. Shared by every status-bar/hint builder in this file.
    private static string F(FormattableString s) => s.ToString(CultureInfo.CurrentCulture);
}
