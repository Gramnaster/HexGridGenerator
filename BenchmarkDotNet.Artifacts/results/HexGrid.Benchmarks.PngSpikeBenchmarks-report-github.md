```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900HX 2.30GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-FGEKWY : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=3  LaunchCount=1  WarmupCount=1  

```
| Method              | Canvas | Mean       | Error       | StdDev   | Ratio | RatioSD |
|-------------------- |------- |-----------:|------------:|---------:|------:|--------:|
| Gdi                 | A0     | 1,551.0 ms | 1,359.59 ms | 74.52 ms |  1.00 |    0.06 |
| SerialNoneFastest   | A0     |   787.1 ms |   305.28 ms | 16.73 ms |  0.51 |    0.02 |
| ParallelNoneFastest | A0     |   205.5 ms |   191.84 ms | 10.52 ms |  0.13 |    0.01 |
| ParallelUpFastest   | A0     |   214.6 ms |   126.73 ms |  6.95 ms |  0.14 |    0.01 |
| ParallelUpOptimal   | A0     |   238.9 ms |    83.76 ms |  4.59 ms |  0.15 |    0.01 |
