using System.Drawing;
using BenchmarkDotNet.Attributes;
using HexGrid.App.Rendering;
using HexGrid.Core;
using HexGrid.Core.Layout;
using HexGrid.Core.Scene;

namespace HexGrid.Benchmarks;

/// <summary>
/// The full-resolution PNG export path, from A3 at 300 dpi (about 17 megapixels) up to A0 (139
/// megapixels): the raster on its own, and the whole export including PNG encoding and the file
/// write. Each operation takes tenths of a second or more, so the job is bounded to keep a full run
/// practical: one launch, 2 warmups and 8 measured iterations is plenty for operations this long.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 8)]
public class ExportRasterBenchmarks : IDisposable
{
    private readonly SceneRasterizer rasterizer = new();
    private GridSettings settings = new();
    private DrawScene? scene;
    private string path = string.Empty;

    [Params(Scenario.DefaultHex, Scenario.LargeHexLabelled, Scenario.A0HexLabelled)]
    public Scenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        settings = Scenarios.Create(Scenario);
        scene = SceneBuilder.Build(settings, GridLayoutEngine.Build(settings));
        path = Path.Combine(Path.GetTempPath(), $"hexgrid-benchmark-{Guid.NewGuid():N}.png");
    }

    [Benchmark]
    public Size RasterFullResolution()
    {
        using Bitmap bitmap = rasterizer.Render(scene!, Color.Transparent, settings.Antialiasing);
        return bitmap.Size;
    }

    [Benchmark]
    public int SavePng() => ExportService.SavePng(rasterizer, scene!, settings, path).Count;

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
            File.Delete(path);
            rasterizer.Dispose();
        }
    }
}
