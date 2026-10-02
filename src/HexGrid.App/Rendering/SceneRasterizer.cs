using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using HexGrid.Core;
using HexGrid.Core.Scene;

namespace HexGrid.App.Rendering;

/// <summary>
/// Draws a <see cref="DrawScene"/> with GDI+. The same code path serves the live preview (scaled down)
/// and the PNG export (full resolution), so what you see really is what you get.
/// </summary>
public sealed class SceneRasterizer : IDisposable
{
    private readonly Dictionary<(string Family, float Size, bool Bold), Font> _fonts = [];

    // Ascent in pixels per cached font. Three GDI+ metric calls per label otherwise, for a value
    // that only depends on the font.
    private readonly Dictionary<Font, float> _ascents = new(ReferenceEqualityComparer.Instance);
    private readonly Bitmap _measureSurface = new(1, 1);
    private readonly Graphics _measure;

    // StringFormat.GenericTypographic allocates a fresh disposable GDI+ object on every access.
    // Reading it per text item would leak handles across a grid with labels in every hex.
    private readonly StringFormat _typographic = new(StringFormat.GenericTypographic);

    // Pens and brushes for one Render call, keyed by ARGB (and width for pens). A grid draws
    // thousands of items in a handful of colours, and creating plus disposing a GDI+ object per
    // item was a measurable share of the raster. Released at the end of every Render, so the
    // handles never outlive the call that made them.
    private readonly Dictionary<(int Argb, float Width), Pen> _pens = [];
    private readonly Dictionary<int, SolidBrush> _brushes = [];

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

        // DPI metadata (SetResolution) belongs to the exported *file*, not this bitmap - callers that
        // export set it explicitly (ExportService). Baking scene.Dpi * scale in here tagged preview
        // bitmaps (scale < 1) with a fractional, meaningless DPI, and GDI+ sizes some draw calls off
        // that metadata rather than raw pixel count, which made the live preview render oversized and
        // get clipped by the panel instead of showing the whole page.
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);

        using (Graphics g = Graphics.FromImage(bmp))
        {
            ConfigureGraphics(g, antialias);
            g.Clear(background);
            g.ScaleTransform((float)scale, (float)scale);

            // Stroke floor is expressed in device pixels, so convert into world units.
            double minStrokeWorld = scale > 0 ? minStrokePx / scale : 0;

            try
            {
                DrawLayers(g, scene, includeLayer, minStrokeWorld);
            }
            finally
            {
                ReleasePensAndBrushes();
            }

            g.ResetClip();
        }

        return bmp;
    }

    private static void ConfigureGraphics(Graphics g, bool antialias)
    {
        g.PageUnit = GraphicsUnit.Pixel;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = antialias ? SmoothingMode.AntiAlias : SmoothingMode.None;
        g.PixelOffsetMode = antialias ? PixelOffsetMode.HighQuality : PixelOffsetMode.None;
        g.TextRenderingHint = antialias
            ? TextRenderingHint.AntiAlias      // not ClearType: subpixel AA fringes on transparency
            : TextRenderingHint.SingleBitPerPixelGridFit;
    }

    private void DrawLayers(Graphics g, DrawScene scene, Func<LayerKind, bool>? includeLayer, double minStrokeWorld)
    {
        foreach (SceneLayer layer in scene.Layers)
        {
            if (layer.IsEmpty || (includeLayer is not null && !includeLayer(layer.Kind)))
            {
                continue;
            }

            // Grid layers are cut off at the frame; labels and the frame itself are not.
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
    }

    private void DrawItem(Graphics g, IDrawItem item, double minStrokeWorld)
    {
        switch (item)
        {
            case PathItem p when p.Points.Length >= 2:
                DrawPathItem(g, p, minStrokeWorld);
                break;

            case CircleItem c when c.Fill.A > 0 && c.RadiusPx > 0:
            {
                float r = (float)c.RadiusPx;
                g.FillEllipse(GetBrush(c.Fill), c.Center.X - r, c.Center.Y - r, r * 2, r * 2);
                break;
            }

            case RectItem r:
                DrawRectItem(g, r, minStrokeWorld);
                break;

            case TextItem t when t.Color.A > 0 && t.FontSizePx > 0 && !string.IsNullOrEmpty(t.Text):
                DrawText(g, t);
                break;
        }
    }

    private void DrawPathItem(Graphics g, PathItem p, double minStrokeWorld)
    {
        if (p.Fill is { A: > 0 } fill)
        {
            g.FillPolygon(GetBrush(fill), p.Points);
        }

        if (p.Stroke is { A: > 0 } stroke && p.StrokeWidthPx > 0)
        {
            if (p.Closed)
            {
                g.DrawPolygon(GetPen(stroke, p.StrokeWidthPx, minStrokeWorld), p.Points);
            }
            else
            {
                g.DrawLines(GetPen(stroke, p.StrokeWidthPx, minStrokeWorld), p.Points);
            }
        }
    }

    private void DrawRectItem(Graphics g, RectItem r, double minStrokeWorld)
    {
        if (r.Fill is { A: > 0 } rectFill)
        {
            g.FillRectangle(GetBrush(rectFill), r.Rect);
        }

        if (r.Stroke is { A: > 0 } rectStroke && r.StrokeWidthPx > 0)
        {
            g.DrawRectangle(GetPen(rectStroke, r.StrokeWidthPx, minStrokeWorld), r.Rect.X, r.Rect.Y, r.Rect.Width, r.Rect.Height);
        }
    }

    private void DrawText(Graphics g, TextItem t)
    {
        // IDISP001: font is borrowed from the _fonts cache, not created here: GetFont only creates
        // on a cache miss and stores the result in _fonts, which owns it and disposes it in Dispose()
        // below. Disposing it here would break every later draw call that reuses the same cache entry.
#pragma warning disable IDISP001
        Font font = GetFont(t.FontFamily, (float)t.FontSizePx, t.Bold);
#pragma warning restore IDISP001
        float ascent = GetAscent(font);

        // Shared with the SVG writer so both exports place text identically.
        double baselineY = TextMetrics.BaselineY(t.At.Y, t.FontSizePx, t.Baseline);

        SizeF size = _measure.MeasureString(t.Text, font, PointF.Empty, _typographic);
        float x = t.Anchor switch
        {
            HexGrid.Core.TextAnchor.Start => t.At.X,
            HexGrid.Core.TextAnchor.End => t.At.X - size.Width,
            HexGrid.Core.TextAnchor.Middle => t.At.X - (size.Width / 2f),
            _ => t.At.X - (size.Width / 2f),
        };

        g.DrawString(t.Text, font, GetBrush(t.Color), x, (float)baselineY - ascent, _typographic);
    }

    /// <summary>Borrowed from the per-Render cache. Callers must not dispose it.</summary>
    private Pen GetPen(Color color, double width, double minWidth)
    {
        var key = (color.ToArgb(), (float)Math.Max(width, minWidth));
        if (_pens.TryGetValue(key, out Pen? cached))
        {
            return cached;
        }

        _pens.Add(key, new Pen(color, key.Item2)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        });
        return _pens[key];
    }

    /// <summary>Borrowed from the per-Render cache. Callers must not dispose it.</summary>
    private SolidBrush GetBrush(Color color)
    {
        int key = color.ToArgb();
        if (_brushes.TryGetValue(key, out SolidBrush? cached))
        {
            return cached;
        }

        _brushes.Add(key, new SolidBrush(color));
        return _brushes[key];
    }

    private void ReleasePensAndBrushes()
    {
        foreach (Pen pen in _pens.Values)
        {
            pen.Dispose();
        }

        foreach (SolidBrush brush in _brushes.Values)
        {
            brush.Dispose();
        }

        _pens.Clear();
        _brushes.Clear();
    }

    private float GetAscent(Font font)
    {
        if (_ascents.TryGetValue(font, out float cached))
        {
            return cached;
        }

        FontFamily family = font.FontFamily;
        float emHeight = family.GetEmHeight(font.Style);
        float ascent = emHeight > 0 ? font.Size * family.GetCellAscent(font.Style) / emHeight : (float)(font.Size * TextMetrics.AscentRatio);
        _ascents[font] = ascent;
        return ascent;
    }

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
        _ascents.Clear();
        _typographic.Dispose();
        _measure.Dispose();
        _measureSurface.Dispose();
    }
}
