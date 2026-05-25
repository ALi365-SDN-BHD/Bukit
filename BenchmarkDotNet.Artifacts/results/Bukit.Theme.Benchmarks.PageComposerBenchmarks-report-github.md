```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.7.7 (24G720) [Darwin 24.6.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.100
  [Host]   : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method          | SectionCount | Mean     | Error     | StdDev    | Gen0   | Allocated |
|---------------- |------------- |---------:|----------:|----------:|-------:|----------:|
| **ParseAndCompose** | **1**            | **4.043 μs** | **0.2436 μs** | **0.0134 μs** | **0.1678** |   **1.38 KB** |
| **ParseAndCompose** | **5**            | **5.057 μs** | **0.3919 μs** | **0.0215 μs** | **0.1678** |   **1.38 KB** |
| **ParseAndCompose** | **10**           | **6.310 μs** | **1.1226 μs** | **0.0615 μs** | **0.1678** |   **1.38 KB** |
