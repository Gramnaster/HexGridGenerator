namespace HexGrid.Core.Scene;

/// <summary>
/// Layers exist so the output can be dropped into Photoshop, Affinity or Krita as separable pieces.
/// Order here is bottom to top.
/// </summary>
public enum LayerKind
{
    HexFill,
    HexGrid,
    CenterDots,
    HexLabels,
    EdgeLabels,
    Border,
}
