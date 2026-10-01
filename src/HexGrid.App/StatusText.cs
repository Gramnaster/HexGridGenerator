using System.Globalization;
using HexGrid.Core;
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
    // instead of overhanging - see SquareLayoutEngine.RecommendFit. In AutoFitRowsColumns mode this
    // reports the tightest achievable (Columns, Rows) and its leftover gap; in FixedHexWidth mode
    // it reports the square size that would produce that same tight fit for the stored Columns/Rows.
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
        (bool columnsBound, int threshold) = HexLayoutEngine.SizingBindingHint(settings, layout.ClipBounds);
        return columnsBound
            ? F($"  ·  Rows need ≥ {threshold} to matter")
            : F($"  ·  Columns need ≥ {threshold} to matter");
    }

    private static string SquareFitHint(GridSettings settings, GridLayout layout)
    {
        var scale = new UnitScale(settings.Unit, settings.Dpi);
        SquareFitSuggestion fit = SquareLayoutEngine.RecommendFit(settings, layout.ClipBounds);
        string unit = UnitSuffix(settings.Unit);

        // FlushAxis moves the whole leftover onto one side instead of splitting it - report the full
        // residual and name that side, rather than the "half on each side" wording centred mode gets.
        (bool columnsBound, _) = SquareLayoutEngine.SizingBindingHint(settings, layout.ClipBounds);
        bool verticalGap = columnsBound;
        bool flushed = settings.AutoFitSquares &&
            (verticalGap ? settings.FlushAxis.FlushesVertically() : settings.FlushAxis.FlushesHorizontally());
        string sideDescription = flushed ? $"on the {FarSideName(settings, verticalGap)}" : "on two sides";

        double gapNow = scale.FromPx(flushed ? fit.CurrentGapPx : fit.CurrentGapPx / 2.0);
        if (gapNow < 0.05)
        {
            return string.Empty;
        }

        if (!fit.HasTighterFit)
        {
            return F($"  ·  ≈{gapNow:0.##} {unit} gap {sideDescription} (canvas doesn't divide evenly by {settings.Columns} × {settings.Rows})");
        }

        if (fit.GapPx <= 0)
        {
            return F($"  ·  ≈{gapNow:0.##} {unit} gap {sideDescription} - try {fit.Columns} × {fit.Rows} for no gap");
        }

        double gapAfter = scale.FromPx(flushed ? fit.GapPx : fit.GapPx / 2.0);
        return F($"  ·  ≈{gapNow:0.##} {unit} gap {sideDescription} - try {fit.Columns} × {fit.Rows} for ≈{gapAfter:0.##} {unit}");
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
        SquareFitSuggestion fit = SquareLayoutEngine.RecommendFit(settings, layout.ClipBounds);
        double recommendedSize = scale.FromPx(fit.SidePx);

        return fit.GapPx <= 0
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
