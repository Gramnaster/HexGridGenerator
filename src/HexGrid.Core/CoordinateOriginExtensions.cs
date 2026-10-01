namespace HexGrid.Core;

/// <summary>Which edges of the grid a <see cref="CoordinateOrigin"/> corner sits on.</summary>
public static class CoordinateOriginExtensions
{
    public static bool IsLeft(this CoordinateOrigin origin) =>
        origin is CoordinateOrigin.TopLeft or CoordinateOrigin.BottomLeft;

    public static bool IsTop(this CoordinateOrigin origin) =>
        origin is CoordinateOrigin.TopLeft or CoordinateOrigin.TopRight;
}
