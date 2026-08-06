using System.Drawing;

namespace HexGrid.Core.Scene;

public sealed record CircleItem(PointF Center, double RadiusPx, Color Fill) : IDrawItem;
