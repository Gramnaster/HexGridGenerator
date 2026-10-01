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
    public void Describe_AutoFitSquaresWithUnevenFit_ReportsCentredGapLeftAndRight()
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
        Assert.Contains("gap left and right", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_SquareFlushedFromBottomRightOrigin_ReportsGapOnTheLeft()
    {
        // Arrange: same height-bound 7 × 7 fit, but FlushAxis moves the whole horizontal leftover to
        // the side away from the origin corner. BottomRight's far horizontal side is left. The frame
        // shrinks to hug the grid, so the hint must measure the area the grid was sized for, not the
        // shrunk frame, or it sees no gap at all.
        var settings = new GridSettings
        {
            GridType = GridType.Square,
            AutoFitSquares = true,
            FlushAxis = FlushAxis.Both,
            CoordinateOrigin = CoordinateOrigin.BottomRight,
            Columns = 7,
            Rows = 7,
        };
        GridLayout layout = GridLayoutEngine.Build(settings);

        // Act
        string text = StatusText.Describe(settings, layout);

        // Assert
        Assert.Contains("gap on the left", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_FixedSizeSquares_RecommendationIgnoresFlushAxis()
    {
        // Arrange: the recommended square size answers "what side fits Columns × Rows into this map
        // area", which flushing the leftover to one side doesn't change.
        GridSettings Settings(FlushAxis flush) => new()
        {
            GridType = GridType.Square,
            AutoFitSquares = true,
            SizingMode = GridSizingMode.FixedHexWidth,
            SquareSize = 30,
            FlushAxis = flush,
        };
        GridSettings centred = Settings(FlushAxis.None);
        GridSettings flushed = Settings(FlushAxis.Both);

        // Act
        string centredText = StatusText.Describe(centred, GridLayoutEngine.Build(centred));
        string flushedText = StatusText.Describe(flushed, GridLayoutEngine.Build(flushed));

        // Assert
        Assert.Equal(centredText, flushedText);
    }
}
