using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins;

internal static class HtmlTransformHooks
{
    internal const string HtmlTransform = "html-transform";
}

internal sealed record HtmlTransformPluginContext(
    BuildContext BuildContext,
    BuildExecutionMode ExecutionMode);

internal interface IHtmlTransformPlugin
{
    IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context);
}
