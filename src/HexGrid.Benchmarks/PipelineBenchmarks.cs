using System.Drawing;
using BenchmarkDotNet.Attributes;
using HexGrid.App.Rendering;
using HexGrid.Core;
using HexGrid.Core.Layout;
using HexGrid.Core.Rendering;
using HexGrid.Core.Scene;

namespace HexGrid.Benchmarks;

/// <summary>
/// Every stage of a live-preview rebuild (layout, scene, preview raster) plus SVG export, each
/// measured on its own so a regression points at the stage that caused it.
/// </summary>
[MemoryDiagnoser]
public class PipelineBenchmarks : IDisposable
{
    private readonly SceneRasterizer rasterizer = new();
    private GridSettings settings = new();
    private GridLayout? layout;
    private DrawScene? scene;

    [Params(Scenario.DefaultHex, Scenario.LargeHexLabelled, Scenario.SquareAutoFitGapped)]
    public Scenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        settings = Scenarios.Create(Scenario);
        layout = GridLayoutEngine.Build(settings);
        scene = SceneBuilder.Build(settings, layout);
    }

    [Benchmark]
    public GridLayout Layout() => GridLayoutEngine.Build(settings);

    [Benchmark]
    public DrawScene Scene() => SceneBuilder.Build(settings, layout!);

    [Benchmark]
    public string Svg() => SvgRenderer.Render(scene!);

    [Benchmark]
    public Size RasterPreview()
    {
        using Bitmap bitmap = rasterizer.Render(scene!, Color.Transparent, settings.Antialiasing, Scenarios.PreviewScale, minStrokePx: 1.0);
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
