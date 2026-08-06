namespace HexGrid.Core;

/// <summary>
/// The frame separating the map area from the coordinate-label band. Deliberately plain: a single
/// rule is what a grid overlay needs, and anything fancier is better done in the art package.
/// </summary>
public enum MapBorderStyle
{
    None,
    Line,
}
