using System.ComponentModel;

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

    [Fact]
    public void GetProperties_HexGridType_HidesGapYAndRelabelsGapXAsGap()
    {
        // Arrange: a regular hexagon's six neighbours are all equidistant, so there is no separate
        // horizontal/vertical gap to expose - only the shared Gap value (CellGapX) applies.
        var settings = new GridSettings { GridType = GridType.Hex };

        // Act
        PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(settings);

        // Assert
        Assert.Null(properties[nameof(GridSettings.CellGapY)]);
        Assert.Equal("Gap", properties[nameof(GridSettings.CellGapX)]!.DisplayName);
    }

    [Fact]
    public void GetProperties_SquareGridType_ShowsIndependentGapXAndGapY()
    {
        // Arrange
        var settings = new GridSettings { GridType = GridType.Square };

        // Act
        PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(settings);

        // Assert
        Assert.Equal("Gap X", properties[nameof(GridSettings.CellGapX)]!.DisplayName);
        Assert.Equal("Gap Y", properties[nameof(GridSettings.CellGapY)]!.DisplayName);
    }
}
