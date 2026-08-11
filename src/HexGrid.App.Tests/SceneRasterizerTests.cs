using HexGrid.App.Rendering;
using HexGrid.Core;
using HexGrid.Core.Scene;

namespace HexGrid.App.Tests;

public class SceneRasterizerTests
{
    [Fact]
    public void Render_NullScene_Throws()
    {
        // Arrange
        using var rasterizer = new SceneRasterizer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => rasterizer.Render(null!, Color.White, antialias: true));
    }

    [Fact]
    public void Render_FractionalScale_CeilsPixelDimensions()
    {
        // Arrange: 21x11 at 0.5 scale is 10.5x5.5 - the bitmap must round up on both axes so no
        // content is clipped, not truncate down.
        using var rasterizer = new SceneRasterizer();
        DrawScene scene = NewScene(widthPx: 21, heightPx: 11);

        // Act
        using Bitmap bmp = rasterizer.Render(scene, Color.White, antialias: false, scale: 0.5);

        // Assert
        Assert.Equal(11, bmp.Width);
        Assert.Equal(6, bmp.Height);
    }

    [Fact]
    public void Render_NoContent_FillsEntireCanvasWithBackground()
    {
        // Arrange
        using var rasterizer = new SceneRasterizer();
        DrawScene scene = NewScene(widthPx: 10, heightPx: 10);
        Color background = Color.FromArgb(255, 10, 20, 30);

        // Act
        using Bitmap bmp = rasterizer.Render(scene, background, antialias: false);

        // Assert
        Assert.Equal(background, bmp.GetPixel(0, 0));
        Assert.Equal(background, bmp.GetPixel(5, 5));
        Assert.Equal(background, bmp.GetPixel(9, 9));
    }

    [Fact]
    public void Render_IncludeLayerFilter_DrawsOnlyTheIncludedLayer()
    {
        // Arrange: both layers fill the whole canvas with a different opaque colour, so whichever
        // one the filter admits is the one left standing.
        using var rasterizer = new SceneRasterizer();
        DrawScene scene = NewScene(widthPx: 10, heightPx: 10);
        scene.Layer(LayerKind.HexGrid).Items.Add(
            new RectItem(new RectangleF(0, 0, 10, 10), Stroke: null, StrokeWidthPx: 0, Fill: Color.Red));
        scene.Layer(LayerKind.Border).Items.Add(
            new RectItem(new RectangleF(0, 0, 10, 10), Stroke: null, StrokeWidthPx: 0, Fill: Color.Blue));

        // Act
        using Bitmap bmp = rasterizer.Render(
            scene, Color.White, antialias: false, includeLayer: kind => kind == LayerKind.HexGrid);

        // Assert
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(5, 5));
    }

    [Fact]
    public void Render_ClippedLayer_ContentOutsideClipBoundsIsNotDrawn()
    {
        // Arrange: HexGrid is a clipped layer kind (LayerRules.IsClipped); a fill spanning the whole
        // canvas must still stop at ClipBounds instead of bleeding into the margin around it.
        using var rasterizer = new SceneRasterizer();
        DrawScene scene = NewScene(widthPx: 10, heightPx: 10, clip: new RectangleF(2, 2, 6, 6));
        scene.Layer(LayerKind.HexGrid).Items.Add(
            new RectItem(new RectangleF(0, 0, 10, 10), Stroke: null, StrokeWidthPx: 0, Fill: Color.Red));

        // Act
        using Bitmap bmp = rasterizer.Render(scene, Color.White, antialias: false);

        // Assert
        Assert.Equal(Color.FromArgb(255, 255, 255, 255), bmp.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(5, 5));
    }

    [Fact]
    public void Render_UnclippedLayer_ContentOutsideClipBoundsIsStillDrawn()
    {
        // Arrange: Border is not in LayerRules.IsClipped's set, so its content must survive outside
        // the map area's ClipBounds (the frame is meant to sit around the grid, not inside it).
        using var rasterizer = new SceneRasterizer();
        DrawScene scene = NewScene(widthPx: 10, heightPx: 10, clip: new RectangleF(2, 2, 6, 6));
        scene.Layer(LayerKind.Border).Items.Add(
            new RectItem(new RectangleF(0, 0, 10, 10), Stroke: null, StrokeWidthPx: 0, Fill: Color.Red));

        // Act
        using Bitmap bmp = rasterizer.Render(scene, Color.White, antialias: false);

        // Assert
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), bmp.GetPixel(0, 0));
    }

    private static DrawScene NewScene(double widthPx, double heightPx, RectangleF? clip = null) => new()
    {
        WidthPx = widthPx,
        HeightPx = heightPx,
        WidthMm = widthPx,
        HeightMm = heightPx,
        Dpi = 96,
        ClipBounds = clip ?? new RectangleF(0, 0, (float)widthPx, (float)heightPx),
        GridType = GridType.Hex,
    };
}
