using Bukit.Config;
using Bukit.Engine.Analytics;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;

namespace Bukit.Engine;

internal sealed record VariantAnalyticsTransformPlan(
    AnalyticsBuildState AnalyticsBuildState,
    CollectedHtmlTransforms PluginHtmlTransforms,
    HtmlTransformPipeline HtmlTransformPipeline);

internal static class VariantAnalyticsTransformStage
{
    internal static VariantAnalyticsTransformPlan Create(
        ConfigOverrides overrides,
        BuildContext pluginContext,
        PluginExecutionSession pluginSession,
        SeoPipelineResult seoResult)
    {
        var pluginHtmlTransforms = PluginRunner.CollectHtmlTransforms(
            pluginContext,
            pluginSession,
            overrides.ExecutionMode);
        var htmlTransformPipeline = CreateHtmlTransformPipeline(
            seoResult,
            pluginHtmlTransforms,
            overrides.ExecutionMode);
        return new VariantAnalyticsTransformPlan(
            pluginSession.AnalyticsBuildState,
            pluginHtmlTransforms,
            htmlTransformPipeline);
    }

    internal static HtmlTransformPipeline CreateHtmlTransformPipeline(
        SeoPipelineResult seoResult,
        CollectedHtmlTransforms pluginHtmlTransforms,
        BuildExecutionMode executionMode)
    {
        var transforms = new List<IHtmlTransform>();
        if (seoResult.HtmlTransform is not null)
        {
            transforms.Add(seoResult.HtmlTransform);
        }

        transforms.AddRange(pluginHtmlTransforms);
        return new HtmlTransformPipeline(transforms, executionMode);
    }
}
