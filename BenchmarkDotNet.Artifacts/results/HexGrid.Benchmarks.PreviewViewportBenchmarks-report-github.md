```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-KRRUPJ : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=8  LaunchCount=1  WarmupCount=2  

```
| Method                  | ZoomOfFit | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------ |---------- |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **WholeCanvas**             | **1**         | **149.992 ms** |  **7.4173 ms** | **3.8794 ms** |  **1.00** |    **0.03** |     **448 B** |        **1.00** |
| VisibleRegion           | 1         | 141.604 ms | 11.1890 ms | 5.8520 ms |  0.94 |    0.04 |     448 B |        1.00 |
| VisibleRegionNative     | 1         |   9.486 ms |  0.3679 ms | 0.1924 ms |  0.06 |    0.00 |     504 B |        1.12 |
| VisibleRegionSmoothZoom | 1         |   5.177 ms |  0.1176 ms | 0.0615 ms |  0.03 |    0.00 |     504 B |        1.12 |
|                         |           |            |            |           |       |         |           |             |
| **WholeCanvas**             | **4**         | **221.981 ms** |  **4.3573 ms** | **2.2789 ms** |  **1.00** |    **0.01** |     **448 B** |        **1.00** |
| VisibleRegion           | 4         |  47.362 ms |  1.9351 ms | 1.0121 ms |  0.21 |    0.00 |     448 B |        1.00 |
| VisibleRegionNative     | 4         |   9.702 ms |  0.3120 ms | 0.1632 ms |  0.04 |    0.00 |     504 B |        1.12 |
| VisibleRegionSmoothZoom | 4         |   4.542 ms |  0.2320 ms | 0.1214 ms |  0.02 |    0.00 |     504 B |        1.12 |
