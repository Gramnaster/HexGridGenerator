using System.Runtime.InteropServices;

namespace HexGrid.Core.Layout;

/// <summary>
/// A concrete (Columns, Rows) suggestion for tightening an AutoFitSquares fit, from
/// <see cref="SquareLayoutEngine.RecommendFit"/>. <see cref="GapPx"/> is the total leftover space
/// on whichever axis is not bound (split evenly as margin on both of that axis's sides), for
/// <see cref="Columns"/> and <see cref="Rows"/> - i.e. the gap after applying the suggestion, or
/// the gap already present if <see cref="HasTighterFit"/> is false.
/// </summary>
// MA0008 wants an explicit StructLayoutAttribute; see CanvasSpec.cs for the rationale for Auto
// over Sequential/Explicit - this is a plain value type from a UI hint calculation, not a hot path
// or interop boundary.
[StructLayout(LayoutKind.Auto)]
public readonly record struct SquareFitSuggestion(
    bool HasTighterFit, int Columns, int Rows, double SidePx, double GapPx, double CurrentGapPx);
