using System.Drawing;

namespace HexGrid.Core.Scene;

/// <summary>
/// Layers exist so the output can be dropped into Photoshop, Affinity or Krita as separable pieces.
/// Order here is bottom to top.
/// </summary>
public enum LayerKind
{
    Background,
    HexFill,
    HexGrid,
    CenterDots,
    HexLabels,
    EdgeLabels,
    Border,
}

public static class LayerRules
{
    /// <summary>
    /// True for the layers that make up the grid itself. These are clipped to the map area so the
    /// outermost hexes are cut off cleanly at the frame instead of leaving a gap.
    /// </summary>
    public static bool IsClipped(LayerKind kind) =>
        kind is LayerKind.HexFill or LayerKind.HexGrid or LayerKind.CenterDots or LayerKind.HexLabels;
}

public interface IDrawItem;

/// <summary>Closed or open path. <paramref name="Fill"/> and <paramref name="Stroke"/> already carry their alpha.</summary>
public sealed record PathItem(PointF[] Points, bool Closed, Color? Stroke, double StrokeWidthPx, Color? Fill) : IDrawItem;

public sealed record LineItem(PointF A, PointF B, Color Stroke, double StrokeWidthPx) : IDrawItem;

public sealed record CircleItem(PointF Center, double RadiusPx, Color Fill) : IDrawItem;

public sealed record RectItem(RectangleF Rect, Color? Stroke, double StrokeWidthPx, Color? Fill) : IDrawItem;

public sealed record TextItem(
    string Text,
    PointF At,
    TextAnchor Anchor,
    TextBaseline Baseline,
    string FontFamily,
    double FontSizePx,
    bool Bold,
    Color Color) : IDrawItem;

public sealed class SceneLayer(LayerKind kind, string name)
{
    public LayerKind Kind { get; } = kind;

    public string Name { get; } = name;

    public List<IDrawItem> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;
}

/// <summary>A fully resolved drawing in device pixels, ready for any renderer.</summary>
public sealed class DrawScene
{
    public required double WidthPx { get; init; }

    public required double HeightPx { get; init; }

    public required double WidthMm { get; init; }

    public required double HeightMm { get; init; }

    public required int Dpi { get; init; }

    /// <summary>The map area. Layers where <see cref="LayerRules.IsClipped"/> is true are clipped to it.</summary>
    public required RectangleF ClipBounds { get; init; }

    public List<SceneLayer> Layers { get; } = [];

    public SceneLayer Layer(LayerKind kind)
    {
        SceneLayer? existing = Layers.Find(l => l.Kind == kind);
        if (existing is not null)
        {
            return existing;
        }

        var layer = new SceneLayer(kind, kind.ToString());
        Layers.Add(layer);
        Layers.Sort((a, b) => a.Kind.CompareTo(b.Kind));
        return layer;
    }
}

/// <summary>Colour helpers. Opacity is expressed as a 0-100 percentage everywhere in the settings.</summary>
public static class Paint
{
    public static Color WithOpacity(Color color, int percent)
    {
        int clamped = Math.Clamp(percent, 0, 100);
        int alpha = (int)Math.Round(255.0 * clamped / 100.0);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    public static bool IsInvisible(Color c) => c.A == 0;
}

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
