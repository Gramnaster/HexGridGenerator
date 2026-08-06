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

    /// <summary>Nominal ascent as a fraction of the em, used to place text baselines.</summary>
    public const double AscentRatio = 0.80;

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
              .Append(Inv, $"fill=\"{Hex(bg)}\"{Opacity("fill", bg)} />\n");
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

                case LineItem l:
                    WriteLine(sb, l);
                    i++;
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
        var d = new StringBuilder();
        while (j < items.Count && items[j] is PathItem p && SameStyle(first, p))
        {
            AppendPath(d, p);
            j++;
        }

        sb.Append("    <path d=\"").Append(d).Append('"')
          .Append(StrokeAttrs(first.Stroke, first.StrokeWidthPx))
          .Append(FillAttrs(first.Fill))
          .Append(" />\n");
        return j;
    }

    private static int WriteCircleRun(StringBuilder sb, IList<IDrawItem> items, int i, CircleItem firstCircle)
    {
        int j = i;
        var d = new StringBuilder();
        while (j < items.Count && items[j] is CircleItem c && c.Fill == firstCircle.Fill)
        {
            AppendCircle(d, c);
            j++;
        }

        sb.Append("    <path d=\"").Append(d).Append('"')
          .Append(FillAttrs(firstCircle.Fill))
          .Append(" stroke=\"none\" />\n");
        return j;
    }

    private static void WriteLine(StringBuilder sb, LineItem l) =>
        sb.Append(Inv, $"    <line x1=\"{N(l.A.X)}\" y1=\"{N(l.A.Y)}\" x2=\"{N(l.B.X)}\" y2=\"{N(l.B.Y)}\"")
          .Append(StrokeAttrs(l.Stroke, l.StrokeWidthPx))
          .Append(" />\n");

    private static void WriteRect(StringBuilder sb, RectItem r) =>
        sb.Append(Inv, $"    <rect x=\"{N(r.Rect.X)}\" y=\"{N(r.Rect.Y)}\" width=\"{N(r.Rect.Width)}\" height=\"{N(r.Rect.Height)}\"")
          .Append(StrokeAttrs(r.Stroke, r.StrokeWidthPx))
          .Append(FillAttrs(r.Fill))
          .Append(" />\n");

    private static void WriteText(StringBuilder sb, TextItem t)
    {
        double baselineY = BaselineY(t.At.Y, t.FontSizePx, t.Baseline);
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
          .Append(Inv, $"fill=\"{Hex(t.Color)}\"{Opacity("fill", t.Color)}>")
          .Append(Escape(t.Text))
          .Append("</text>\n");
    }

    /// <summary>
    /// Converts a box-relative vertical anchor into an alphabetic baseline position. Shared with the
    /// GDI+ renderer so PNG and SVG place text identically.
    /// </summary>
    public static double BaselineY(double y, double fontSizePx, TextBaseline baseline) => baseline switch
    {
        TextBaseline.Top => y + (AscentRatio * fontSizePx),
        TextBaseline.Bottom => y - ((1 - AscentRatio) * fontSizePx),
        TextBaseline.Middle => y + ((AscentRatio - 0.5) * fontSizePx),
        _ => y + ((AscentRatio - 0.5) * fontSizePx),
    };

    // ----------------------------------------------------------------- helpers

    private static bool SameStyle(PathItem a, PathItem b) =>
        a.Stroke == b.Stroke && a.Fill == b.Fill && Math.Abs(a.StrokeWidthPx - b.StrokeWidthPx) < 1e-9;

    private static void AppendPath(StringBuilder d, PathItem p)
    {
        for (int k = 0; k < p.Points.Length; k++)
        {
            d.Append(k == 0 ? 'M' : 'L')
             .Append(N(p.Points[k].X)).Append(' ').Append(N(p.Points[k].Y)).Append(' ');
        }

        if (p.Closed)
        {
            d.Append("Z ");
        }
    }

    private static void AppendCircle(StringBuilder d, CircleItem c)
    {
        string r = N(c.RadiusPx);
        d.Append('M').Append(N(c.Center.X - c.RadiusPx)).Append(' ').Append(N(c.Center.Y)).Append(' ')
         .Append('a').Append(r).Append(',').Append(r).Append(" 0 1,0 ").Append(N(c.RadiusPx * 2)).Append(",0 ")
         .Append('a').Append(r).Append(',').Append(r).Append(" 0 1,0 ").Append(N(-c.RadiusPx * 2)).Append(",0 Z ");
    }

    private static string StrokeAttrs(Color? stroke, double width)
    {
        if (stroke is not { } c || c.A == 0 || width <= 0)
        {
            return " stroke=\"none\"";
        }

        return string.Create(Inv,
            $" stroke=\"{Hex(c)}\"{Opacity("stroke", c)} stroke-width=\"{N(width)}\" stroke-linejoin=\"round\" stroke-linecap=\"round\"");
    }

    private static string FillAttrs(Color? fill)
    {
        if (fill is not { } c || c.A == 0)
        {
            return " fill=\"none\"";
        }

        return string.Create(Inv, $" fill=\"{Hex(c)}\"{Opacity("fill", c)}");
    }

    private static string Opacity(string kind, Color c) =>
        c.A == 255 ? string.Empty : string.Create(Inv, $" {kind}-opacity=\"{N(c.A / 255.0)}\"");

    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    private static string N(double v)
    {
        // MA0193: explicit mode for consistency (see SceneBuilder.EdgeKey's Q for why the mode itself
        // doesn't practically matter here — real coordinates essentially never tie at 3 decimals).
        double r = Math.Round(v, 3, MidpointRounding.AwayFromZero);
        if (Math.Abs(r) < 0.0005)
        {
            r = 0;
        }

        return r.ToString("0.###", Inv);
    }

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
}
