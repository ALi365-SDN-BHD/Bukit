using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Analytics;

namespace Bukit.Engine.Plugins;

internal sealed class PluginExecutionSession
{
    private PluginExecutionSession(
        PluginExecutionPolicy policy,
        IReadOnlyList<(IBukitPlugin Plugin, string Source)> registrations,
        AnalyticsBuildState analyticsBuildState)
    {
        Policy = policy;
        Registrations = registrations;
        AnalyticsBuildState = analyticsBuildState;
    }

    internal PluginExecutionPolicy Policy { get; }

    internal IReadOnlyList<(IBukitPlugin Plugin, string Source)> Registrations { get; }

    internal AnalyticsBuildState AnalyticsBuildState { get; }

    internal static PluginExecutionSession Create(
        AppConfig config,
        BuildExecutionMode executionMode)
    {
        ArgumentNullException.ThrowIfNull(config);

        var analyticsBuildState = AnalyticsBuildState.Create(config, executionMode);
        return new PluginExecutionSession(
            PluginExecutionPolicy.From(config.Site),
            PluginRegistry.BuildPlugins(config, analyticsBuildState),
            analyticsBuildState);
    }

    internal static PluginExecutionSession CreateCompatibility(
        BuildExecutionMode executionMode = BuildExecutionMode.Production)
        => Create(PluginRegistry.CompatibilityConfiguration, executionMode);
}
