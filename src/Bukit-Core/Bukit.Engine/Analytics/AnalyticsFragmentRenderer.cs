namespace Bukit.Engine.Analytics;

internal static class AnalyticsFragmentRenderer
{
    internal static AnalyticsHtmlFragments[] Render(
        ResolvedAnalyticsConfig config,
        AnalyticsProviderRegistry providers,
        AnalyticsRenderContext renderContext)
    {
        var fragments = new List<AnalyticsHtmlFragments>(config.Providers.Count + 1);
        if (config.GoogleConsent is { } googleConsent)
        {
            fragments.Add(GoogleConsentRenderer.Render(googleConsent));
        }

        var googleAnalyticsBootstrapRendered = false;
        foreach (var provider in config.Providers)
        {
            var renderer = providers.GetRequired(provider.Type);
            if (renderer is GoogleAnalyticsProvider googleAnalytics)
            {
                fragments.Add(googleAnalyticsBootstrapRendered
                    ? googleAnalytics.RenderDestination(provider, renderContext)
                    : config.GoogleConsent is null
                        ? googleAnalytics.Render(provider, renderContext)
                        : googleAnalytics.RenderAfterConsent(provider, renderContext));
                googleAnalyticsBootstrapRendered = true;
                continue;
            }

            fragments.Add(renderer.Render(provider, renderContext));
        }

        return fragments.ToArray();
    }
}
