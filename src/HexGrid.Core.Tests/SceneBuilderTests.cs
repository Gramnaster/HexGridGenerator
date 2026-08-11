using System.Drawing;
using HexGrid.Core.Layout;
using HexGrid.Core.Scene;

namespace HexGrid.Core.Tests;

public class SceneBuilderTests
{
    [Fact]
    public void Build_HexGridLayer_EmitsOpenTwoPointSegments()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        GridLayout layout = HexLayoutEngine.Build(s);

        // Act
        DrawScene scene = SceneBuilder.Build(s, layout);
        List<PathItem> edges = [.. scene.Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];

        // Assert
        Assert.NotEmpty(edges);
        Assert.All(edges, e => Assert.False(e.Closed));
        Assert.All(edges, e => Assert.Equal(2, e.Points.Length));
    }

    [Fact]
    public void Build_AdjacentHexes_EachSharedEdgeIsStrokedExactlyOnce()
    {
        // Arrange: TestSettings.Minimal() is a 5x4 grid, so interior hexes share edges with their
        // neighbours - the case SceneBuilder.AddHexes deduplicates for. Stroking every hex as a
        // whole polygon instead would double-stroke each shared edge, which is the regression this
        // guards against (see the "Every hex edge is stroked exactly once" note in README.md).
        GridSettings s = TestSettings.Minimal();
        GridLayout layout = HexLayoutEngine.Build(s);

        // Act
        DrawScene scene = SceneBuilder.Build(s, layout);
        List<PathItem> edges = [.. scene.Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];
        List<(long, long, long, long)> keys = [.. edges.Select(e => EdgeKey(e.Points[0], e.Points[1]))];

        // Assert: no two segments represent the same geometric edge, and dedup actually removed
        // shared edges rather than trivially having none to remove (a single hex has no neighbour
        // to share with, so cellCount > 1 with fewer than 6 edges per cell proves sharing occurred).
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.True(layout.Cells.Count > 1);
        Assert.True(edges.Count < layout.Cells.Count * 6);
    }

    /// <summary>
    /// Mirrors <c>SceneBuilder.EdgeKey</c>'s direction-independent, quantised identity. Two hexes
    /// sharing an edge compute its endpoints via independent trigonometry, so exact float equality
    /// would under-count duplicates that production code correctly treats as the same edge.
    /// </summary>
    private static (long, long, long, long) EdgeKey(PointF a, PointF b)
    {
        long ax = Q(a.X), ay = Q(a.Y), bx = Q(b.X), by = Q(b.Y);
        return ax < bx || (ax == bx && ay <= by) ? (ax, ay, bx, by) : (bx, by, ax, ay);
        static long Q(float v) => (long)Math.Round(v * 10.0, MidpointRounding.AwayFromZero);
    }
}
