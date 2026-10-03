using BenchmarkDotNet.Running;

namespace HexGrid.Benchmarks;

internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
