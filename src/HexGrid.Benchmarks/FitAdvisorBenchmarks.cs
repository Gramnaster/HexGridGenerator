using BenchmarkDotNet.Attributes;
using HexGrid.Core;
using HexGrid.Core.Layout;

namespace HexGrid.Benchmarks;

/// <summary>
/// <see cref="SquareFitAdvisor.RecommendFit"/> runs on every rebuild of an auto-fit square grid (it
/// feeds the status bar) and solves up to ~160 candidate layouts, so it is a hot path in its own right.
/// </summary>
[MemoryDiagnoser]
public class FitAdvisorBenchmarks
{
    private GridSettings settings = new();
    private GridLayout? layout;

    [Params(GridSizingMode.AutoFitRowsColumns, GridSizingMode.FixedHexWidth)]
    public GridSizingMode Mode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        settings = Scenarios.Create(Scenario.SquareAutoFitGapped);
        settings.SizingMode = Mode;
        settings.SquareSize = 9.0;
        layout = GridLayoutEngine.Build(settings);
    }

    [Benchmark]
    public SquareFitSuggestion RecommendFit() => SquareFitAdvisor.RecommendFit(settings, layout!);
}
