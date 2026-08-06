namespace HexGrid.Core.Scene;

/// <summary>
/// Font metrics estimated from the font size alone. Good enough to reserve gutters and size label
/// background plates without dragging a text-shaping dependency into the geometry layer.
/// </summary>
public static class TextMetrics
{
    public const double AverageGlyphWidthRatio = 0.62;

    public const double LineHeightRatio = 1.0;

    public static double EstimateWidthPx(string text, double fontSizePx) =>
        text.Length * fontSizePx * AverageGlyphWidthRatio;

    public static double EstimateHeightPx(double fontSizePx) => fontSizePx * LineHeightRatio;
}
