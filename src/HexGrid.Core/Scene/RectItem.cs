using System.Drawing;

namespace HexGrid.Core.Scene;

public sealed record RectItem(RectangleF Rect, Color? Stroke, double StrokeWidthPx, Color? Fill) : IDrawItem;
