namespace HexGrid.Core;

/// <summary>
/// Which axis of a fitted square grid's leftover slack is pushed entirely to one side - the side
/// away from <see cref="CoordinateOrigin"/> - instead of split evenly as a centred margin. Only
/// applies when <see cref="GridSettings.AutoFitSquares"/> is on; hexes always fill and clip, so
/// there is no margin to redistribute.
/// </summary>
public enum FlushAxis
{
    None,
    Vertical,
    Horizontal,
    Both,
}
