namespace HexGrid.Core;

/// <summary>Paper and screen canvas presets. Paper sizes are ISO 216 millimetre sizes; screen sizes are fixed pixel sizes.</summary>
public enum CanvasPreset
{
    Custom = 0,

    A2_0, // 2A0 - 1189 x 1682 mm
    A0,
    A1,
    A2,
    A3,
    A4,
    A5,
    A6,

    Uhd8K,      // 7680 x 4320
    Uhd4K,      // 3840 x 2160
    Qhd2K,      // 2560 x 1440
    Fhd1080p,   // 1920 x 1080
}

public enum PageOrientation
{
    Portrait,
    Landscape,
}

public enum LengthUnit
{
    Pixels,
    Millimeters,
    Centimeters,
    Inches,
}

public enum HexOrientation
{
    FlatTop,
    PointyTop,
}

/// <summary>How the hex size is decided.</summary>
public enum GridSizingMode
{
    /// <summary>You give rows x columns; the largest hex that fits the drawable area is computed.</summary>
    AutoFitRowsColumns,

    /// <summary>You give the hex width (corner-to-corner for flat-top, flat-to-flat for pointy-top); rows and columns are computed to fill.</summary>
    FixedHexWidth,
}

/// <summary>Which physical corner of the grid receives coordinate A1.</summary>
public enum CoordinateOrigin
{
    TopLeft,
    BottomLeft,
    TopRight,
    BottomRight,
}

/// <summary>Which axis carries letters and which carries numbers.</summary>
public enum LabelScheme
{
    /// <summary>Columns A B C..., rows 1 2 3... (A1, B1, C1).</summary>
    LettersNumbers,

    /// <summary>Columns 1 2 3..., rows A B C... (1A, 1B, 1C).</summary>
    NumbersLetters,

    /// <summary>Both axes numeric (01.01 style).</summary>
    NumbersNumbers,
}

[Flags]
public enum LabelSides
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8,
    TopLeft = Top | Left,
    All = Top | Bottom | Left | Right,
}

/// <summary>Where the per-hex coordinate label sits inside its hex.</summary>
public enum HexLabelPosition
{
    Center,
    Top,
    Bottom,
}

/// <summary>
/// The frame separating the map area from the coordinate-label band. Deliberately plain: a single
/// rule is what a grid overlay needs, and anything fancier is better done in the art package.
/// </summary>
public enum MapBorderStyle
{
    None,
    Line,
}

public enum PngBackground
{
    Transparent,
    White,
    Black,
    Custom,
}

public enum TextAnchor
{
    Start,
    Middle,
    End,
}

public enum TextBaseline
{
    Top,
    Middle,
    Bottom,
}
