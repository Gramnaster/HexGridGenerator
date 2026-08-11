using System.Drawing;

namespace HexGrid.Core.Scene;

/// <summary>A fully resolved drawing in device pixels, ready for any renderer.</summary>
public sealed class DrawScene
{
    // MA0016: only DrawScene itself mutates the layer list (Layer(), below), so the public surface
    // exposes IReadOnlyList<T> and keeps the mutable List<T> as a private backing field.
    private readonly List<SceneLayer> layers = [];

    public required double WidthPx { get; init; }

    public required double HeightPx { get; init; }

    public required double WidthMm { get; init; }

    public required double HeightMm { get; init; }

    public required int Dpi { get; init; }

    /// <summary>The map area. Layers where <see cref="LayerRules.IsClipped"/> is true are clipped to it.</summary>
    public required RectangleF ClipBounds { get; init; }

    /// <summary>Which shape the cell layers hold, so their exported names read "Square..." instead of "Hex...".</summary>
    public required GridType GridType { get; init; }

    public IReadOnlyList<SceneLayer> Layers => layers;

    public SceneLayer Layer(LayerKind kind)
    {
        SceneLayer? existing = layers.Find(l => l.Kind == kind);
        if (existing is not null)
        {
            return existing;
        }

        var layer = new SceneLayer(kind, LayerName(kind));
        layers.Add(layer);
        layers.Sort((a, b) => a.Kind.CompareTo(b.Kind));
        return layer;
    }

    private string LayerName(LayerKind kind)
    {
        if (GridType != GridType.Square)
        {
            return kind.ToString();
        }

        return kind switch
        {
            LayerKind.HexFill => "SquareFill",
            LayerKind.HexGrid => "SquareGrid",
            LayerKind.HexLabels => "SquareLabels",
            _ => kind.ToString(),
        };
    }
}
