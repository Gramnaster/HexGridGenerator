namespace HexGrid.Core;

/// <summary>Which axes a <see cref="FlushAxis"/> value flushes.</summary>
public static class FlushAxisExtensions
{
    public static bool FlushesHorizontally(this FlushAxis axis) =>
        axis is FlushAxis.Horizontal or FlushAxis.Both;

    public static bool FlushesVertically(this FlushAxis axis) =>
        axis is FlushAxis.Vertical or FlushAxis.Both;
}
