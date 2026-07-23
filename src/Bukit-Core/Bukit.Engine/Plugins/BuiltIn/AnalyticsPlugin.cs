using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Analytics;
using Bukit.Config;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class AnalyticsPlugin :
    IBukitPlugin,
    IOrderedPlugin,
    IHookFilterPlugin,
    IHtmlTransformPlugin
{
    private readonly ResolvedAnalyticsConfig _config;
    private readonly AnalyticsBuildState _state;

    internal AnalyticsPlugin(AppConfig config)
        : this(
            config,
            AnalyticsBuildState.Create(config, BuildExecutionMode.Production))
    {
    }

    internal AnalyticsPlugin(
        AppConfig config,
        AnalyticsBuildState state)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(state);
        _config = AnalyticsConfigNormalizer.Normalize(config.Site.Analytics);
        _state = state;
    }

    public string Name => "analytics";

    public string Version => "1.0.0";

    public int Order => 1000;

    public bool SupportsHook(string hook)
        => string.Equals(hook, HtmlTransformHooks.HtmlTransform, StringComparison.Ordinal);

    public IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context)
        => new AnalyticsHtmlTransform(
            _config,
            AnalyticsProviderRegistry.CreateDefault(),
            _state);
}
