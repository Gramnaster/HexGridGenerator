namespace HexGrid.Core.Scene;

public static class LayerRules
{
    /// <summary>
    /// True for the layers that make up the grid itself. These are clipped to the map area so the
    /// outermost hexes are cut off cleanly at the frame instead of leaving a gap.
    /// </summary>
    public static bool IsClipped(LayerKind kind) =>
        kind is LayerKind.HexFill or LayerKind.HexGrid or LayerKind.CenterDots or LayerKind.HexLabels;
}
