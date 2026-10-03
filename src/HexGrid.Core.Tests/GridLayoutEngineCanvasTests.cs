using HexGrid.Core.Layout;
using HexGrid.Core.Settings;

namespace HexGrid.Core.Tests;

public class GridLayoutEngineCanvasTests
{
    [Fact]
    public void Build_ScreenPreset_Landscape_KeepsNativeWidthByHeight()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        s.Preset = CanvasPreset.Uhd4K;
        s.PageOrientation = PageOrientation.Landscape;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(3840, layout.CanvasWidthPx, 3);
        Assert.Equal(2160, layout.CanvasHeightPx, 3);
    }

    [Fact]
    public void Build_ScreenPreset_Portrait_SwapsWidthAndHeight()
    {
        // Arrange: a bare orientation flip must actually reshape a screen preset's canvas, not just
        // paper presets - a portrait 4K display or wallpaper is a real target for this generator.
        GridSettings s = TestSettings.Minimal();
        s.Preset = CanvasPreset.Uhd4K;
        s.PageOrientation = PageOrientation.Portrait;

        // Act
        GridLayout layout = GridLayoutEngine.Build(s);

        // Assert
        Assert.Equal(2160, layout.CanvasWidthPx, 3);
        Assert.Equal(3840, layout.CanvasHeightPx, 3);
    }
}
