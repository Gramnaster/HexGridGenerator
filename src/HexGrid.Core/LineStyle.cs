namespace HexGrid.Core;

/// <summary>
/// How each grid-line segment between two cells is drawn. Applies identically to hex and square
/// grids: <see cref="SceneBuilder"/> walks the same deduplicated edge set either way.
/// </summary>
public enum LineStyle
{
    /// <summary>The full edge, unbroken.</summary>
    Solid,

    /// <summary>
    /// Only a short arm at each end of the edge, reaching in from the intersection. A vertex shared
    /// by several edges ends up with one arm per edge meeting there - a plus sign where four square
    /// edges meet, a three-legged mark where three hex edges meet - so the eye fills in the rest of
    /// the line itself.
    /// </summary>
    Crosshair,
}
