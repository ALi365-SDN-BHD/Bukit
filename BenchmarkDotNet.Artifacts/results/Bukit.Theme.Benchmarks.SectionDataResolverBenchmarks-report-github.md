```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.7.7 (24G720) [Darwin 24.6.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.100
  [Host]   : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                      | ItemCount | Mean         | Error       | StdDev     | Gen0     | Gen1    | Gen2    | Allocated  |
|---------------------------- |---------- |-------------:|------------:|-----------:|---------:|--------:|--------:|-----------:|
| **Resolve_WithSourceOnly**      | **100**       |     **8.400 μs** |   **0.8007 μs** |  **0.0439 μs** |   **2.9449** |  **0.0305** |       **-** |   **24.11 KB** |
| Resolve_WithSourceAndFilter | 100       |    10.727 μs |   0.8731 μs |  0.0479 μs |   3.3569 |  0.0153 |       - |   27.48 KB |
| Resolve_WithSourceAndSort   | 100       |    16.645 μs |   0.5045 μs |  0.0277 μs |   3.5400 |  0.0305 |       - |   28.93 KB |
| Resolve_AllPages            | 100       |     1.300 μs |   0.0254 μs |  0.0014 μs |   0.5360 |  0.0038 |       - |    4.38 KB |
| **Resolve_WithSourceOnly**      | **1000**      |    **97.459 μs** |  **50.2997 μs** |  **2.7571 μs** |  **27.8320** |  **1.7090** |       **-** |  **227.96 KB** |
| Resolve_WithSourceAndFilter | 1000      |   107.414 μs |  47.2979 μs |  2.5926 μs |  32.5928 |  1.2207 |       - |  266.55 KB |
| Resolve_WithSourceAndSort   | 1000      |   213.457 μs |  60.4187 μs |  3.3118 μs |  33.2031 |  2.1973 |       - |  271.45 KB |
| Resolve_AllPages            | 1000      |    10.283 μs |   0.0255 μs |  0.0014 μs |   3.9673 |  0.2289 |       - |   32.45 KB |
| **Resolve_WithSourceOnly**      | **5000**      |   **776.232 μs** | **853.6810 μs** | **46.7931 μs** | **169.9219** | **49.8047** | **35.1563** | **1233.32 KB** |
| Resolve_WithSourceAndFilter | 5000      |   571.129 μs | 120.2476 μs |  6.5912 μs | 160.1563 | 17.5781 |       - | 1314.59 KB |
| Resolve_WithSourceAndSort   | 5000      | 1,428.046 μs |  84.8416 μs |  4.6505 μs | 195.3125 | 62.5000 | 35.1563 | 1448.67 KB |
| Resolve_AllPages            | 5000      |   114.677 μs |  22.2202 μs |  1.2180 μs |  34.1797 | 19.1650 | 18.5547 |  256.61 KB |
