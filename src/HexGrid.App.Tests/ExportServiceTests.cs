using HexGrid.App.Rendering;
using HexGrid.Core;
using HexGrid.Core.Scene;

namespace HexGrid.App.Tests;

public class ExportServiceTests
{
    [Theory]
    [InlineData(PngBackground.Transparent, 0, 255, 255, 255)] // Color.Transparent is #00FFFFFF, not #00000000.
    [InlineData(PngBackground.White, 255, 255, 255, 255)]
    [InlineData(PngBackground.Black, 255, 0, 0, 0)]
    public void BackgroundFor_StandardOptions_ReturnsExpectedColor(PngBackground option, int a, int r, int g, int b)
    {
        // Arrange
        var settings = new GridSettings { PngBackground = option };

        // Act
        Color color = ExportService.BackgroundFor(settings);

        // Assert: ToArgb(), not Color equality directly - Color.Equals also compares the "known
        // color" bookkeeping (e.g. Color.Transparent vs. an unnamed Color.FromArgb(0,0,0,0) with the
        // same bits are unequal), which isn't a property this pipeline's output actually depends on.
        Assert.Equal(Color.FromArgb(a, r, g, b).ToArgb(), color.ToArgb());
    }

    [Fact]
    public void BackgroundFor_Custom_ReturnsPngCustomBackground()
    {
        // Arrange
        Color custom = Color.FromArgb(200, 10, 20, 30);
        var settings = new GridSettings { PngBackground = PngBackground.Custom, PngCustomBackground = custom };

        // Act
        Color color = ExportService.BackgroundFor(settings);

        // Assert
        Assert.Equal(custom, color);
    }

    [Fact]
    public void SaveSvg_WritesFileWithSvgContent()
    {
        // Arrange
        DrawScene scene = NewScene();
        var settings = new GridSettings();
        string path = Path.Combine(Path.GetTempPath(), $"hexgrid-test-{Guid.NewGuid():N}.svg");

        try
        {
            // Act
            ExportService.SaveSvg(scene, settings, path);

            // Assert
            Assert.True(File.Exists(path));
            Assert.Contains("<svg ", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SavePng_LayersNotSeparate_WritesOnlyFlattenedFile()
    {
        // Arrange
        DrawScene scene = NewScene();
        scene.Layer(LayerKind.HexGrid).Items.Add(
            new RectItem(new RectangleF(0, 0, 10, 10), Color.Black, 1.0, Fill: null));
        var settings = new GridSettings { ExportLayersSeparately = false };
        string path = Path.Combine(Path.GetTempPath(), $"hexgrid-test-{Guid.NewGuid():N}.png");

        using var rasterizer = new SceneRasterizer();
        try
        {
            // Act
            IReadOnlyList<string> written = ExportService.SavePng(rasterizer, scene, settings, path);

            // Assert
            Assert.Equal([path], written);
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SavePng_LayersSeparate_WritesOneFilePerNonEmptyLayerAndSkipsReservedEmptyOnes()
    {
        // Arrange: CenterDots is reserved via Layer() but never given an item, matching how the real
        // pipeline reserves layers up front. An empty layer must not produce a stray PNG file.
        DrawScene scene = NewScene();
        scene.Layer(LayerKind.HexGrid).Items.Add(
            new RectItem(new RectangleF(0, 0, 10, 10), Color.Black, 1.0, Fill: null));
        scene.Layer(LayerKind.Border).Items.Add(
            new RectItem(new RectangleF(0, 0, 10, 10), Color.Black, 1.0, Fill: null));
        scene.Layer(LayerKind.CenterDots);
        var settings = new GridSettings { ExportLayersSeparately = true };
        string dir = Path.Combine(Path.GetTempPath(), $"hexgrid-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "scene.png");

        using var rasterizer = new SceneRasterizer();
        try
        {
            // Act
            IReadOnlyList<string> written = ExportService.SavePng(rasterizer, scene, settings, path);

            // Assert
            Assert.Equal(3, written.Count);
            Assert.Contains(path, written);
            Assert.Contains(Path.Combine(dir, "scene_HexGrid.png"), written);
            Assert.Contains(Path.Combine(dir, "scene_Border.png"), written);
            Assert.All(written, f => Assert.True(File.Exists(f)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static DrawScene NewScene() => new()
    {
        WidthPx = 50,
        HeightPx = 40,
        WidthMm = 50,
        HeightMm = 40,
        Dpi = 96,
        ClipBounds = new RectangleF(0, 0, 50, 40),
    };
}
