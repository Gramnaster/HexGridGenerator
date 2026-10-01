using System.ComponentModel;

namespace HexGrid.Core.Tests;

public class GridSettingsTests
{
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
