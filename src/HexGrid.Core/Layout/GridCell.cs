using System.Drawing;

namespace HexGrid.Core.Layout;

/// <summary>One cell (hex or square), fully resolved in device pixels. Renderers consume these and know nothing about the settings.</summary>
public sealed class GridCell
{
    public required int Column { get; init; }

    public required int Row { get; init; }

    public required PointF Center { get; init; }

    /// <summary>Vertices, clockwise: six for a hex (starting at the rightmost for flat-top, the top for pointy-top), four for a square (starting top-left).</summary>
    public required PointF[] Vertices { get; init; }

    /// <summary>Full coordinate label, e.g. "A1".</summary>
    public required string Label { get; init; }
}
