using System.Drawing;
using HexGrid.Core.Rendering;
using HexGrid.Core.Scene;

namespace HexGrid.Core.Tests;

public class SvgRendererTests
{
    [Fact]
    public void Render_NullScene_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SvgRenderer.Render(null!));
    }

    [Fact]
    public void Render_RootElement_CarriesSizeFromScene()
    {
        // Arrange
        DrawScene scene = NewScene(widthPx: 800, heightPx: 600, widthMm: 210, heightMm: 297);

        // Act
        string svg = SvgRenderer.Render(scene);

        // Assert
        Assert.Contains("width=\"210mm\" height=\"297mm\"", svg, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"0 0 800 600\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_LayerWithNoItems_IsOmittedFromOutput()
    {
        // Arrange: Layer() reserves the layer even with zero items added to it.
        DrawScene scene = NewScene();
        scene.Layer(LayerKind.HexGrid);

        // Act
        string svg = SvgRenderer.Render(scene);

        // Assert
        Assert.DoesNotContain("id=\"HexGrid\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ClippedLayerKind_CarriesClipPathAttribute()
    {
        // Arrange: HexGrid is one of the layer kinds LayerRules.IsClipped marks as clipped to the map area.
        DrawScene scene = NewScene();
        scene.Layer(LayerKind.HexGrid).Items.Add(
            new PathItem([new PointF(0, 0), new PointF(1, 1)], Closed: false, Color.Black, 1.0, Fill: null));

        // Act
        string svg = SvgRenderer.Render(scene);

        // Assert
        Assert.Contains("id=\"HexGrid\" inkscape:groupmode=\"layer\" inkscape:label=\"Hex Grid\" clip-path=\"url(#mapArea)\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_UnclippedLayerKind_HasNoClipPathAttribute()
    {
        // Arrange: Border is not in LayerRules.IsClipped's set.
        DrawScene scene = NewScene();
        scene.Layer(LayerKind.Border).Items.Add(new RectItem(new RectangleF(0, 0, 10, 10), Color.Black, 1.0, Fill: null));

        // Act
        string svg = SvgRenderer.Render(scene);

        // Assert
        Assert.Contains("id=\"Border\" inkscape:groupmode=\"layer\" inkscape:label=\"Border\">", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_TextWithXmlSpecialCharacters_EscapesThem()
    {
        // Arrange
        DrawScene scene = NewScene();
        scene.Layer(LayerKind.HexLabels).Items.Add(new TextItem(
            "A & B < C > \"D\"", new PointF(0, 0), TextAnchor.Middle, TextBaseline.Middle,
            "Segoe UI", 12.0, Bold: false, Color.Black));

        // Act
        string svg = SvgRenderer.Render(scene);

        // Assert
        Assert.Contains(">A &amp; B &lt; C &gt; &quot;D&quot;</text>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_TwoPathItemsWithIdenticalStyle_MergeIntoOnePathElement()
    {
        // Arrange: adjacent hexes sharing an edge style must not double the element count in the export.
        DrawScene scene = NewScene();
        SceneLayer layer = scene.Layer(LayerKind.HexGrid);
        layer.Items.Add(new PathItem([new PointF(0, 0), new PointF(1, 1)], Closed: false, Color.Black, 1.0, Fill: null));
        layer.Items.Add(new PathItem([new PointF(2, 2), new PointF(3, 3)], Closed: false, Color.Black, 1.0, Fill: null));

        // Act
        string svg = SvgRenderer.Render(scene);

        // Assert
        Assert.Equal(1, CountOccurrences(svg, "<path"));
    }

    [Fact]
    public void Render_TwoPathItemsWithDifferentStroke_ProduceSeparatePathElements()
    {
        // Arrange
        DrawScene scene = NewScene();
        SceneLayer layer = scene.Layer(LayerKind.HexGrid);
        layer.Items.Add(new PathItem([new PointF(0, 0), new PointF(1, 1)], Closed: false, Color.Black, 1.0, Fill: null));
        layer.Items.Add(new PathItem([new PointF(2, 2), new PointF(3, 3)], Closed: false, Color.Red, 1.0, Fill: null));

        // Act
        string svg = SvgRenderer.Render(scene);

        // Assert
        Assert.Equal(2, CountOccurrences(svg, "<path"));
    }

    [Fact]
    public void Render_BackgroundWithZeroAlpha_OmitsBackgroundLayer()
    {
        // Arrange
        DrawScene scene = NewScene();

        // Act
        string svg = SvgRenderer.Render(scene, Color.FromArgb(0, 255, 255, 255));

        // Assert
        Assert.DoesNotContain("id=\"Background\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_BackgroundWithOpaqueColor_EmitsFilledRect()
    {
        // Arrange
        DrawScene scene = NewScene();

        // Act
        string svg = SvgRenderer.Render(scene, Color.FromArgb(255, 255, 0, 0));

        // Assert
        Assert.Contains("id=\"Background\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#ff0000\"", svg, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static DrawScene NewScene(
        double widthPx = 100, double heightPx = 100, double widthMm = 100, double heightMm = 100) => new()
    {
        WidthPx = widthPx,
        HeightPx = heightPx,
        WidthMm = widthMm,
        HeightMm = heightMm,
        Dpi = 300,
        ClipBounds = new RectangleF(0, 0, (float)widthPx, (float)heightPx),
        GridType = GridType.Hex,
    };
}
