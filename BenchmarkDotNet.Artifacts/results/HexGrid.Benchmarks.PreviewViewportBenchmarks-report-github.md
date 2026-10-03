```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-KRRUPJ : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=8  LaunchCount=1  WarmupCount=2  

```
| Method                     | ZoomOfFit | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------------- |---------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **WholeCanvas**                | **1**         | **134.10 ms** |  **8.153 ms** |  **3.620 ms** |  **1.00** |    **0.04** |     **448 B** |        **1.00** |
| VisibleRegion              | 1         | 158.99 ms | 54.239 ms | 28.368 ms |  1.19 |    0.20 |     448 B |        1.00 |
| VisibleRegionNative        | 1         |  18.16 ms |  5.393 ms |  2.821 ms |  0.14 |    0.02 |     504 B |        1.12 |
| VisibleRegionUnculledSpike | 1         | 189.20 ms | 36.107 ms | 18.885 ms |  1.41 |    0.14 |    2432 B |        5.43 |
|                            |           |           |           |           |       |         |           |             |
| **WholeCanvas**                | **4**         | **293.02 ms** | **99.376 ms** | **51.975 ms** |  **1.02** |    **0.23** |     **472 B** |        **1.00** |
| VisibleRegion              | 4         |  47.72 ms |  2.699 ms |  1.198 ms |  0.17 |    0.03 |     448 B |        0.95 |
| VisibleRegionNative        | 4         |  10.05 ms |  0.516 ms |  0.270 ms |  0.04 |    0.01 |     504 B |        1.07 |
| VisibleRegionUnculledSpike | 4         |  97.26 ms |  6.382 ms |  3.338 ms |  0.34 |    0.05 |    1584 B |        3.36 |
