```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-FGEKWY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=3  LaunchCount=1  WarmupCount=1  

```
| Method    | Canvas | Mean       | Error      | StdDev    | Gen0      | Gen1      | Gen2      | Allocated  |
|---------- |------- |-----------:|-----------:|----------:|----------:|----------:|----------:|-----------:|
| **Raster**    | **A0**     | **1,132.3 ms** | **1,050.9 ms** |  **57.60 ms** |         **-** |         **-** |         **-** |      **504 B** |
| EncodePng | A0     | 1,573.8 ms | 1,848.6 ms | 101.33 ms | 2000.0000 | 2000.0000 | 1000.0000 | 33490264 B |
| **Raster**    | **TwoA0**  | **3,249.3 ms** | **4,539.6 ms** | **248.83 ms** |         **-** |         **-** |         **-** |      **504 B** |
| EncodePng | TwoA0  | 3,089.0 ms | 2,479.5 ms | 135.91 ms | 2000.0000 | 2000.0000 |         - | 67044072 B |
| **Raster**    | **Uhd16K** |   **883.1 ms** |   **881.1 ms** |  **48.30 ms** |         **-** |         **-** |         **-** |      **504 B** |
| EncodePng | Uhd16K | 1,465.0 ms |   865.4 ms |  47.44 ms | 1000.0000 | 1000.0000 | 1000.0000 | 16713024 B |
