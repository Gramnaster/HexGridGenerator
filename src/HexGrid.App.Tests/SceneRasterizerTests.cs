using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HexGrid.App.Rendering;
using HexGrid.Core.Settings;
using HexGrid.Core.Layout;
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

    [Theory]
    [InlineData(0.25)]
    [InlineData(1.0)]
    public void RenderRegion_AnyRectangle_MatchesThatRectangleOfTheWholeCanvasRender(double scale)
    {
        // Arrange: a label in every hex, so the region's edges cut through lines, dots and text,
        // and items that sit just outside it still reach in.
        using var rasterizer = new SceneRasterizer();
        var settings = new GridSettings { Preset = CanvasPreset.A6, ShowHexLabels = true };
        DrawScene scene = SceneBuilder.Build(settings, GridLayoutEngine.Build(settings));
        using Bitmap whole = rasterizer.Render(scene, Color.Transparent, antialias: true, scale, minStrokePx: 1.0);
        var region = new Rectangle(whole.Width / 3, whole.Height / 4, whole.Width / 3, whole.Height / 3);

        // Act
        using Bitmap part = rasterizer.RenderRegion(
            scene, Color.Transparent, antialias: true, scale, region, minStrokePx: 1.0, CancellationToken.None);

        // Assert: GDI+ antialiasing starts its coverage sums at the bitmap's left edge, which leaves a
        // few edge pixels one level off in one channel. Anything more would be a real difference.
        Assert.Equal(region.Size, part.Size);
        Assert.Equal(0, CountPixelsDifferingByMoreThanOneLevel(whole, region.Location, part));
    }

    [Fact]
    public void RenderRegion_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange: more items than one cancellation check interval, so the check is reached.
        using var rasterizer = new SceneRasterizer();
        DrawScene scene = NewScene(widthPx: 10, heightPx: 10);
        for (int i = 0; i < 1000; i++)
        {
            scene.Layer(LayerKind.HexGrid).Items.Add(
                new RectItem(new RectangleF(0, 0, 10, 10), Stroke: null, StrokeWidthPx: 0, Fill: Color.Red));
        }

        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() => rasterizer.RenderRegion(
            scene, Color.White, antialias: false, scale: 1.0, new Rectangle(0, 0, 10, 10), minStrokePx: 0, cancel.Token));
    }

    private static int CountPixelsDifferingByMoreThanOneLevel(Bitmap whole, Point offset, Bitmap part)
    {
        int[] wholePixels = ReadPixels(whole);
        int[] partPixels = ReadPixels(part);
        int differing = 0;
        for (int y = 0; y < part.Height; y++)
        {
            for (int x = 0; x < part.Width; x++)
            {
                int a = partPixels[(y * part.Width) + x];
                int b = wholePixels[((y + offset.Y) * whole.Width) + x + offset.X];
                for (int shift = 0; shift < 32; shift += 8)
                {
                    if (Math.Abs(((a >> shift) & 0xFF) - ((b >> shift) & 0xFF)) > 1)
                    {
                        differing++;
                        break;
                    }
                }
            }
        }

        return differing;
    }

    private static int[] ReadPixels(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(
            new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            // Format32bppArgb rows are whole ints, so the stride is exactly the width.
            var pixels = new int[bitmap.Width * bitmap.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
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
