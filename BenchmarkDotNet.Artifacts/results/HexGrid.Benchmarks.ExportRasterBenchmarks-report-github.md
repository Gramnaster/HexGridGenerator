```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-KRRUPJ : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=8  LaunchCount=1  WarmupCount=2  

```
| Method               | Scenario         | Mean      | Error     | StdDev    | Allocated |
|--------------------- |----------------- |----------:|----------:|----------:|----------:|
| **RasterFullResolution** | **DefaultHex**       |  **73.74 ms** |  **2.212 ms** |  **0.982 ms** |     **464 B** |
| **RasterFullResolution** | **LargeHexLabelled** | **644.03 ms** | **45.554 ms** | **16.245 ms** |     **504 B** |
