using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Rendering;

namespace Bukit.Engine;

/// <summary>
/// The single catalog of <see cref="BuildContext"/> shared-data keys used by Core.
/// Each key lists who writes it and who reads it, so plugin data dependencies stay traceable.
/// Names without a <c>__</c> prefix are also exposed to templates as <c>site.data.&lt;name&gt;</c>;
/// <see cref="DataFiles"/> and <see cref="RelatedPages"/> are exposed as <c>site.data_files</c> and <c>site.related_pages</c>.
/// Renaming a key changes template output and incremental build hashes.
/// </summary>
internal static class BuildDataKeys
{
    /// <summary>Writer: VariantRouteStage, SiteEngine, VariantBuildPipeline.ListRoutes, SeoAlternatesService. Readers: PaginationPlugin, SitemapPlugin, SearchIndexPlugin.</summary>
    internal static readonly BuildDataKey<ListRouteGraph> ListRouteGraph = new("__list_route_graph");

    /// <summary>Writer: VariantRouteStage. Reader: ImageProcessingPlugin (HTML transform).</summary>
    internal static readonly BuildDataKey<string> MediaDownloadDir = new("__media_download_dir");

    /// <summary>Writer: PluginPipeline (from the previous build manifest). Reader: ImageProcessingPlugin.</summary>
    internal static readonly BuildDataKey<HashSet<PluginOutputTrackingInfo>> PriorPluginOutputs = new("__prior_plugin_outputs");

    /// <summary>Writer: PluginPipeline. Reader: ImageProcessingPlugin.</summary>
    internal static readonly BuildDataKey<IReadOnlyList<string>> CurrentHtmlOutputs = new("__current_html_outputs");

    /// <summary>Writer: VariantSeoStage. Readers: LlmsTxtPlugin, SearchIndexPlugin.</summary>
    internal static readonly BuildDataKey<IReadOnlyDictionary<string, SeoModel>> SeoModels = new("__seo_models");

    /// <summary>Writers: ImageProcessingPlugin, TaxonomyPlugin (after-build). Readers: BuildManifestTracker, VariantReportStage.</summary>
    internal static readonly BuildDataKey<HashSet<PluginOutputTrackingInfo>> PluginOutputs = new("__plugin_outputs");

    /// <summary>Writer and reader: CollectionRouteIndex (per-build cache).</summary>
    internal static readonly BuildDataKey<CollectionRouteIndex> CollectionRouteIndex = new("__collection_route_index");

    /// <summary>Writer: ImageProcessingPlugin. No production reader.</summary>
    internal static readonly BuildDataKey<Dictionary<string, object>> ImageSrcsets = new("__image_srcsets");

    /// <summary>Writer: DataFilesPlugin. Reader: templates as <c>site.data_files</c>.</summary>
    internal static readonly BuildDataKey<Dictionary<string, object>> DataFiles = new("__data_files");

    /// <summary>Writer: RelatedContentPlugin. Reader: templates as <c>site.related_pages</c>.</summary>
    internal static readonly BuildDataKey<Dictionary<string, List<object>>> RelatedPages = new("__related_pages");

    /// <summary>Writer: PagesIndexPlugin. Reader: templates as <c>site.data.pages_by_id</c>.</summary>
    internal static readonly BuildDataKey<Dictionary<string, object>> PagesById = new("pages_by_id");

    /// <summary>Writer: TaxonomyDataWriter. Reader: templates as <c>site.data.taxonomy</c>.</summary>
    internal static readonly BuildDataKey<Dictionary<string, object>> Taxonomy = new("taxonomy");

    /// <summary>Writer: MenuPlugin. Reader: templates as <c>site.data.menus</c>.</summary>
    internal static readonly BuildDataKey<IReadOnlyDictionary<string, object>> Menus = new("menus");

    /// <summary>
    /// Writer: TaxonomyTermsInjector (data documents and Notion options). Readers: TaxonomyIndexBuilder, TaxonomyMetadataLoader.
    /// Also visible to templates as <c>site.data.taxonomy_ensure_terms</c> (existing behavior).
    /// </summary>
    internal static readonly BuildDataKey<Dictionary<string, List<Dictionary<string, object>>>> TaxonomyEnsureTerms = new("taxonomy_ensure_terms");
}
