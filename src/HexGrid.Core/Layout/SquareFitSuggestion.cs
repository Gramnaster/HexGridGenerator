using System.Runtime.InteropServices;

namespace HexGrid.Core.Layout;

/// <summary>
/// A concrete suggestion for tightening an AutoFitSquares fit, from <see cref="SquareFitAdvisor.RecommendFit"/>.
/// In AutoFitRowsColumns mode <see cref="Columns"/> and <see cref="Rows"/> are the counts to enter;
/// in FixedHexWidth mode they are the counts that <see cref="SidePx"/> produces. <see cref="Leftover"/>
/// is what applying the suggestion leaves (zero when that is sub-pixel), <see cref="CurrentLeftover"/>
/// what the grid on screen leaves now.
/// </summary>
// MA0008 wants an explicit StructLayoutAttribute; see CanvasSpec.cs for the rationale for Auto
// over Sequential/Explicit - this is a plain value type from a UI hint calculation, not a hot path
// or interop boundary.
[StructLayout(LayoutKind.Auto)]
public readonly record struct SquareFitSuggestion(
    bool HasTighterFit, int Columns, int Rows, double SidePx, SquareLeftover Leftover, SquareLeftover CurrentLeftover);
