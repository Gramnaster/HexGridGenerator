using HexGrid.Core.Layout;
using HexGrid.Core.Rendering;
using HexGrid.Core.Scene;

namespace HexGrid.Core.Tests;

/// <summary>
/// End-to-end regression coverage for the real generator pipeline (Settings -&gt; Layout -&gt; Scene
/// -&gt; SVG), run with the actual out-of-the-box <see cref="GridSettings"/> defaults rather than a
/// pared-down fixture. A default-constructed <see cref="GridSettings"/> is what a user gets on first
/// launch, so these tests catch a broken default or a visibility flag that stops reaching the export.
/// </summary>
public class GenerationPipelineTests
{
    [Fact]
    public void Generate_DefaultSettings_ProducesWellFormedSvgWithMatchingCellCount()
    {
        // Arrange
        var s = new GridSettings();

        // Act
        string svg = Render(s, out GridLayout layout);

        // Assert
        Assert.StartsWith("<?xml", svg, StringComparison.Ordinal);
        Assert.Contains("<svg ", svg, StringComparison.Ordinal);
        Assert.Contains("</svg>", svg, StringComparison.Ordinal);
        Assert.True(layout.Cells.Count > 0);
        Assert.Equal(layout.Columns * layout.Rows, layout.Cells.Count);
    }

    [Fact]
    public void Generate_LineOpacity_ControlsHexGridLayerPresence()
    {
        // Arrange
        var visible = new GridSettings();
        var invisible = new GridSettings { LineOpacity = 0 };

        // Act & Assert
        Assert.Contains("id=\"HexGrid\"", Render(visible, out _), StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"HexGrid\"", Render(invisible, out _), StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ShowCenterDots_ControlsCenterDotsLayerPresence()
    {
        // Arrange
        var shown = new GridSettings { ShowCenterDots = true };
        var hidden = new GridSettings { ShowCenterDots = false };

        // Act & Assert
        Assert.Contains("id=\"CenterDots\"", Render(shown, out _), StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"CenterDots\"", Render(hidden, out _), StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ShowHexLabels_ControlsHexLabelsLayerPresence()
    {
        // Arrange
        var hidden = new GridSettings { ShowHexLabels = false };
        var shown = new GridSettings { ShowHexLabels = true };

        // Act & Assert
        Assert.DoesNotContain("id=\"HexLabels\"", Render(hidden, out _), StringComparison.Ordinal);
        Assert.Contains("id=\"HexLabels\"", Render(shown, out _), StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_MarginalLabelSides_ControlsEdgeLabelsLayerPresence()
    {
        // Arrange
        var shown = new GridSettings { MarginalLabelSides = LabelSides.All };
        var hidden = new GridSettings { MarginalLabelSides = LabelSides.None };

        // Act & Assert
        Assert.Contains("id=\"EdgeLabels\"", Render(shown, out _), StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"EdgeLabels\"", Render(hidden, out _), StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_BorderStyle_ControlsBorderLayerPresence()
    {
        // Arrange
        var shown = new GridSettings { BorderStyle = MapBorderStyle.Line };
        var hidden = new GridSettings { BorderStyle = MapBorderStyle.None };

        // Act & Assert
        Assert.Contains("id=\"Border\"", Render(shown, out _), StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"Border\"", Render(hidden, out _), StringComparison.Ordinal);
    }

    private static string Render(GridSettings s, out GridLayout layout)
    {
        layout = HexLayoutEngine.Build(s);
        DrawScene scene = SceneBuilder.Build(s, layout);
        return SvgRenderer.Render(scene);
    }
}
