using System.Collections.Concurrent;
using Xunit;

namespace Bukit.Engine.Tests;

public class BuildStageMetricsCollectorConcurrencyTests
{
    [Fact]
    public void Increment_Should_BeThreadSafe_When_CalledConcurrently()
    {
        var collector = new BuildStageMetricsCollector();
        var iterations = 100_000;
        var stage = "render";

        Parallel.For(0, iterations, _ => collector.Increment(stage));

        var snapshot = collector.Snapshot();
        Assert.Equal(iterations, snapshot.Counts[stage]);
    }

    [Fact]
    public void AddDuration_Should_BeThreadSafe_When_CalledConcurrently()
    {
        var collector = new BuildStageMetricsCollector();
        var iterations = 100_000;
        var stage = "bodyLoad";

        Parallel.For(0, iterations, _ => collector.AddDuration(stage, 1));

        var snapshot = collector.Snapshot();
        Assert.Equal((long)iterations, snapshot.DurationsMs[stage]);
    }

    [Fact]
    public void Merge_Should_BeThreadSafe_When_CalledConcurrently()
    {
        var collector = new BuildStageMetricsCollector();
        var iterations = 10_000;

        var metricsToMerge = Enumerable.Range(0, iterations).Select(_ =>
        {
            var m = new BuildStageMetricsCollector();
            m.Increment("pageRender");
            m.AddDuration("pageRender", 5);
            return m.Snapshot();
        }).ToArray();

        Parallel.For(0, iterations, i => collector.Merge(metricsToMerge[i]));

        var snapshot = collector.Snapshot();
        Assert.Equal(iterations, snapshot.Counts["pageRender"]);
        Assert.Equal((long)iterations * 5, snapshot.DurationsMs["pageRender"]);
    }

    [Fact]
    public void MixedConcurrentOperations_Should_ProduceConsistentCounts()
    {
        var collector = new BuildStageMetricsCollector();
        var iterations = 50_000;

        Parallel.For(0, iterations, i =>
        {
            switch (i % 3)
            {
                case 0:
                    collector.Increment("pageRender");
                    collector.AddDuration("pageRender", 1);
                    break;
                case 1:
                    collector.Increment("listBuild");
                    collector.AddDuration("listBuild", 2);
                    break;
                default:
                    collector.Increment("staticRender");
                    collector.AddDuration("staticRender", 3);
                    break;
            }
        });

        var snapshot = collector.Snapshot();
        var total = snapshot.Counts.Values.Sum();
        Assert.Equal(iterations, total);
    }
}
