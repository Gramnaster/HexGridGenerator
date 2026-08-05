using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using HexGrid.Core.Rendering;
using HexGrid.Core.Scene;

namespace HexGrid.App.Rendering;

/// <summary>
/// Draws a <see cref="DrawScene"/> with GDI+. The same code path serves the live preview (scaled down)
/// and the PNG export (full resolution), so what you see really is what you get.
/// </summary>
public sealed class SceneRasterizer : IDisposable
{
    private readonly Dictionary<(string Family, float Size, bool Bold), Font> _fonts = [];
    private readonly Bitmap _measureSurface = new(1, 1);
    private readonly Graphics _measure;

    // StringFormat.GenericTypographic allocates a fresh disposable GDI+ object on every access.
    // Reading it per text item would leak handles across a grid with labels in every hex.
    private readonly StringFormat _typographic = new(StringFormat.GenericTypographic);

    private bool _disposed;

    public SceneRasterizer()
    {
        _measure = Graphics.FromImage(_measureSurface);
        _measure.PageUnit = GraphicsUnit.Pixel;
    }

    /// <summary>
    /// Rasterises the scene.
    /// </summary>
    /// <param name="scene">The scene to draw.</param>
    /// <param name="background">Canvas fill. Use <see cref="Color.Transparent"/> for an overlay.</param>
    /// <param name="antialias">Off gives hard aliased edges.</param>
    /// <param name="scale">1.0 for full resolution; less than 1 for the preview.</param>
    /// <param name="includeLayer">Optional filter, used by the layered export.</param>
    /// <param name="minStrokePx">
    /// Floor for stroke widths in <i>device</i> pixels. Keeps hairlines visible in a scaled-down preview.
    /// </param>
    public Bitmap Render(
        DrawScene scene,
        Color background,
        bool antialias,
        double scale = 1.0,
        Func<LayerKind, bool>? includeLayer = null,
        double minStrokePx = 0)
    {
        ArgumentNullException.ThrowIfNull(scene);

        int w = Math.Max(1, (int)Math.Ceiling(scene.WidthPx * scale));
        int h = Math.Max(1, (int)Math.Ceiling(scene.HeightPx * scale));

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        bmp.SetResolution((float)(scene.Dpi * scale), (float)(scene.Dpi * scale));

        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.PageUnit = GraphicsUnit.Pixel;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = antialias ? SmoothingMode.AntiAlias : SmoothingMode.None;
            g.PixelOffsetMode = antialias ? PixelOffsetMode.HighQuality : PixelOffsetMode.None;
            g.TextRenderingHint = antialias
                ? TextRenderingHint.AntiAlias      // not ClearType: subpixel AA fringes on transparency
                : TextRenderingHint.SingleBitPerPixelGridFit;

            g.Clear(background);
            g.ScaleTransform((float)scale, (float)scale);

            // Stroke floor is expressed in device pixels, so convert into world units.
            double minStrokeWorld = scale > 0 ? minStrokePx / scale : 0;

            foreach (SceneLayer layer in scene.Layers)
            {
                if (layer.IsEmpty || (includeLayer is not null && !includeLayer(layer.Kind)))
                {
                    continue;
                }

                // Grid layers are cut off at the frame; labels, frame and scale bar are not.
                if (LayerRules.IsClipped(layer.Kind))
                {
                    g.SetClip(scene.ClipBounds);
                }
                else
                {
                    g.ResetClip();
                }

                foreach (IDrawItem item in layer.Items)
                {
                    DrawItem(g, item, minStrokeWorld);
                }
            }

            g.ResetClip();
        }

        return bmp;
    }

    private void DrawItem(Graphics g, IDrawItem item, double minStrokeWorld)
    {
        switch (item)
        {
            case PathItem p when p.Points.Length >= 2:
                if (p.Fill is { A: > 0 } fill)
                {
                    using (var brush = new SolidBrush(fill))
                    {
                        g.FillPolygon(brush, p.Points);
                    }
                }

                if (p.Stroke is { A: > 0 } stroke && p.StrokeWidthPx > 0)
                {
                    using var pen = MakePen(stroke, p.StrokeWidthPx, minStrokeWorld);
                    if (p.Closed)
                    {
                        g.DrawPolygon(pen, p.Points);
                    }
                    else
                    {
                        g.DrawLines(pen, p.Points);
                    }
                }

                break;

            case LineItem l when l.Stroke.A > 0 && l.StrokeWidthPx > 0:
            {
                using var pen = MakePen(l.Stroke, l.StrokeWidthPx, minStrokeWorld);
                g.DrawLine(pen, l.A, l.B);
                break;
            }

            case CircleItem c when c.Fill.A > 0 && c.RadiusPx > 0:
            {
                using var brush = new SolidBrush(c.Fill);
                float r = (float)c.RadiusPx;
                g.FillEllipse(brush, c.Center.X - r, c.Center.Y - r, r * 2, r * 2);
                break;
            }

            case RectItem r:
                if (r.Fill is { A: > 0 } rectFill)
                {
                    using (var brush = new SolidBrush(rectFill))
                    {
                        g.FillRectangle(brush, r.Rect);
                    }
                }

                if (r.Stroke is { A: > 0 } rectStroke && r.StrokeWidthPx > 0)
                {
                    using var pen = MakePen(rectStroke, r.StrokeWidthPx, minStrokeWorld);
                    g.DrawRectangle(pen, r.Rect.X, r.Rect.Y, r.Rect.Width, r.Rect.Height);
                }

                break;

            case TextItem t when t.Color.A > 0 && t.FontSizePx > 0 && !string.IsNullOrEmpty(t.Text):
                DrawText(g, t);
                break;
        }
    }

    private void DrawText(Graphics g, TextItem t)
    {
        Font font = GetFont(t.FontFamily, (float)t.FontSizePx, t.Bold);
        FontFamily family = font.FontFamily;

        float emHeight = family.GetEmHeight(font.Style);
        float ascent = emHeight > 0 ? font.Size * family.GetCellAscent(font.Style) / emHeight : font.Size * 0.8f;

        // Shared with the SVG writer so both exports place text identically.
        double baselineY = SvgRenderer.BaselineY(t.At.Y, t.FontSizePx, t.Baseline);

        SizeF size = _measure.MeasureString(t.Text, font, PointF.Empty, _typographic);
        float x = t.Anchor switch
        {
            HexGrid.Core.TextAnchor.Start => t.At.X,
            HexGrid.Core.TextAnchor.End => t.At.X - size.Width,
            _ => t.At.X - size.Width / 2f,
        };

        using var brush = new SolidBrush(t.Color);
        g.DrawString(t.Text, font, brush, x, (float)baselineY - ascent, _typographic);
    }

    private static Pen MakePen(Color color, double width, double minWidth) =>
        new(color, (float)Math.Max(width, minWidth))
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

    private Font GetFont(string family, float sizePx, bool bold)
    {
        var key = (family, sizePx, bold);
        if (_fonts.TryGetValue(key, out Font? cached))
        {
            return cached;
        }

        // The string overload falls back to a system font when the family is unknown, which is what we want.
        var font = new Font(family, Math.Max(1f, sizePx), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        _fonts[key] = font;
        return font;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (Font f in _fonts.Values)
        {
            f.Dispose();
        }

        _fonts.Clear();
        _typographic.Dispose();
        _measure.Dispose();
        _measureSurface.Dispose();
    }
}
