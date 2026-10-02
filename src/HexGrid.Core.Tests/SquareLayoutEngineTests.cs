using HexGrid.Core.Layout;

namespace HexGrid.Core.Tests;

public class SquareLayoutEngineTests
{
    [Fact]
    public void Build_FixedSizeCellGap_KeepsDrawnSquareSizeButWidensSpacing()
    {
        // Arrange: in fixed-size mode the square size is an input, so the gap never touches it.
        GridSettings baseline = FixedSize(50.0);

        GridSettings gapped = FixedSize(50.0);
        gapped.CellGapX = 6.0;
        gapped.CellGapY = 2.0;

        // Act
        GridLayout baselineLayout = GridLayoutEngine.Build(baseline);
        GridLayout gappedLayout = GridLayoutEngine.Build(gapped);

        // Assert: the square's own drawn size never changes because of the gap.
        Assert.Equal(baselineLayout.CellWidthPx, gappedLayout.CellWidthPx, precision: 6);
        GridCell baseCell = baselineLayout.Cells.First(c => c.Column == 0 && c.Row == 0);
        GridCell gapCell = gappedLayout.Cells.First(c => c.Column == 0 && c.Row == 0);
        double baseWidth = baseCell.Vertices[1].X - baseCell.Vertices[0].X;
        double gapWidth = gapCell.Vertices[1].X - gapCell.Vertices[0].X;
        Assert.Equal(baseWidth, gapWidth, precision: 4);

        // Assert: Gap X/Gap Y widen the pitch between neighbouring centres independently, by
        // exactly the requested gap on each axis - the square shape itself never changes, so
        // different Gap X/Gap Y values can never turn it into a rectangle.
        GridCell baseColNeighbor = baselineLayout.Cells.First(c => c.Column == 1 && c.Row == 0);
        GridCell gapColNeighbor = gappedLayout.Cells.First(c => c.Column == 1 && c.Row == 0);
        double columnPitchDelta = (gapColNeighbor.Center.X - gapCell.Center.X) - (baseColNeighbor.Center.X - baseCell.Center.X);
        Assert.Equal(6.0, columnPitchDelta, precision: 4);

        GridCell baseRowNeighbor = baselineLayout.Cells.First(c => c.Column == 0 && c.Row == 1);
        GridCell gapRowNeighbor = gappedLayout.Cells.First(c => c.Column == 0 && c.Row == 1);
        double rowPitchDelta = (gapRowNeighbor.Center.Y - gapCell.Center.Y) - (baseRowNeighbor.Center.Y - baseCell.Center.Y);
        Assert.Equal(2.0, rowPitchDelta, precision: 4);
    }

    [Fact]
    public void Build_FixedSizeLargeCellGap_FitsFewerColumnsAndRows()
    {
        // Arrange: the square's own size is held fixed (previous test), so a large enough gap eats
        // into how many whole squares fit in the same map area.
        GridSettings baseline = FixedSize(50.0);
        GridSettings gapped = FixedSize(50.0);
        gapped.CellGapX = 60.0;
        gapped.CellGapY = 60.0;

        // Act
        GridLayout baselineLayout = GridLayoutEngine.Build(baseline);
        GridLayout gappedLayout = GridLayoutEngine.Build(gapped);

        // Assert
        Assert.True(gappedLayout.Columns < baselineLayout.Columns);
        Assert.True(gappedLayout.Rows < baselineLayout.Rows);
    }

    [Fact]
    public void Build_AutoFitCountsWithCellGap_KeepsRequestedCountsAndShrinksSquares()
    {
        // Arrange: CSS Grid-style gap. 5 × 4 in a 400 × 300 map area with Gap X 6, Gap Y 2: the
        // gaps come out of the squares, so side = min((400 - 4·6) / 5, (300 - 3·2) / 4) = 73.5
        // and the four rows plus three gaps fill the height exactly.
        GridSettings s = TestSettings.Minimal();
        s.GridType = GridType.Square;
        s.CellGapX = 6.0;
        s.CellGapY = 2.0;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal((5, 4), (layout.Columns, layout.Rows));
        Assert.Equal(73.5, layout.CellWidthPx, 6);
        Assert.Equal(300.0, layout.GridBounds.Height, 3);
    }

    [Fact]
    public void Build_AutoFitCountsWithGapFillingTheArea_ThrowsNamingTheGap()
    {
        // Arrange: four 100 px gaps between five columns consume the whole 400 px width.
        GridSettings s = TestSettings.Minimal();
        s.GridType = GridType.Square;
        s.CellGapX = 100.0;

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => GridLayoutEngine.Build(s));

        // Assert
        Assert.Contains("Gap", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AutoFitCountsOnDefaultA3WithBorderAndLabels_KeepsAllRequestedColumns()
    {
        // Arrange: regression. The side used to be computed as a float division and then divided
        // back into the map width to count columns, which came out at 29.99999 and floored to 29.
        var s = new GridSettings { GridType = GridType.Square };

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal((30, 20), (layout.Columns, layout.Rows));
    }

    private static GridSettings FixedSize(double side)
    {
        GridSettings s = TestSettings.Minimal();
        s.GridType = GridType.Square;
        s.SizingMode = GridSizingMode.FixedHexWidth;
        s.SquareSize = side;
        return s;
    }
}
