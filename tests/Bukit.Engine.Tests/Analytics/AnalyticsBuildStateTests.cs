using Bukit.Config;
using Bukit.Engine.Analytics;
using Xunit;

namespace Bukit.Engine.Tests.Analytics;

public sealed class AnalyticsBuildStateTests
{
    [Fact]
    public void Snapshot_ConcurrentUpdates_AreAtomicAndUseFixedReasons()
    {
        var state = new AnalyticsBuildState(
            pluginEnabled: true,
            AnalyticsConfigNormalizer.Normalize(new AnalyticsConfig
            {
                Providers =
                [
                    new AnalyticsProviderConfig
                    {
                        Type = "google-analytics",
                        MeasurementId = "G-TEST123"
                    }
                ]
            }),
            BuildExecutionMode.Production);

        Parallel.For(0, 1_000, _ =>
        {
            state.RecordProcessed();
            state.RecordInjected();
            state.RecordSkipped(AnalyticsSkipReason.HeadMissing);
        });

        var snapshot = state.Snapshot();
        Assert.Equal(1_000, snapshot.ProcessedHtml);
        Assert.Equal(1_000, snapshot.InjectedHtml);
        Assert.Equal(1_000, snapshot.SkippedByReason["head_missing"]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            state.RecordSkipped("unknown_reason");
        });
    }

    [Fact]
    public void RecordRenderOutcome_IsIdempotentAndCountsOnlyThisBuild()
    {
        var state = new AnalyticsBuildState(
            pluginEnabled: false,
            AnalyticsConfigNormalizer.Normalize(new AnalyticsConfig()),
            BuildExecutionMode.Development);

        state.RecordRenderOutcome(renderedCount: 3, incrementalUnchangedCount: 2);
        state.RecordRenderOutcome(renderedCount: 3, incrementalUnchangedCount: 2);

        var snapshot = state.Snapshot();
        Assert.Equal(3, snapshot.SkippedByReason["plugin_disabled"]);
        Assert.Equal(2, snapshot.SkippedByReason["incremental_unchanged"]);
        Assert.Equal(0, snapshot.ProcessedHtml);
    }
}
