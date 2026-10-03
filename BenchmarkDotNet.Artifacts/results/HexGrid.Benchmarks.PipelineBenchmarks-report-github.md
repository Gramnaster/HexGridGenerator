```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method        | Scenario            | Mean       | Error     | StdDev    | Allocated |
|-------------- |-------------------- |-----------:|----------:|----------:|----------:|
| **RasterPreview** | **DefaultHex**          |   **8.454 ms** | **0.3201 ms** | **0.9082 ms** |     **464 B** |
| **RasterPreview** | **LargeHexLabelled**    | **101.528 ms** | **1.9671 ms** | **5.1821 ms** |     **504 B** |
| **RasterPreview** | **SquareAutoFitGapped** |  **16.208 ms** | **0.2883 ms** | **0.6082 ms** |     **464 B** |
