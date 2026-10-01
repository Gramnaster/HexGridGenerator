using System.Drawing;
using BenchmarkDotNet.Attributes;
using HexGrid.App.Rendering;
using HexGrid.Core;
using HexGrid.Core.Layout;
using HexGrid.Core.Scene;

namespace HexGrid.Benchmarks;

/// <summary>
/// Full-resolution PNG rasterisation (A3 at 300 dpi, about 17 megapixels), the export path. Each
/// operation takes tenths of a second, so the job is bounded to keep a full run practical: one
/// launch, 2 warmups and 8 measured iterations is plenty for operations this long.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 8)]
public class ExportRasterBenchmarks : IDisposable
{
    private readonly SceneRasterizer rasterizer = new();
    private GridSettings settings = new();
    private DrawScene? scene;

    [Params(Scenario.DefaultHex, Scenario.LargeHexLabelled)]
    public Scenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        settings = Scenarios.Create(Scenario);
        scene = SceneBuilder.Build(settings, GridLayoutEngine.Build(settings));
    }

    [Benchmark]
    public Size RasterFullResolution()
    {
        using Bitmap bitmap = rasterizer.Render(scene!, Color.Transparent, settings.Antialiasing);
        return bitmap.Size;
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
