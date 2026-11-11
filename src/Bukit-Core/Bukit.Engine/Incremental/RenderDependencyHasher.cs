using System.Security.Cryptography;
using Bukit.Config;
using Bukit.Engine.Analytics;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

internal static class RenderDependencyHasher
{
    /// <summary>
    /// The hash of every render dependency: the base part and every shared collection.
    /// </summary>
    internal static string Compute(
        AppConfig config,
        SiteModel siteModel,
        BuildExecutionMode executionMode = BuildExecutionMode.Production,
        string analyticsRendererContractVersion = AnalyticsRendererContract.Version,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>>? seoAlternates = null,
        string? imageSourceFingerprint = null)
    {
        var snapshot = ComputeSnapshot(
            config,
            siteModel,
            executionMode,
            analyticsRendererContractVersion,
            seoAlternates,
            imageSourceFingerprint);
        return snapshot.Combine(snapshot.BaseHash, reads: null);
    }

    /// <summary>
    /// Splits render dependencies into the base hash (every contributor and the SEO alternates)
    /// and lazily computed fingerprints of the shared collections, including <c>pages</c> when
    /// <paramref name="pagesFingerprint"/> is given.
    /// </summary>
    internal static RenderDependencySnapshot ComputeSnapshot(
        AppConfig config,
        SiteModel siteModel,
        BuildExecutionMode executionMode = BuildExecutionMode.Production,
        string analyticsRendererContractVersion = AnalyticsRendererContract.Version,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>>? seoAlternates = null,
        string? imageSourceFingerprint = null,
        Func<string>? pagesFingerprint = null)
    {
        var context = new RenderDependencyContext(
            config,
            siteModel,
            executionMode,
            analyticsRendererContractVersion,
            imageSourceFingerprint);

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

        var baseHash = HashUtil.ToHexLower(hasher.GetHashAndReset());
        var collections = new Dictionary<string, Lazy<string>>(StringComparer.Ordinal);
        var sharedCollections = SiteModelDataContributor.EnumerateCollections(context)
            .Concat(SiteModelDataContributor.EnumerateRouteMetadataSourceCollections(context));
        foreach (var group in sharedCollections.GroupBy(item => item.Key, StringComparer.Ordinal))
        {
            var writes = group.Select(item => item.Write).ToArray();
            collections[group.Key] = new Lazy<string>(() => HashCollection(writes));
        }

        if (pagesFingerprint is not null)
        {
            collections[RenderReadRecorder.Pages] = new Lazy<string>(pagesFingerprint);
        }

        return new RenderDependencySnapshot(baseHash, collections);
    }

    private static string HashCollection(IReadOnlyList<Action<RenderDependencyHashWriter>> writes)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var writer = new RenderDependencyHashWriter(hasher);
        foreach (var write in writes)
        {
            write(writer);
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

    /// <summary>
    /// Folds a page's BreadcrumbList (names and targets of every node) into its dependency hash,
    /// so renamed or re-parented ancestors re-render the page.
    /// </summary>
    internal static string ComputeForBreadcrumb(string baseHash, BreadcrumbDescriptor descriptor)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var writer = new RenderDependencyHashWriter(hasher);
        writer.AppendUtf8(baseHash);
        foreach (var item in descriptor.Items)
        {
            writer.AppendFramedValue("breadcrumb.name", item.Name);
            writer.AppendFramedValue("breadcrumb.item", item.Item);
        }

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
