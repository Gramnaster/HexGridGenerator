namespace HexGrid.Core.Units;

/// <summary>Converts the user's chosen length unit into device pixels at a given DPI.</summary>
public sealed class UnitScale
{
    public UnitScale(LengthUnit unit, int dpi)
    {
        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be positive.");
        }

        Unit = unit;
        Dpi = dpi;
        PixelsPerUnit = unit switch
        {
            LengthUnit.Pixels => 1.0,
            LengthUnit.Inches => dpi,
            LengthUnit.Centimeters => dpi / 2.54,
            LengthUnit.Millimeters => dpi / 25.4,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
        };
    }

    public LengthUnit Unit { get; }

    public int Dpi { get; }

    public double PixelsPerUnit { get; }

    /// <summary>User-facing length to device pixels.</summary>
    public double ToPx(double value) => value * PixelsPerUnit;

    /// <summary>Device pixels back to the user-facing length.</summary>
    public double FromPx(double px) => px / PixelsPerUnit;

    /// <summary>Typographic points to device pixels. Points are always 1/72 inch, independent of <see cref="Unit"/>.</summary>
    public double PointsToPx(double points) => points / 72.0 * Dpi;

    public static double MmToPx(double mm, int dpi) => mm / 25.4 * dpi;

    public static double PxToMm(double px, int dpi) => px * 25.4 / dpi;
}
