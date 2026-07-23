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
    private readonly AppConfig _config;

    internal AnalyticsPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "analytics";

    public string Version => "1.0.0";

    public int Order => 1000;

    public bool SupportsHook(string hook)
        => string.Equals(hook, HtmlTransformHooks.HtmlTransform, StringComparison.Ordinal);

    public IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context)
        => new AnalyticsHtmlTransform(
            AnalyticsConfigNormalizer.Normalize(_config.Site.Analytics),
            AnalyticsProviderRegistry.CreateDefault(),
            AnalyticsBuildState.GetOrCreate(context.BuildContext, _config, context.ExecutionMode));
}
