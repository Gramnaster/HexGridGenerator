using HexGrid.Core;
using HexGrid.Core.Layout;

namespace HexGrid.App.Tests;

public class StatusTextTests
{
    [Fact]
    public void Describe_DefaultHexSettings_ReportsHexCountsAndCellTotal()
    {
        // Arrange
        var settings = new GridSettings();
        GridLayout layout = GridLayoutEngine.Build(settings);

        // Act
        string text = StatusText.Describe(settings, layout);

        // Assert
        Assert.Contains($"{layout.Columns} × {layout.Rows} hexes", text, StringComparison.Ordinal);
        Assert.EndsWith($"{layout.Cells.Count} cells", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_InchUnit_ReportsCellWidthInInches()
    {
        // Arrange
        var settings = new GridSettings { Unit = LengthUnit.Inches, CustomWidth = 16, CustomHeight = 11, HexWidth = 0.5 };
        GridLayout layout = GridLayoutEngine.Build(settings);

        // Act
        string text = StatusText.Describe(settings, layout);

        // Assert
        Assert.Contains(" in wide)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_AutoFitSquaresWithUnevenFit_ReportsCentredGapOnTwoSides()
    {
        // Arrange: on landscape A3, 7 × 7 squares are bound by height, leaving a wide horizontal
        // margin that FlushAxis.None splits evenly between left and right.
        var settings = new GridSettings
        {
            GridType = GridType.Square,
            AutoFitSquares = true,
            FlushAxis = FlushAxis.None,
            Columns = 7,
            Rows = 7,
        };
        GridLayout layout = GridLayoutEngine.Build(settings);

        // Act
        string text = StatusText.Describe(settings, layout);

        // Assert
        Assert.Contains("gap on two sides", text, StringComparison.Ordinal);
    }
}
