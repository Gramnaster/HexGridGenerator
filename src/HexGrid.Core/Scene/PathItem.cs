using System.Drawing;

namespace HexGrid.Core.Scene;

/// <summary>Closed or open path. <paramref name="Fill"/> and <paramref name="Stroke"/> already carry their alpha.</summary>
public sealed record PathItem(PointF[] Points, bool Closed, Color? Stroke, double StrokeWidthPx, Color? Fill) : IDrawItem;
