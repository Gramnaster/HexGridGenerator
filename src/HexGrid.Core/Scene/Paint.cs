using System.Drawing;

namespace HexGrid.Core.Scene;

/// <summary>Colour helpers. Opacity is expressed as a 0-100 percentage everywhere in the settings.</summary>
public static class Paint
{
    public static Color WithOpacity(Color color, int percent)
    {
        int clamped = Math.Clamp(percent, 0, 100);

        // MA0193: the implicit default is banker's rounding (ToEven), which is not cosmetic here —
        // 30% opacity is 255 * 0.3 = 76.5, and ToEven rounds that down to 76 while AwayFromZero (the
        // "round half up" a user actually expects from a percentage slider) rounds to 77.
        int alpha = (int)Math.Round(255.0 * clamped / 100.0, MidpointRounding.AwayFromZero);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    public static bool IsInvisible(Color c) => c.A == 0;
}
