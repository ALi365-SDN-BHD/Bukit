namespace Bukit.Engine.Analytics;

internal static class GoogleConsentRenderer
{
    internal static AnalyticsHtmlFragments Render(ResolvedGoogleConsent consent)
    {
        var waitForUpdate = consent.WaitForUpdateMs is { } waitForUpdateMs
            ? $",\n  'wait_for_update': {waitForUpdateMs}"
            : string.Empty;
        var headStart = $$"""
            <script>
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('consent', 'default', {
              'ad_storage': '{{AnalyticsValueEncoder.JavaScriptString(consent.AdStorage)}}',
              'analytics_storage': '{{AnalyticsValueEncoder.JavaScriptString(consent.AnalyticsStorage)}}',
              'ad_user_data': '{{AnalyticsValueEncoder.JavaScriptString(consent.AdUserData)}}',
              'ad_personalization': '{{AnalyticsValueEncoder.JavaScriptString(consent.AdPersonalization)}}'{{waitForUpdate}}
            });
            </script>
            """;

        return new AnalyticsHtmlFragments("google-consent:default", HeadStart: headStart);
    }
}
