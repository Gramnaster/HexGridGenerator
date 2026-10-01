using HexGrid.Core.Layout;

namespace HexGrid.Core.Tests;

public class GridLayoutEngineFlushTests
{
    [Fact]
    public void Build_SquareFlushHorizontalFromTopLeft_ClipHugsGridOnRightAndNominalKeepsFullArea()
    {
        // Arrange: 400 × 300 px map area, 5 × 5 auto-fit squares → side 60 (height-bound), so the
        // block is 300 px wide and leaves 100 px of horizontal slack. Flushing from a left origin
        // pushes that slack to the right and shrinks the clip to end where the grid ends.
        GridSettings s = TestSettings.Minimal();
        s.GridType = GridType.Square;
        s.AutoFitSquares = true;
        s.Columns = 5;
        s.Rows = 5;
        s.CoordinateOrigin = CoordinateOrigin.TopLeft;
        s.FlushAxis = FlushAxis.Horizontal;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(300, layout.ClipBounds.Right, 3);
        Assert.Equal(layout.GridBounds.Right, layout.ClipBounds.Right, 3);
        Assert.Equal(400, layout.NominalClipBounds.Right, 3);
    }

    [Fact]
    public void Build_NoFlush_NominalClipEqualsClip()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.GridType = GridType.Square;
        s.FlushAxis = FlushAxis.None;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(layout.ClipBounds, layout.NominalClipBounds);
    }
}
