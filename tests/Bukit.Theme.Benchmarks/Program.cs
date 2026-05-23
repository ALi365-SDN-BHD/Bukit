using BenchmarkDotNet.Running;
using Bukit.Theme.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(SectionDataResolverBenchmarks).Assembly).Run(args);
