namespace HexGrid.Core;

/// <summary>
/// Font metrics estimated from the font size alone. Good enough to reserve label gutters and place
/// text baselines without dragging a text-shaping dependency into the geometry layer. Shared by the
/// layout engine and both renderers so PNG and SVG agree on where text sits.
/// </summary>
public static class TextMetrics
{
    /// <summary>Rough advance width of one glyph as a fraction of the font size, for gutter reservation.</summary>
    public const double AverageGlyphWidthRatio = 0.62;

    /// <summary>Cap-height-ish line box as a fraction of the font size.</summary>
    public const double LineHeightRatio = 1.0;

    /// <summary>Nominal ascent as a fraction of the em, used to place text baselines.</summary>
    public const double AscentRatio = 0.80;

    public static double EstimateWidthPx(int charCount, double fontSizePx) =>
        charCount * fontSizePx * AverageGlyphWidthRatio;

    public static double EstimateHeightPx(double fontSizePx) => fontSizePx * LineHeightRatio;

    /// <summary>Converts a box-relative vertical anchor into an alphabetic baseline position.</summary>
    public static double BaselineY(double y, double fontSizePx, TextBaseline baseline) => baseline switch
    {
        TextBaseline.Top => y + (AscentRatio * fontSizePx),
        TextBaseline.Bottom => y - ((1 - AscentRatio) * fontSizePx),
        TextBaseline.Middle => y + ((AscentRatio - 0.5) * fontSizePx),
        _ => y + ((AscentRatio - 0.5) * fontSizePx),
    };
}
