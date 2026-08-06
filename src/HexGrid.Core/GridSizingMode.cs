namespace HexGrid.Core;

/// <summary>How the hex size is decided.</summary>
public enum GridSizingMode
{
    /// <summary>You give rows x columns; the largest hex that fits the drawable area is computed.</summary>
    AutoFitRowsColumns,

    /// <summary>You give the hex width (corner-to-corner for flat-top, flat-to-flat for pointy-top); rows and columns are computed to fill.</summary>
    FixedHexWidth,
}
