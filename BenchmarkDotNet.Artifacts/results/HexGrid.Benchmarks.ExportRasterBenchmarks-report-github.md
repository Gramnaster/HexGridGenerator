```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-KRRUPJ : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=8  LaunchCount=1  WarmupCount=2  

```
| Method  | Scenario         | Mean       | Error     | StdDev    | Allocated |
|-------- |----------------- |-----------:|----------:|----------:|----------:|
| **SavePng** | **DefaultHex**       |   **262.9 ms** |  **26.65 ms** |  **13.94 ms** |     **648 B** |
| **SavePng** | **LargeHexLabelled** |   **975.9 ms** |  **88.22 ms** |  **31.46 ms** |     **680 B** |
| **SavePng** | **A0HexLabelled**    | **2,585.2 ms** | **288.11 ms** | **150.69 ms** |     **680 B** |
