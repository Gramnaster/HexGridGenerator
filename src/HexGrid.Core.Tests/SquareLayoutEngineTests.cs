using HexGrid.Core.Layout;

namespace HexGrid.Core.Tests;

public class SquareLayoutEngineTests
{
    [Fact]
    public void Build_CellGap_KeepsDrawnSquareSizeButWidensSpacing()
    {
        // Arrange
        GridSettings baseline = TestSettings.Minimal();
        baseline.GridType = GridType.Square;

        GridSettings gapped = TestSettings.Minimal();
        gapped.GridType = GridType.Square;
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
    public void Build_LargeCellGap_CanReduceColumnsOrRowsBelowRequested()
    {
        // Arrange: the square's own size is held fixed (previous test), so a large enough gap eats
        // into how many whole squares fit in the same map area, even though AutoFitSquares
        // normally guarantees the exact requested count when Gap is 0.
        GridSettings s = TestSettings.Minimal();
        s.GridType = GridType.Square;
        s.CellGapX = 60.0;
        s.CellGapY = 60.0;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.True(layout.Columns < 5 || layout.Rows < 4);
    }
}
