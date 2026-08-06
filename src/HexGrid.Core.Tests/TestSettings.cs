namespace HexGrid.Core.Tests;

/// <summary>
/// Baseline <see cref="GridSettings"/> for geometry tests: Custom/Pixels makes every length pass
/// through <see cref="Units.UnitScale"/> as an identity conversion, and every gutter (safe margin,
/// grid inset, border, edge labels) is switched off so ClipBounds == the raw canvas rect. Individual
/// tests turn a specific gutter back on to exercise it in isolation.
/// </summary>
internal static class TestSettings
{
    public static GridSettings Minimal() => new()
    {
        Preset = CanvasPreset.Custom,
        Unit = LengthUnit.Pixels,
        CustomWidth = 400,
        CustomHeight = 300,
        Dpi = 300,
        SafeMargin = 0,
        GridInset = 0,
        BorderStyle = MapBorderStyle.None,
        MarginalLabelSides = LabelSides.None,
        Columns = 5,
        Rows = 4,
    };
}
