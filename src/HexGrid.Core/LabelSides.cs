namespace HexGrid.Core;

[Flags]
public enum LabelSides
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    TopLeft = Top | Left,
    Right = 8,
    All = Top | Bottom | Left | Right,
}
