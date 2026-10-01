using System.Runtime.InteropServices;

namespace HexGrid.Core.Layout;

/// <summary>
/// Map-area space a fitted square grid leaves unused on each axis, in device pixels. With no cell gap
/// one axis is always flush; once Gap X/Gap Y are set, both axes can carry leftover.
/// </summary>
// MA0008 wants an explicit StructLayoutAttribute; see CanvasSpec.cs for the rationale for Auto
// over Sequential/Explicit - a plain value type from a UI hint calculation, not a hot path.
[StructLayout(LayoutKind.Auto)]
public readonly record struct SquareLeftover(double XPx, double YPx)
{
    public double TotalPx => XPx + YPx;
}
