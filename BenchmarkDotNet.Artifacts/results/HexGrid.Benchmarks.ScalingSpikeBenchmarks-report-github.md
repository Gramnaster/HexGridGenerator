```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method             | Threads | Layers   | Mean        | Error       | StdDev    | Gen0      | Allocated   |
|------------------- |-------- |--------- |------------:|------------:|----------:|----------:|------------:|
| **ConcurrentPreviews** | **1**       | **all**      |   **123.30 ms** |   **286.12 ms** | **15.683 ms** |         **-** |  **2935.65 KB** |
| **ConcurrentPreviews** | **1**       | **geometry** |    **70.00 ms** |    **44.70 ms** |  **2.450 ms** |  **125.0000** |  **2334.88 KB** |
| **ConcurrentPreviews** | **1**       | **text**     |    **50.32 ms** |    **21.16 ms** |  **1.160 ms** |         **-** |   **602.35 KB** |
| **ConcurrentPreviews** | **4**       | **all**      |   **705.06 ms** | **1,584.63 ms** | **86.859 ms** |         **-** | **11751.13 KB** |
| **ConcurrentPreviews** | **4**       | **geometry** |   **295.76 ms** |   **187.48 ms** | **10.277 ms** |  **500.0000** |  **9335.95 KB** |
| **ConcurrentPreviews** | **4**       | **text**     |   **308.58 ms** |   **122.94 ms** |  **6.739 ms** |         **-** |  **2405.84 KB** |
| **ConcurrentPreviews** | **8**       | **all**      | **1,865.64 ms** |   **773.18 ms** | **42.381 ms** | **1000.0000** | **23477.19 KB** |
| **ConcurrentPreviews** | **8**       | **geometry** |   **822.23 ms** | **1,002.65 ms** | **54.958 ms** | **1000.0000** | **18670.73 KB** |
| **ConcurrentPreviews** | **8**       | **text**     |   **926.65 ms** | **1,651.80 ms** | **90.541 ms** |         **-** |  **4810.55 KB** |
