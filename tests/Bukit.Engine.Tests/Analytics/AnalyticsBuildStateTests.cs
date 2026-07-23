using Bukit.Config;
using Bukit.Engine.Analytics;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
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

    [Fact]
    public void GetOrCreate_UsesExplicitEffectiveConfigInsteadOfContextBridge()
    {
        var contextConfig = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "context",
                Title = "Context",
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = false }
                }
            },
            Content = TestContent.Markdown()
        };
        var effectiveConfig = contextConfig with
        {
            Site = contextConfig.Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = true }
                }
            }
        };
        var context = new BuildContext
        {
            RootDir = ".",
            OutputDir = "dist",
            BaseUrl = "/",
            LayoutsDir = "layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            BodyStore = NullContentBodyStore.Instance,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var state = AnalyticsBuildState.GetOrCreate(
            context,
            effectiveConfig,
            BuildExecutionMode.Production);

        Assert.True(state.Snapshot().PluginEnabled);
    }

    [Fact]
    public void GetOrCreate_SameContextAndDifferentConfigReference_ReplacesCachedState()
    {
        var configA = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "a",
                Title = "A",
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = false }
                }
            },
            Content = TestContent.Markdown()
        };
        var configB = configA with
        {
            Site = configA.Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = true }
                }
            }
        };
        var context = new BuildContext
        {
            RootDir = ".",
            OutputDir = "dist",
            BaseUrl = "/",
            LayoutsDir = "layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            BodyStore = NullContentBodyStore.Instance,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var first = AnalyticsBuildState.GetOrCreate(
            context,
            configA,
            BuildExecutionMode.Production);
        var second = AnalyticsBuildState.GetOrCreate(
            context,
            configB,
            BuildExecutionMode.Production);
        var third = AnalyticsBuildState.GetOrCreate(
            context,
            configB,
            BuildExecutionMode.Development);

        Assert.False(first.Snapshot().PluginEnabled);
        Assert.True(second.Snapshot().PluginEnabled);
        Assert.NotSame(first, second);
        Assert.NotSame(second, third);
        Assert.Equal(BuildExecutionMode.Development, third.ExecutionMode);
    }
}
