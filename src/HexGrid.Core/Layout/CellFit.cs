using System.Runtime.InteropServices;

namespace HexGrid.Core.Layout;

/// <summary>
/// One convergence pass's answer from <see cref="HexLayoutEngine"/> or <see cref="SquareLayoutEngine"/>:
/// how many cells fit and how big each one is drawn. <see cref="RadiusPx"/> is null for squares.
/// </summary>
// MA0008 wants an explicit StructLayoutAttribute; see CanvasSpec.cs for the rationale for Auto over
// Sequential/Explicit - a plain value passed between layout steps, not a hot path or interop boundary.
[StructLayout(LayoutKind.Auto)]
internal readonly record struct CellFit(int Columns, int Rows, double CellWidthPx, double CellHeightPx, double? RadiusPx);
