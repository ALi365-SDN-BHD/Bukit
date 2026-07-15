using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Analytics;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class AnalyticsPlugin :
    IBukitPlugin,
    IOrderedPlugin,
    IHookFilterPlugin,
    IHtmlTransformPlugin
{
    public string Name => "analytics";

    public string Version => "1.0.0";

    public int Order => 1000;

    public bool SupportsHook(string hook)
        => string.Equals(hook, HtmlTransformHooks.HtmlTransform, StringComparison.Ordinal);

    public IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context)
        => new AnalyticsHtmlTransform(
            AnalyticsConfigNormalizer.Normalize(context.BuildContext.Config.Site.Analytics),
            AnalyticsProviderRegistry.CreateDefault(),
            AnalyticsBuildState.GetOrCreate(context.BuildContext, context.ExecutionMode));
}
