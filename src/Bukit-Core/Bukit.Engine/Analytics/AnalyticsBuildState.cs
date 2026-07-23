using System.Collections.Concurrent;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Analytics;

internal static class AnalyticsSkipReason
{
    internal const string PluginDisabled = "plugin_disabled";
    internal const string AnalyticsDisabled = "analytics_disabled";
    internal const string NoProviders = "no_providers";
    internal const string DevelopmentMode = "development_mode";
    internal const string HeadMissing = "head_missing";
    internal const string BodyMissing = "body_missing";
    internal const string IncrementalUnchanged = "incremental_unchanged";
    internal const string TransformFailed = "transform_failed";

    internal static readonly string[] All =
    [
        PluginDisabled,
        AnalyticsDisabled,
        NoProviders,
        DevelopmentMode,
        HeadMissing,
        BodyMissing,
        IncrementalUnchanged,
        TransformFailed
    ];

    internal static bool IsKnown(string reason)
        => All.Contains(reason, StringComparer.Ordinal);
}

internal sealed record AnalyticsBuildSnapshot(
    bool PluginEnabled,
    bool AnalyticsEnabled,
    bool ProductionOnly,
    BuildExecutionMode ExecutionMode,
    IReadOnlyList<string> ProviderTypes,
    ResolvedGoogleConsent? GoogleConsent,
    AnalyticsCspRequirements? Csp,
    long ProcessedHtml,
    long InjectedHtml,
    IReadOnlyDictionary<string, long> SkippedByReason);

internal sealed class AnalyticsBuildState
{
    private readonly AppConfig? _sourceConfig;
    private readonly ResolvedAnalyticsConfig _config;
    private readonly ConcurrentDictionary<string, long> _skippedByReason =
        new(StringComparer.Ordinal);
    private long _processedHtml;
    private long _injectedHtml;
    private int _renderOutcomeRecorded;

    internal AnalyticsBuildState(
        bool pluginEnabled,
        ResolvedAnalyticsConfig config,
        BuildExecutionMode executionMode,
        AppConfig? sourceConfig = null)
    {
        PluginEnabled = pluginEnabled;
        _config = config;
        ExecutionMode = executionMode;
        _sourceConfig = sourceConfig;
    }

    internal bool PluginEnabled { get; }

    internal BuildExecutionMode ExecutionMode { get; }

    internal static AnalyticsBuildState Create(AppConfig config, BuildExecutionMode executionMode)
        => new(
            ResolvePluginEnabled(config.Site.Plugins),
            AnalyticsConfigNormalizer.Normalize(config.Site.Analytics),
            executionMode,
            config);

    internal static void Attach(BuildContext context, AnalyticsBuildState state)
        => context.Data[BuildContextDataKeys.AnalyticsBuildState] = state;

    internal static AnalyticsBuildState GetOrCreate(
        BuildContext context,
        AppConfig config,
        BuildExecutionMode executionMode)
    {
        if (context.Data.TryGetValue(BuildContextDataKeys.AnalyticsBuildState, out var value) &&
            value is AnalyticsBuildState state &&
            ReferenceEquals(state._sourceConfig, config) &&
            state.ExecutionMode == executionMode)
        {
            return state;
        }

        state = Create(config, executionMode);
        Attach(context, state);
        return state;
    }

    internal static bool ResolvePluginEnabled(IReadOnlyDictionary<string, PluginToggleConfig>? plugins)
    {
        if (plugins is null)
        {
            return true;
        }

        foreach (var (name, toggle) in plugins)
        {
            if (string.Equals(name, "analytics", StringComparison.OrdinalIgnoreCase))
            {
                return toggle.Enabled;
            }
        }

        return true;
    }

    internal void RecordProcessed() => Interlocked.Increment(ref _processedHtml);

    internal void RecordInjected() => Interlocked.Increment(ref _injectedHtml);

    internal void RecordSkipped(string reason, long count = 1)
    {
        if (!AnalyticsSkipReason.IsKnown(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown analytics skip reason.");
        }

        if (count <= 0)
        {
            return;
        }

        _skippedByReason.AddOrUpdate(reason, count, (_, current) => checked(current + count));
    }

    internal void RecordRenderOutcome(int renderedCount, int incrementalUnchangedCount)
    {
        if (Interlocked.Exchange(ref _renderOutcomeRecorded, 1) != 0)
        {
            return;
        }

        if (!PluginEnabled)
        {
            RecordSkipped(AnalyticsSkipReason.PluginDisabled, renderedCount);
        }

        RecordSkipped(AnalyticsSkipReason.IncrementalUnchanged, incrementalUnchangedCount);
    }

    internal AnalyticsBuildSnapshot Snapshot()
    {
        var skipped = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var reason in AnalyticsSkipReason.All)
        {
            if (_skippedByReason.TryGetValue(reason, out var count) && count > 0)
            {
                skipped[reason] = count;
            }
        }

        return new AnalyticsBuildSnapshot(
            PluginEnabled,
            _config.Enabled,
            _config.ProductionOnly,
            ExecutionMode,
            _config.Providers.Select(provider => provider.Type).ToArray(),
            _config.GoogleConsent,
            AnalyticsCspRequirementsBuilder.Build(PluginEnabled, _config, ExecutionMode),
            Interlocked.Read(ref _processedHtml),
            Interlocked.Read(ref _injectedHtml),
            skipped);
    }
}
