using System.Drawing;
using HexGrid.Core.Presets;

namespace HexGrid.Core.Tests;

public class PresetIoTests
{
    [Fact]
    public void SerializeThenDeserialize_RoundTripsRepresentativeValues()
    {
        // Arrange
        var original = new GridSettings
        {
            Preset = CanvasPreset.A4,
            PageOrientation = PageOrientation.Portrait,
            Unit = LengthUnit.Centimeters,
            Dpi = 150,
            HexOrientation = HexOrientation.PointyTop,
            SizingMode = GridSizingMode.FixedHexWidth,
            Columns = 42,
            Rows = 17,
            HexWidth = 3.5,
            LineColor = Color.FromArgb(128, 10, 20, 30),
            LineOpacity = 55,
            ShowCenterDots = false,
            FontFamily = "Arial",
            LabelScheme = LabelScheme.NumbersLetters,
            CoordinateOrigin = CoordinateOrigin.BottomRight,
            SkipLettersIO = false,
            ZeroPadNumbers = true,
            CoordinateSeparator = "/",
            FileNamePattern = "Custom_{cols}",
        };

        // Act
        GridSettings loaded = PresetIo.Deserialize(PresetIo.Serialize(original));

        // Assert
        Assert.Equal(original.Preset, loaded.Preset);
        Assert.Equal(original.PageOrientation, loaded.PageOrientation);
        Assert.Equal(original.Unit, loaded.Unit);
        Assert.Equal(original.Dpi, loaded.Dpi);
        Assert.Equal(original.HexOrientation, loaded.HexOrientation);
        Assert.Equal(original.SizingMode, loaded.SizingMode);
        Assert.Equal(original.Columns, loaded.Columns);
        Assert.Equal(original.Rows, loaded.Rows);
        Assert.Equal(original.HexWidth, loaded.HexWidth);
        Assert.Equal(original.LineColor, loaded.LineColor);
        Assert.Equal(original.LineOpacity, loaded.LineOpacity);
        Assert.Equal(original.ShowCenterDots, loaded.ShowCenterDots);
        Assert.Equal(original.FontFamily, loaded.FontFamily);
        Assert.Equal(original.LabelScheme, loaded.LabelScheme);
        Assert.Equal(original.CoordinateOrigin, loaded.CoordinateOrigin);
        Assert.Equal(original.SkipLettersIO, loaded.SkipLettersIO);
        Assert.Equal(original.ZeroPadNumbers, loaded.ZeroPadNumbers);
        Assert.Equal(original.CoordinateSeparator, loaded.CoordinateSeparator);
        Assert.Equal(original.FileNamePattern, loaded.FileNamePattern);
    }

    [Fact]
    public void Serialize_OpaqueColor_WritesSixHexDigits()
    {
        // Arrange
        var s = new GridSettings { LineColor = Color.FromArgb(255, 0, 0, 0) };

        // Act
        string json = PresetIo.Serialize(s);

        // Assert
        Assert.Contains("\"LineColor\": \"#000000\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_TranslucentColor_WritesEightHexDigitsWithLeadingAlpha()
    {
        // Arrange
        var s = new GridSettings { LineColor = Color.FromArgb(128, 10, 20, 30) };

        // Act
        string json = PresetIo.Serialize(s);

        // Assert
        Assert.Contains("\"LineColor\": \"#800a141e\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_JsonNullLiteral_ThrowsInvalidDataException()
    {
        // Act & Assert
        Assert.Throws<InvalidDataException>(() => PresetIo.Deserialize("null"));
    }
}
