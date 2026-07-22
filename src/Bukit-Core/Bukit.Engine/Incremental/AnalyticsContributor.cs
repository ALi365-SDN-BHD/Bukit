using System.Globalization;
using Bukit.Engine.Analytics;

namespace Bukit.Engine.Incremental;

internal sealed class AnalyticsContributor : IRenderDependencyContributor
{
    public string Name => "analytics";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var analyticsPluginEnabled = AnalyticsBuildState.ResolvePluginEnabled(context.Config.Site.Plugins);
        writer.AppendFramedValue(
            "analytics.pluginEnabled",
            analyticsPluginEnabled.ToString(CultureInfo.InvariantCulture));
        writer.AppendFramedValue("analytics.rendererContractVersion", context.AnalyticsRendererContractVersion);
        if (!analyticsPluginEnabled)
        {
            return;
        }

        var resolvedAnalytics = AnalyticsConfigNormalizer.Normalize(context.Config.Site.Analytics);
        writer.AppendFramedValue("analytics.enabled", resolvedAnalytics.Enabled.ToString(CultureInfo.InvariantCulture));
        writer.AppendFramedValue("analytics.productionOnly", resolvedAnalytics.ProductionOnly.ToString(CultureInfo.InvariantCulture));
        writer.AppendFramedValue("analytics.executionMode", context.ExecutionMode.ToString());
        writer.AppendFramedValue(
            "analytics.googleConsent.configured",
            (resolvedAnalytics.GoogleConsent is not null).ToString(CultureInfo.InvariantCulture));
        if (resolvedAnalytics.GoogleConsent is { } googleConsent)
        {
            writer.AppendFramedValue("analytics.googleConsent.mode", googleConsent.Mode);
            writer.AppendFramedValue("analytics.googleConsent.adStorage", googleConsent.AdStorage);
            writer.AppendFramedValue("analytics.googleConsent.analyticsStorage", googleConsent.AnalyticsStorage);
            writer.AppendFramedValue("analytics.googleConsent.adUserData", googleConsent.AdUserData);
            writer.AppendFramedValue("analytics.googleConsent.adPersonalization", googleConsent.AdPersonalization);
            writer.AppendFramedValue(
                "analytics.googleConsent.waitForUpdateMs.configured",
                googleConsent.WaitForUpdateMs.HasValue.ToString(CultureInfo.InvariantCulture));
            if (googleConsent.WaitForUpdateMs is { } waitForUpdateMs)
            {
                writer.AppendFramedValue(
                    "analytics.googleConsent.waitForUpdateMs",
                    waitForUpdateMs.ToString(CultureInfo.InvariantCulture));
            }
        }

        writer.AppendFramedValue(
            "analytics.providerCount",
            resolvedAnalytics.Providers.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var provider in resolvedAnalytics.Providers)
        {
            writer.AppendFramedValue("analytics.provider.type", provider.Type);
            writer.AppendFramedValue("analytics.provider.key", provider.Key);
            foreach (var option in provider.Options.OrderBy(option => option.Key, StringComparer.Ordinal))
            {
                writer.AppendFramedValue($"analytics.provider.option.{option.Key}", option.Value);
            }
        }
    }
}
