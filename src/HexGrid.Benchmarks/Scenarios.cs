using HexGrid.Core;

namespace HexGrid.Benchmarks;

internal static class Scenarios
{
    /// <summary>
    /// Fits an A3 page into a typical preview panel (about 900 px wide for a 4961 px canvas). The
    /// live preview renders at this scale on every rebuild.
    /// </summary>
    public const double PreviewScale = 0.18;

    public static GridSettings Create(Scenario scenario) => scenario switch
    {
        Scenario.DefaultHex => new GridSettings(),
        Scenario.LargeHexLabelled => new GridSettings { Columns = 120, Rows = 80, ShowHexLabels = true },
        Scenario.SquareAutoFitGapped => new GridSettings
        {
            GridType = GridType.Square,
            AutoFitSquares = true,
            Columns = 40,
            Rows = 28,
            CellGapX = 0.5,
            CellGapY = 0.5,
        },
        Scenario.A0HexLabelled => new GridSettings
        {
            Preset = CanvasPreset.A0,
            SizingMode = GridSizingMode.FixedHexWidth,
            HexWidth = 12,
            ShowHexLabels = true,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, message: null),
    };
}
