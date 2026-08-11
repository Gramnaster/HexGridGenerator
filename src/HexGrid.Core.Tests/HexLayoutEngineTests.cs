using System.Drawing;
using HexGrid.Core.Labels;
using HexGrid.Core.Layout;

namespace HexGrid.Core.Tests;

public class HexLayoutEngineTests
{
    private static readonly double Sqrt3 = Math.Sqrt(3.0);

    [Fact]
    public void Build_NullSettings_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => GridLayoutEngine.Build(null!));
    }

    [Fact]
    public void Build_AutoFitRowsColumns_NeverProducesFewerThanRequested()
    {
        // Arrange: "treated as a minimum" is the documented contract (GridSettings.Columns/Rows).
        GridSettings s = TestSettings.Minimal();
        s.SizingMode = GridSizingMode.AutoFitRowsColumns;
        s.Columns = 5;
        s.Rows = 4;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.True(layout.Columns >= 5);
        Assert.True(layout.Rows >= 4);
    }

    [Fact]
    public void Build_CellCount_EqualsColumnsTimesRows()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(layout.Columns * layout.Rows, layout.Cells.Count);
    }

    [Fact]
    public void Build_EveryCell_HasSixVertices()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.All(layout.Cells, cell => Assert.Equal(6, cell.Vertices.Length));
    }

    [Fact]
    public void Build_FlatTop_HexWidthAndHeightMatchOrientationFormula()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.HexOrientation = HexOrientation.FlatTop;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert: corner-to-corner width is 2r, flat-to-flat height is sqrt(3) * r.
        Assert.Equal(2.0 * layout.CellRadiusPx!.Value, layout.CellWidthPx, precision: 6);
        Assert.Equal(Sqrt3 * layout.CellRadiusPx!.Value, layout.CellHeightPx, precision: 6);
    }

    [Fact]
    public void Build_PointyTop_HexWidthAndHeightMatchOrientationFormula()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.HexOrientation = HexOrientation.PointyTop;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert: flat-to-flat width is sqrt(3) * r, corner-to-corner height is 2r.
        Assert.Equal(Sqrt3 * layout.CellRadiusPx!.Value, layout.CellWidthPx, precision: 6);
        Assert.Equal(2.0 * layout.CellRadiusPx!.Value, layout.CellHeightPx, precision: 6);
    }

    [Fact]
    public void Build_FixedHexWidthFlatTop_RadiusIsHalfTheRequestedWidth()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.SizingMode = GridSizingMode.FixedHexWidth;
        s.HexOrientation = HexOrientation.FlatTop;
        s.HexWidth = 20.0;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(10.0, layout.CellRadiusPx!.Value, precision: 6);
    }

    [Fact]
    public void Build_FixedHexWidthPointyTop_RadiusIsWidthOverSqrt3()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.SizingMode = GridSizingMode.FixedHexWidth;
        s.HexOrientation = HexOrientation.PointyTop;
        s.HexWidth = 20.0;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(20.0 / Sqrt3, layout.CellRadiusPx!.Value, precision: 6);
    }

    [Fact]
    public void Build_ZeroOffset_CentersGridBoundsOnClipBounds()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(ClipCenter(layout).X, GridCenter(layout).X, precision: 2);
        Assert.Equal(ClipCenter(layout).Y, GridCenter(layout).Y, precision: 2);
    }

    [Fact]
    public void Build_NonZeroOffset_ShiftsGridBoundsCenterByTheOffset()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.GridOffsetX = 15.0;
        s.GridOffsetY = -10.0;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(ClipCenter(layout).X + 15.0, GridCenter(layout).X, precision: 2);
        Assert.Equal(ClipCenter(layout).Y - 10.0, GridCenter(layout).Y, precision: 2);
    }

    [Fact]
    public void Build_CellLabel_CombinesItsOwnColumnAndRowLabel()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.CoordinateSeparator = "-";

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);
        GridCell topLeft = layout.Cells.First(c => c.Column == 0 && c.Row == 0);
        GridCell bottomRight = layout.Cells.First(c => c.Column == layout.Columns - 1 && c.Row == layout.Rows - 1);

        // Assert: guards against a transposed column/row index when the label is combined.
        Assert.Equal(CoordinateLabeller.Combine(layout.ColumnLabels[0], layout.RowLabels[0], "-"), topLeft.Label);
        Assert.Equal(
            CoordinateLabeller.Combine(layout.ColumnLabels[^1], layout.RowLabels[^1], "-"), bottomRight.Label);
    }

    [Fact]
    public void Build_SafeMarginConsumesWholeCanvas_Throws()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.SafeMargin = 1000.0;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => GridLayoutEngine.Build(s));
        Assert.Contains("safe margin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_EdgeLabelGuttersConsumeWholeClipArea_Throws()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.MarginalLabelSides = LabelSides.All;
        s.MarginalFontSize = 500.0;
        s.LabelPadding = 50.0;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => GridLayoutEngine.Build(s));
        Assert.Contains("no room for the grid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PointF ClipCenter(GridLayout layout) => new(
        layout.ClipBounds.Left + (layout.ClipBounds.Width / 2f),
        layout.ClipBounds.Top + (layout.ClipBounds.Height / 2f));

    private static PointF GridCenter(GridLayout layout) => new(
        layout.GridBounds.Left + (layout.GridBounds.Width / 2f),
        layout.GridBounds.Top + (layout.GridBounds.Height / 2f));
}
