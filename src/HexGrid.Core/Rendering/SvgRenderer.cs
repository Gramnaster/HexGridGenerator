using System.Drawing;
using System.Globalization;
using System.Text;
using HexGrid.Core.Scene;

namespace HexGrid.Core.Rendering;

/// <summary>
/// Writes the scene as SVG. This is the source-of-truth export: resolution independent, and every
/// layer becomes a named group that Illustrator, Affinity and Inkscape read as a real layer.
/// </summary>
public static class SvgRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const string ClipId = "mapArea";

    public static string Render(DrawScene scene, Color? background = null)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var sb = new StringBuilder(64 * 1024);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n");
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ")
          .Append("xmlns:inkscape=\"http://www.inkscape.org/namespaces/inkscape\" ")
          .Append("version=\"1.1\" ")
          .Append(Inv, $"width=\"{N(scene.WidthMm)}mm\" height=\"{N(scene.HeightMm)}mm\" ")
          .Append(Inv, $"viewBox=\"0 0 {N(scene.WidthPx)} {N(scene.HeightPx)}\" ")
          .Append("shape-rendering=\"geometricPrecision\">\n");

        sb.Append(Inv, $"  <!-- {N(scene.WidthPx)} x {N(scene.HeightPx)} px at {scene.Dpi} dpi -->\n");

        // The grid layers are clipped to the map area so the outermost hexes are cut off at the frame.
        sb.Append("  <defs>\n")
          .Append(Inv, $"    <clipPath id=\"{ClipId}\" clipPathUnits=\"userSpaceOnUse\">\n")
          .Append(Inv, $"      <rect x=\"{N(scene.ClipBounds.X)}\" y=\"{N(scene.ClipBounds.Y)}\" ")
          .Append(Inv, $"width=\"{N(scene.ClipBounds.Width)}\" height=\"{N(scene.ClipBounds.Height)}\" />\n")
          .Append("    </clipPath>\n")
          .Append("  </defs>\n");

        if (background is { } bg && bg.A > 0)
        {
            sb.Append("  <g id=\"Background\" inkscape:groupmode=\"layer\" inkscape:label=\"Background\">\n");
            sb.Append(Inv, $"    <rect x=\"0\" y=\"0\" width=\"{N(scene.WidthPx)}\" height=\"{N(scene.HeightPx)}\" ")
              .Append(Inv, $"fill=\"{Hex(bg)}\"");
            AppendOpacity(sb, "fill", bg);
            sb.Append(" />\n");
            sb.Append("  </g>\n");
        }

        foreach (SceneLayer layer in scene.Layers)
        {
            if (layer.IsEmpty)
            {
                continue;
            }

            string clip = LayerRules.IsClipped(layer.Kind) ? $" clip-path=\"url(#{ClipId})\"" : string.Empty;
            sb.Append(Inv, $"  <g id=\"{layer.Name}\" inkscape:groupmode=\"layer\" inkscape:label=\"{Split(layer.Name)}\"{clip}>\n");
            WriteItems(sb, layer.Items);
            sb.Append("  </g>\n");
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    private static void WriteItems(StringBuilder sb, IList<IDrawItem> items)
    {
        int i = 0;
        while (i < items.Count)
        {
            switch (items[i])
            {
                // Merge a run of identically styled paths into one <path>. A 40x30 grid drops from
                // 1200 elements to 1, which matters when Photoshop imports the file.
                case PathItem first:
                    i = WritePathRun(sb, items, i, first);
                    break;

                case CircleItem firstCircle:
                    i = WriteCircleRun(sb, items, i, firstCircle);
                    break;

                case RectItem r:
                    WriteRect(sb, r);
                    i++;
                    break;

                case TextItem t:
                    WriteText(sb, t);
                    i++;
                    break;

                default:
                    i++;
                    break;
            }
        }
    }

    private static int WritePathRun(StringBuilder sb, IList<IDrawItem> items, int i, PathItem first)
    {
        int j = i;
        sb.Append("    <path d=\"");
        while (j < items.Count && items[j] is PathItem p && SameStyle(first, p))
        {
            AppendPath(sb, p);
            j++;
        }

        sb.Append('"');
        AppendStroke(sb, first.Stroke, first.StrokeWidthPx);
        AppendFill(sb, first.Fill);
        sb.Append(" />\n");
        return j;
    }

    private static int WriteCircleRun(StringBuilder sb, IList<IDrawItem> items, int i, CircleItem firstCircle)
    {
        int j = i;
        sb.Append("    <path d=\"");
        while (j < items.Count && items[j] is CircleItem c && c.Fill == firstCircle.Fill)
        {
            AppendCircle(sb, c);
            j++;
        }

        sb.Append('"');
        AppendFill(sb, firstCircle.Fill);
        sb.Append(" stroke=\"none\" />\n");
        return j;
    }

    private static void WriteRect(StringBuilder sb, RectItem r)
    {
        sb.Append(Inv, $"    <rect x=\"{N(r.Rect.X)}\" y=\"{N(r.Rect.Y)}\" width=\"{N(r.Rect.Width)}\" height=\"{N(r.Rect.Height)}\"");
        AppendStroke(sb, r.Stroke, r.StrokeWidthPx);
        AppendFill(sb, r.Fill);
        sb.Append(" />\n");
    }

    private static void WriteText(StringBuilder sb, TextItem t)
    {
        double baselineY = TextMetrics.BaselineY(t.At.Y, t.FontSizePx, t.Baseline);
        string anchor = t.Anchor switch
        {
            TextAnchor.Start => "start",
            TextAnchor.End => "end",
            TextAnchor.Middle => "middle",
            _ => "middle",
        };

        sb.Append(Inv, $"    <text x=\"{N(t.At.X)}\" y=\"{N(baselineY)}\" ")
          .Append(Inv, $"font-family=\"{Escape(t.FontFamily)}\" font-size=\"{N(t.FontSizePx)}\" ")
          .Append(t.Bold ? "font-weight=\"bold\" " : string.Empty)
          .Append(Inv, $"text-anchor=\"{anchor}\" dominant-baseline=\"auto\" ")
          .Append(Inv, $"fill=\"{Hex(t.Color)}\"");
        AppendOpacity(sb, "fill", t.Color);
        sb.Append('>')
          .Append(Escape(t.Text))
          .Append("</text>\n");
    }

    // ----------------------------------------------------------------- helpers

    private static bool SameStyle(PathItem a, PathItem b) =>
        a.Stroke == b.Stroke && a.Fill == b.Fill && Math.Abs(a.StrokeWidthPx - b.StrokeWidthPx) < 1e-9;

    private static void AppendPath(StringBuilder sb, PathItem p)
    {
        for (int k = 0; k < p.Points.Length; k++)
        {
            sb.Append(k == 0 ? 'M' : 'L')
              .Append(Inv, $"{N(p.Points[k].X)} {N(p.Points[k].Y)} ");
        }

        if (p.Closed)
        {
            sb.Append("Z ");
        }
    }

    private static void AppendCircle(StringBuilder sb, CircleItem c)
    {
        SvgNumber r = N(c.RadiusPx);
        sb.Append(Inv, $"M{N(c.Center.X - c.RadiusPx)} {N(c.Center.Y)} ")
          .Append(Inv, $"a{r},{r} 0 1,0 {N(c.RadiusPx * 2)},0 ")
          .Append(Inv, $"a{r},{r} 0 1,0 {N(-c.RadiusPx * 2)},0 Z ");
    }

    private static void AppendStroke(StringBuilder sb, Color? stroke, double width)
    {
        if (stroke is not { } c || c.A == 0 || width <= 0)
        {
            sb.Append(" stroke=\"none\"");
            return;
        }

        sb.Append(Inv, $" stroke=\"{Hex(c)}\"");
        AppendOpacity(sb, "stroke", c);
        sb.Append(Inv, $" stroke-width=\"{N(width)}\" stroke-linejoin=\"round\" stroke-linecap=\"round\"");
    }

    private static void AppendFill(StringBuilder sb, Color? fill)
    {
        if (fill is not { } c || c.A == 0)
        {
            sb.Append(" fill=\"none\"");
            return;
        }

        sb.Append(Inv, $" fill=\"{Hex(c)}\"");
        AppendOpacity(sb, "fill", c);
    }

    private static void AppendOpacity(StringBuilder sb, string kind, Color c)
    {
        if (c.A != 255)
        {
            sb.Append(Inv, $" {kind}-opacity=\"{N(c.A / 255.0)}\"");
        }
    }

    private static SvgColor Hex(Color c) => new(c);

    private static SvgNumber N(double v) => new(v);

    private static string Split(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascal[i]))
            {
                sb.Append(' ');
            }

            sb.Append(pascal[i]);
        }

        return sb.ToString();
    }

    private static string Escape(string s) => s
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    // The two value types below are ISpanFormattable so that, used as holes in an interpolated
    // StringBuilder.Append, they format straight into the builder's buffer. A large grid writes
    // hundreds of thousands of numbers, and a string per number was most of the export's garbage.

    /// <summary>A coordinate or size as SVG writes it: 3 decimals, halves away from zero, never "-0".</summary>
    private readonly struct SvgNumber : ISpanFormattable
    {
        private readonly double rounded;

        public SvgNumber(double value)
        {
            // MA0193: explicit mode for consistency (see SceneBuilder.EdgeKey's Q for why the mode
            // itself doesn't practically matter here: real coordinates essentially never tie at 3 decimals).
            double r = Math.Round(value, 3, MidpointRounding.AwayFromZero);
            rounded = Math.Abs(r) < 0.0005 ? 0 : r;
        }

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            rounded.TryFormat(destination, out charsWritten, "0.###", CultureInfo.InvariantCulture);

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            rounded.ToString("0.###", CultureInfo.InvariantCulture);

        public override string ToString() => ToString(format: null, formatProvider: null);
    }

    /// <summary>An opaque colour as a lowercase #rrggbb hex triplet. Alpha is written separately as an opacity attribute.</summary>
    private readonly struct SvgColor(Color color) : ISpanFormattable
    {
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
            destination.TryWrite(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}", out charsWritten);

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}");

        public override string ToString() => ToString(format: null, formatProvider: null);
    }
}
