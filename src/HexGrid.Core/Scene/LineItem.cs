using System.Drawing;

namespace HexGrid.Core.Scene;

public sealed record LineItem(PointF A, PointF B, Color Stroke, double StrokeWidthPx) : IDrawItem;
