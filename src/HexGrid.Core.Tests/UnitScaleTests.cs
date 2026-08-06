using HexGrid.Core.Units;

namespace HexGrid.Core.Tests;

public class UnitScaleTests
{
    [Fact]
    public void ToPx_Millimeters_ConvertsUsingDpi()
    {
        // Arrange: 25.4mm is exactly one inch.
        var scale = new UnitScale(LengthUnit.Millimeters, dpi: 300);

        // Act
        double px = scale.ToPx(25.4);

        // Assert
        Assert.Equal(300.0, px, precision: 6);
    }

    [Fact]
    public void ToPx_Inches_ConvertsUsingDpi()
    {
        // Arrange
        var scale = new UnitScale(LengthUnit.Inches, dpi: 300);

        // Act
        double px = scale.ToPx(1.0);

        // Assert
        Assert.Equal(300.0, px, precision: 6);
    }

    [Fact]
    public void ToPx_Pixels_IsIdentity()
    {
        // Arrange
        var scale = new UnitScale(LengthUnit.Pixels, dpi: 96);

        // Act
        double px = scale.ToPx(42.5);

        // Assert
        Assert.Equal(42.5, px, precision: 9);
    }

    [Theory]
    [InlineData(LengthUnit.Pixels)]
    [InlineData(LengthUnit.Millimeters)]
    [InlineData(LengthUnit.Centimeters)]
    [InlineData(LengthUnit.Inches)]
    public void FromPx_RoundTripsWithToPx(LengthUnit unit)
    {
        // Arrange
        var scale = new UnitScale(unit, dpi: 300);

        // Act
        double roundTripped = scale.FromPx(scale.ToPx(17.3));

        // Assert
        Assert.Equal(17.3, roundTripped, precision: 6);
    }

    [Fact]
    public void PointsToPx_IsIndependentOfLengthUnit()
    {
        // Arrange: points are always 1/72 inch, unaffected by the length-unit choice.
        var mm = new UnitScale(LengthUnit.Millimeters, dpi: 300);
        var inches = new UnitScale(LengthUnit.Inches, dpi: 300);

        // Act
        double fromMm = mm.PointsToPx(72.0);
        double fromInches = inches.PointsToPx(72.0);

        // Assert
        Assert.Equal(300.0, fromMm, precision: 6);
        Assert.Equal(300.0, fromInches, precision: 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveDpi_Throws(int dpi)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnitScale(LengthUnit.Pixels, dpi));
    }

    [Fact]
    public void MmToPx_PxToMm_RoundTrip()
    {
        // Act
        double px = UnitScale.MmToPx(210.0, dpi: 300);
        double mm = UnitScale.PxToMm(px, dpi: 300);

        // Assert
        Assert.Equal(210.0, mm, precision: 6);
    }
}
