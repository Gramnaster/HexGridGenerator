using System.Drawing;

namespace HexGrid.Core.Scene;

public sealed record TextItem(
    string Text,
    PointF At,
    TextAnchor Anchor,
    TextBaseline Baseline,
    string FontFamily,
    double FontSizePx,
    bool Bold,
    Color Color) : IDrawItem;
