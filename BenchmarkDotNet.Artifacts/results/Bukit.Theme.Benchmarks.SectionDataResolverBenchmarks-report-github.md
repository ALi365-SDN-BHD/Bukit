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
| **Resolve_WithSourceOnly**      | **100**       |     **8.376 μs** |   **1.6515 μs** |  **0.0905 μs** |   **2.9449** |  **0.0305** |       **-** |   **24.11 KB** |
| Resolve_WithSourceAndFilter | 100       |    10.817 μs |   3.4314 μs |  0.1881 μs |   3.3569 |  0.0153 |       - |   27.48 KB |
| Resolve_WithSourceAndSort   | 100       |    17.716 μs |   9.4490 μs |  0.5179 μs |   3.5400 |  0.0305 |       - |   28.93 KB |
| Resolve_AllPages            | 100       |     1.289 μs |   0.0813 μs |  0.0045 μs |   0.5360 |  0.0038 |       - |    4.38 KB |
| **Resolve_WithSourceOnly**      | **1000**      |    **86.220 μs** |  **22.5237 μs** |  **1.2346 μs** |  **27.8320** |  **1.7090** |       **-** |  **227.96 KB** |
| Resolve_WithSourceAndFilter | 1000      |   105.495 μs |  26.7290 μs |  1.4651 μs |  32.5928 |  1.2207 |       - |  266.55 KB |
| Resolve_WithSourceAndSort   | 1000      |   211.364 μs |  62.4134 μs |  3.4211 μs |  33.2031 |  2.1973 |       - |  271.45 KB |
| Resolve_AllPages            | 1000      |    10.129 μs |   5.3492 μs |  0.2932 μs |   3.9673 |  0.2289 |       - |   32.45 KB |
| **Resolve_WithSourceOnly**      | **5000**      |   **643.634 μs** | **249.8552 μs** | **13.6954 μs** | **168.9453** | **60.5469** | **34.1797** |  **1233.3 KB** |
| Resolve_WithSourceAndFilter | 5000      |   517.398 μs | 180.6153 μs |  9.9001 μs | 160.1563 | 17.5781 |       - | 1314.59 KB |
| Resolve_WithSourceAndSort   | 5000      | 1,407.176 μs | 487.1151 μs | 26.7004 μs | 195.3125 | 62.5000 | 35.1563 | 1448.67 KB |
| Resolve_AllPages            | 5000      |   110.767 μs | 147.0116 μs |  8.0582 μs |  34.1797 | 19.1650 | 18.5547 |  256.61 KB |
