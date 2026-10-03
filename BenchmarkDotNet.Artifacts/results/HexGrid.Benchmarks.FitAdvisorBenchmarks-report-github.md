```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method       | Mode               | Mean     | Error    | StdDev   | Median   | Gen0   | Allocated |
|------------- |------------------- |---------:|---------:|---------:|---------:|-------:|----------:|
| **RecommendFit** | **AutoFitRowsColumns** | **30.26 μs** | **0.755 μs** | **2.226 μs** | **29.64 μs** | **5.0049** |  **76.91 KB** |
| **RecommendFit** | **FixedHexWidth**      | **27.82 μs** | **0.549 μs** | **1.017 μs** | **27.55 μs** | **5.0049** |  **76.91 KB** |
