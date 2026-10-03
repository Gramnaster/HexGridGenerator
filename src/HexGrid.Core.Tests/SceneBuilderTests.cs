using System.Drawing;
using HexGrid.Core.Layout;
using HexGrid.Core.Scene;
using HexGrid.Core.Settings;

namespace HexGrid.Core.Tests;

public class SceneBuilderTests
{
    [Fact]
    public void Build_HexGridLayer_EmitsOpenTwoPointSegments()
    {
        // Arrange
        GridSettings s = TestSettings.Minimal();
        GridLayout layout = GridLayoutEngine.Build(s);

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
        GridLayout layout = GridLayoutEngine.Build(s);

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

    [Fact]
    public void Build_CrosshairLineStyle_EmitsTwoArmsStartingAtEachSolidEdgesEndpoints()
    {
        // Arrange: same layout, one scene per LineStyle, so edge N in the Solid scene lines up
        // positionally with arm pair (2N, 2N+1) in the Crosshair scene - both walk the same
        // cells/vertices in the same order and only differ in what they append per unique edge.
        GridSettings solid = TestSettings.Minimal();
        GridLayout layout = GridLayoutEngine.Build(solid);
        List<PathItem> solidEdges = [.. SceneBuilder.Build(solid, layout).Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];

        GridSettings crosshair = TestSettings.Minimal();
        crosshair.LineStyle = LineStyle.Crosshair;
        List<PathItem> arms = [.. SceneBuilder.Build(crosshair, layout).Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];

        // Assert
        Assert.Equal(solidEdges.Count * 2, arms.Count);
        for (int i = 0; i < solidEdges.Count; i++)
        {
            Assert.Equal(solidEdges[i].Points[0], arms[(2 * i) + 0].Points[0]);
            Assert.Equal(solidEdges[i].Points[1], arms[(2 * i) + 1].Points[0]);
        }
    }

    [Fact]
    public void Build_CrosshairLineStyle_ArmLengthMatchesSetting_WhenShorterThanHalfTheEdge()
    {
        // Arrange
        const double armLength = 1.0;
        GridSettings solid = TestSettings.Minimal();
        GridLayout layout = GridLayoutEngine.Build(solid);
        List<PathItem> solidEdges = [.. SceneBuilder.Build(solid, layout).Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];
        Assert.All(solidEdges, e => Assert.True(Length(e.Points[0], e.Points[1]) > armLength * 2)); // precondition: no clamping in play

        GridSettings crosshair = TestSettings.Minimal();
        crosshair.LineStyle = LineStyle.Crosshair;
        crosshair.CrosshairArmLength = armLength;
        List<PathItem> arms = [.. SceneBuilder.Build(crosshair, layout).Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];

        // Act & Assert
        Assert.All(arms, a => Assert.Equal(armLength, Length(a.Points[0], a.Points[1]), 3));
    }

    [Fact]
    public void Build_CrosshairLineStyle_ArmLengthClampedToHalfTheEdge_WhenSettingExceedsIt()
    {
        // Arrange
        GridSettings solid = TestSettings.Minimal();
        GridLayout layout = GridLayoutEngine.Build(solid);
        List<PathItem> solidEdges = [.. SceneBuilder.Build(solid, layout).Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];

        GridSettings crosshair = TestSettings.Minimal();
        crosshair.LineStyle = LineStyle.Crosshair;
        crosshair.CrosshairArmLength = 10_000; // deliberately far longer than any edge on this canvas
        List<PathItem> arms = [.. SceneBuilder.Build(crosshair, layout).Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];

        // Act & Assert: each pair of arms stops at its edge's midpoint rather than overlapping past it.
        for (int i = 0; i < solidEdges.Count; i++)
        {
            double halfEdge = Length(solidEdges[i].Points[0], solidEdges[i].Points[1]) / 2.0;
            Assert.Equal(halfEdge, Length(arms[2 * i].Points[0], arms[2 * i].Points[1]), 3);
            Assert.Equal(halfEdge, Length(arms[(2 * i) + 1].Points[0], arms[(2 * i) + 1].Points[1]), 3);
        }
    }

    [Fact]
    public void Build_SquareCrosshairLineStyle_EmitsTwoOpenSegmentsPerSolidEdge()
    {
        // Arrange: the Crosshair branch is shared code between grid types - this exercises the
        // Square side of it, since every other test in this file uses the Hex default.
        GridSettings solid = TestSettings.Minimal();
        solid.GridType = GridType.Square;
        GridLayout layout = GridLayoutEngine.Build(solid);
        int solidEdgeCount = SceneBuilder.Build(solid, layout).Layer(LayerKind.HexGrid).Items.Count;

        GridSettings crosshair = TestSettings.Minimal();
        crosshair.GridType = GridType.Square;
        crosshair.LineStyle = LineStyle.Crosshair;
        List<PathItem> arms = [.. SceneBuilder.Build(crosshair, layout).Layer(LayerKind.HexGrid).Items.Cast<PathItem>()];

        // Assert
        Assert.Equal(solidEdgeCount * 2, arms.Count);
        Assert.All(arms, a => Assert.False(a.Closed));
        Assert.All(arms, a => Assert.Equal(2, a.Points.Length));
    }

    private static double Length(PointF a, PointF b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

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
