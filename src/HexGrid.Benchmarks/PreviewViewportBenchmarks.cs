using System.Drawing;
using BenchmarkDotNet.Attributes;
using HexGrid.App.Rendering;
using HexGrid.Core;
using HexGrid.Core.Layout;
using HexGrid.Core.Scene;

namespace HexGrid.Benchmarks;

/// <summary>
/// The live preview of the largest paper preset, at the panel's fit scale and zoomed in to four
/// times fit. Rasterising the whole canvas costs more the further in the user zooms, which is what
/// once capped zoom at 400% of fit. The preview now renders only the region in view, whose cost is
/// bounded by the panel's size instead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 8)]
public class PreviewViewportBenchmarks : IDisposable
{
    // A 2A0 page (14043 × 19866 px) fitted into a typical preview panel of about 900 × 720 px.
    private const double FitScale = 0.036;

    // The visible panel plus the margin the preview renders around it, so a short pan needs no new frame.
    private static readonly Size Viewport = new(900 + 256, 720 + 256);

    private readonly SceneRasterizer rasterizer = new();
    private GridSettings settings = new();
    private DrawScene? scene;

    [Params(1, 4)]
    public int ZoomOfFit { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        settings = Scenarios.Create(Scenario.TwoA0HexLabelled);
        scene = SceneBuilder.Build(settings, GridLayoutEngine.Build(settings));
    }

    [Benchmark(Baseline = true)]
    public Size WholeCanvas()
    {
        using Bitmap bitmap = rasterizer.Render(scene!, Color.Transparent, settings.Antialiasing, FitScale * ZoomOfFit, minStrokePx: 1.0);
        return bitmap.Size;
    }

    /// <summary>What the preview renders now: only the pixels in view, centred on the canvas.</summary>
    [Benchmark]
    public Size VisibleRegion()
    {
        using Bitmap bitmap = rasterizer.RenderRegion(
            scene!, Color.Transparent, settings.Antialiasing, FitScale * ZoomOfFit, CentredRegion(FitScale * ZoomOfFit), minStrokePx: 1.0, CancellationToken.None);
        return bitmap.Size;
    }

    /// <summary>Zoomed past the canvas's own resolution, the preview renders in view at scale 1 and magnifies.</summary>
    [Benchmark]
    public Size VisibleRegionNative()
    {
        using Bitmap bitmap = rasterizer.RenderRegion(
            scene!, Color.Transparent, settings.Antialiasing, 1.0, CentredRegion(1.0), minStrokePx: 0, CancellationToken.None);
        return bitmap.Size;
    }

    private Rectangle CentredRegion(double scale)
    {
        var canvas = new Rectangle(0, 0, (int)Math.Ceiling(scene!.WidthPx * scale), (int)Math.Ceiling(scene.HeightPx * scale));
        var centred = new Rectangle(
            (canvas.Width - Viewport.Width) / 2, (canvas.Height - Viewport.Height) / 2, Viewport.Width, Viewport.Height);
        return Rectangle.Intersect(canvas, centred);
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    // BenchmarkDotNet generates a class deriving from this one, so it can't be sealed, and the
    // unsealed dispose pattern applies.
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            rasterizer.Dispose();
        }
    }
}
