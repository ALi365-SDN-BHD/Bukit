using System.Security.Cryptography;
using System.Text;
using Bukit.Config;

namespace Bukit.Engine.Analytics;

internal sealed record AnalyticsCspRequirements(
    IReadOnlyList<string> InlineScriptSha256,
    IReadOnlyList<string> ScriptSrcOrigins,
    IReadOnlyList<string> FrameSrcOrigins,
    bool DynamicContainerDestinationsUnknown);

internal static class AnalyticsCspRequirementsBuilder
{
    private const string GoogleTagOrigin = "https://www.googletagmanager.com";

    internal static AnalyticsCspRequirements? Build(
        bool pluginEnabled,
        ResolvedAnalyticsConfig config,
        BuildExecutionMode executionMode)
    {
        if (config.CspMode != "requirements-report")
        {
            return null;
        }

        if (!pluginEnabled || !config.Enabled || config.Providers.Count == 0 ||
            (config.ProductionOnly && executionMode == BuildExecutionMode.Development))
        {
            return Empty();
        }

        var fragments = AnalyticsFragmentRenderer.Render(
            config,
            AnalyticsProviderRegistry.CreateDefault(),
            new AnalyticsRenderContext("/", "index.html", IsListPage: false, executionMode));
        var inlineHashes = fragments
            .SelectMany(AllFragments)
            .SelectMany(ExtractInlineScriptBodies)
            .Select(HashInlineScript)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var scriptOrigins = new HashSet<string>(StringComparer.Ordinal);
        var frameOrigins = new HashSet<string>(StringComparer.Ordinal);
        var dynamicContainerDestinationsUnknown = false;
        foreach (var provider in config.Providers)
        {
            switch (provider.Type)
            {
                case "google-analytics":
                    scriptOrigins.Add(GoogleTagOrigin);
                    break;
                case "google-tag-manager":
                    scriptOrigins.Add(GoogleTagOrigin);
                    frameOrigins.Add(GoogleTagOrigin);
                    dynamicContainerDestinationsUnknown = true;
                    break;
                case "plausible":
                case "umami":
                    scriptOrigins.Add(GetOrigin(provider.Options["scriptUrl"]));
                    break;
            }
        }

        return new AnalyticsCspRequirements(
            inlineHashes,
            scriptOrigins.Order(StringComparer.Ordinal).ToArray(),
            frameOrigins.Order(StringComparer.Ordinal).ToArray(),
            dynamicContainerDestinationsUnknown);
    }

    private static AnalyticsCspRequirements Empty()
        => new([], [], [], DynamicContainerDestinationsUnknown: false);

    private static IEnumerable<string> AllFragments(AnalyticsHtmlFragments fragments)
    {
        if (fragments.HeadStart is not null)
        {
            yield return fragments.HeadStart;
        }

        if (fragments.HeadEnd is not null)
        {
            yield return fragments.HeadEnd;
        }

        if (fragments.BodyStart is not null)
        {
            yield return fragments.BodyStart;
        }
    }

    private static IEnumerable<string> ExtractInlineScriptBodies(string html)
    {
        var index = 0;
        while ((index = html.IndexOf("<script", index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var tagEnd = html.IndexOf('>', index);
            if (tagEnd < 0)
            {
                yield break;
            }

            var close = html.IndexOf("</script>", tagEnd + 1, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
            {
                yield break;
            }

            var openingTag = html[index..(tagEnd + 1)];
            if (!openingTag.Contains(" src=", StringComparison.OrdinalIgnoreCase))
            {
                yield return html[(tagEnd + 1)..close];
            }

            index = close + "</script>".Length;
        }
    }

    private static string HashInlineScript(string body)
        => "sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

    private static string GetOrigin(string absoluteUrl)
    {
        var uri = new Uri(absoluteUrl, UriKind.Absolute);
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
