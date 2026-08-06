namespace HexGrid.Core.Tests;

public class GridSettingsTests
{
    [Fact]
    public void Clone_ReturnsDistinctInstanceWithEqualValues()
    {
        // Arrange
        var original = new GridSettings { Columns = 99 };

        // Act
        GridSettings clone = original.Clone();

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(original.Columns, clone.Columns);
    }

    [Fact]
    public void Clone_MutatingClone_DoesNotAffectOriginal()
    {
        // Arrange
        var original = new GridSettings { Columns = 10 };
        GridSettings clone = original.Clone();

        // Act
        clone.Columns = 20;

        // Assert
        Assert.Equal(10, original.Columns);
        Assert.Equal(20, clone.Columns);
    }
}
