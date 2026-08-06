using System.Drawing;

namespace HexGrid.Core.Layout;

/// <summary>
/// The complete geometric solution for one set of settings. Pure numbers, no drawing.
/// </summary>
/// <remarks>
/// Layout order from the canvas edge inward: safe margin, coordinate-label band, frame rule
/// (<see cref="FrameBounds"/>), then the map area (<see cref="ClipBounds"/>). Every hex centre lies
/// inside the map area and the outermost hexes overhang it, so the grid meets the frame on all four
/// sides with no gap. Renderers clip the grid layers to <see cref="ClipBounds"/>.
/// </remarks>
public sealed class GridLayout
{
    public required double CanvasWidthPx { get; init; }

    public required double CanvasHeightPx { get; init; }

    /// <summary>Physical canvas size in millimetres. Always populated, derived from DPI for pixel-defined canvases.</summary>
    public required double CanvasWidthMm { get; init; }

    public required double CanvasHeightMm { get; init; }

    public required int Columns { get; init; }

    public required int Rows { get; init; }

    /// <summary>Centre to vertex distance.</summary>
    public required double HexRadiusPx { get; init; }

    /// <summary>Corner-to-corner for flat-top, flat-to-flat for pointy-top.</summary>
    public required double HexWidthPx { get; init; }

    public required double HexHeightPx { get; init; }

    /// <summary>Centreline of the frame rule. Equal to <see cref="ClipBounds"/> when the grid inset is 0.</summary>
    public required RectangleF FrameBounds { get; init; }

    /// <summary>The map area. Grid layers are clipped to this; hexes overhang it and are cut off.</summary>
    public required RectangleF ClipBounds { get; init; }

    /// <summary>Tight bounding box of the untrimmed hex block. Larger than <see cref="ClipBounds"/>.</summary>
    public required RectangleF GridBounds { get; init; }

    public required IReadOnlyList<HexCell> Cells { get; init; }

    /// <summary>X centre of each column, for top and bottom edge labels. All lie inside <see cref="ClipBounds"/>.</summary>
    public required double[] ColumnCenterXs { get; init; }

    /// <summary>Y centre of each row, for left and right edge labels.</summary>
    public required double[] RowCenterYs { get; init; }

    /// <summary>Column labels in display order, left to right.</summary>
    public required string[] ColumnLabels { get; init; }

    /// <summary>Row labels in display order, top to bottom.</summary>
    public required string[] RowLabels { get; init; }
}
