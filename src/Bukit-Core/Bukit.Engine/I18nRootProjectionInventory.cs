using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class I18nRootProjectionInventory
{
    internal static IReadOnlyList<PublishRepresentationOutput> BuildOutputs(
        string outputDir,
        PublishRepresentation representation,
        IReadOnlyList<BuildVariantResult> results)
    {
        var path = Path.Combine(outputDir, representation.Path);
        var fileExists = File.Exists(path);
        var text = fileExists ? File.ReadAllText(path) : null;
        var outputs = new List<PublishRepresentationOutput>();
        foreach (var result in results)
        {
            foreach (var (_, seo) in result.SeoIndex.OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                var url = I18nRootProjectionPath.CombineBaseUrl(result.BaseUrl, seo.Route.Url);
                var routePresent = ContainsInvariant(text, url) || ContainsInvariant(text, seo.Canonical);
                var exists = representation.Kind.Equals("robots", StringComparison.OrdinalIgnoreCase)
                    ? fileExists
                    : fileExists && seo.Indexable && routePresent;
                outputs.Add(new PublishRepresentationOutput(
                    representation.Kind,
                    url,
                    representation.Path.Replace('\\', '/'),
                    exists,
                    seo.Indexable));
            }
        }

        if (outputs.Count > 0)
        {
            return outputs;
        }

        return
        [
            new PublishRepresentationOutput(
                representation.Kind,
                "/" + representation.Path.Replace('\\', '/'),
                representation.Path.Replace('\\', '/'),
                fileExists,
                Indexable: false)
        ];
    }

    private static IReadOnlyList<PublishProjectionResult> BuildAll(
        string outputDir,
        IReadOnlyList<BuildVariantResult> results)
    {
        return PublishRepresentationRegistry.AggregateRepresentations()
            .Select(representation => new PublishProjectionResult(
                representation,
                BuildOutputs(outputDir, representation, results)))
            .ToArray();
    }

    private static bool ContainsInvariant(string? haystack, string needle)
        => !string.IsNullOrWhiteSpace(haystack) &&
           !string.IsNullOrWhiteSpace(needle) &&
           haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}

internal static class I18nRootProjectionPath
{
    internal static string CombineBaseUrl(string baseUrl, string routeUrl)
    {
        var normalizedBaseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var normalizedRouteUrl = routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl;
        return string.IsNullOrWhiteSpace(normalizedBaseUrl)
            ? normalizedRouteUrl
            : normalizedBaseUrl + normalizedRouteUrl;
    }
}
