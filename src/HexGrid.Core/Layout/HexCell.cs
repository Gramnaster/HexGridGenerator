using System.Drawing;

namespace HexGrid.Core.Layout;

/// <summary>One hex, fully resolved in device pixels. Renderers consume these and know nothing about the settings.</summary>
public sealed class HexCell
{
    public required int Column { get; init; }

    public required int Row { get; init; }

    public required PointF Center { get; init; }

    /// <summary>Six vertices, clockwise, starting at the rightmost for flat-top and the top for pointy-top.</summary>
    public required PointF[] Vertices { get; init; }

    /// <summary>Full coordinate label, e.g. "A1".</summary>
    public required string Label { get; init; }
}
