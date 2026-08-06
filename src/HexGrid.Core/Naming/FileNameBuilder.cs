using System.Globalization;
using System.Text;
using HexGrid.Core.Layout;
using HexGrid.Core.Units;

namespace HexGrid.Core.Naming;

/// <summary>Expands the filename pattern tokens against a solved layout.</summary>
public static class FileNameBuilder
{
    public static string Build(GridSettings s, GridLayout layout)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(layout);

        var scale = new UnitScale(s.Unit, s.Dpi);

        string raw = s.FileNamePattern
            .Replace("{preset}", CanvasPresets.ShortName(s.Preset), StringComparison.OrdinalIgnoreCase)
            .Replace("{w}", Round(layout.CanvasWidthPx), StringComparison.OrdinalIgnoreCase)
            .Replace("{h}", Round(layout.CanvasHeightPx), StringComparison.OrdinalIgnoreCase)
            .Replace("{cols}", layout.Columns.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{rows}", layout.Rows.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{hexw}", Round(layout.HexWidthPx), StringComparison.OrdinalIgnoreCase)
            .Replace("{hexwu}", Trim(scale.FromPx(layout.HexWidthPx)), StringComparison.OrdinalIgnoreCase)
            .Replace("{dpi}", s.Dpi.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{orient}", s.HexOrientation == HexOrientation.FlatTop ? "flat" : "pointy", StringComparison.OrdinalIgnoreCase);

        string cleaned = Sanitise(raw).Trim('_', '-', ' ');
        return cleaned.Length == 0 ? "HexGrid" : cleaned;
    }

    // MA0193: explicit AwayFromZero rather than the implicit ToEven default, matching the "round
    // half up" a user would expect from a number embedded in a filename.
    private static string Round(double v) =>
        Math.Round(v, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    private static string Trim(double v) =>
        Math.Round(v, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Sanitise(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 || c == ' ' ? '_' : c);
        }

        return sb.ToString();
    }
}
