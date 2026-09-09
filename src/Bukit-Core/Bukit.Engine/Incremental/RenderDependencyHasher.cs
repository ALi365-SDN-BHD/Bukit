using System.Security.Cryptography;
using Bukit.Config;
using Bukit.Engine.Analytics;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

internal static class RenderDependencyHasher
{
    internal static string Compute(
        AppConfig config,
        SiteModel siteModel,
        BuildExecutionMode executionMode = BuildExecutionMode.Production,
        string analyticsRendererContractVersion = AnalyticsRendererContract.Version,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>>? seoAlternates = null)
    {
        var context = new RenderDependencyContext(
            config,
            siteModel,
            executionMode,
            analyticsRendererContractVersion);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var writer = new RenderDependencyHashWriter(hasher);
        foreach (var contributor in RenderDependencyContributorPlan.Contributors)
        {
            contributor.Contribute(context, writer);
        }

        if (config.Site.Seo.Enabled && seoAlternates is not null)
        {
            foreach (var (key, alternates) in seoAlternates.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                writer.AppendFramedValue("alternateGroup", key);
                foreach (var alternate in alternates)
                {
                    writer.AppendFramedValue("hreflang", alternate.Hreflang);
                    writer.AppendFramedValue("href", alternate.Href);
                }
            }
        }

        return HashUtil.ToHexLower(hasher.GetHashAndReset());
    }

    internal static string ComputeForRoute(
        string baseHash,
        string metadataRouteUrl,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
    {
        if (routeMetadata is null || !routeMetadata.TryGetValue(metadataRouteUrl, out var metadata))
        {
            return baseHash;
        }

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var writer = new RenderDependencyHashWriter(hasher);
        writer.AppendUtf8(baseHash);
        AppendRouteMetadataValue(writer, metadata);
        return HashUtil.ToHexLower(hasher.GetHashAndReset());
    }

    private static void AppendRouteMetadataValue(
        RenderDependencyHashWriter writer,
        RouteMetadataEntry metadata)
    {
        writer.AppendFramedValue("route", metadata.Route);
        writer.AppendFramedValue("title", metadata.Title);
        writer.AppendFramedValue("summary", metadata.Summary);
        writer.AppendFramedValue("seoTitle", metadata.SeoTitle);
        writer.AppendFramedValue("seoDescription", metadata.SeoDescription);
    }
}
