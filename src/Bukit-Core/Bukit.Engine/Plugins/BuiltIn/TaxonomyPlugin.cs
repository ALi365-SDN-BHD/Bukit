using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class TaxonomyPlugin : IBukitPlugin, IDerivePagesPlugin, IAfterBuildPlugin, ITemplateRequirementPlugin
{
    internal const string IndexCacheKey = "__taxonomy_index_cache";
    internal static readonly AsyncLocal<int> BuildIndexCountForTestsScope = new();

    public string Name => "taxonomy";
    public string Version => "3.0.0";

    public IReadOnlyList<string> GetTemplateRequirementKinds(BuildContext context)
    {
        var outputMode = NormalizeOutputMode(context.Config.Taxonomy.OutputMode);
        if (outputMode == "data")
        {
            return Array.Empty<string>();
        }

        var itemFields = NormalizeItemFields(context.Config.Taxonomy.ItemFields);
        var requirements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (context.Config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            foreach (var kindConfig in kinds)
            {
                var key = (kindConfig.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var kind = string.IsNullOrWhiteSpace(kindConfig.Kind) ? key : kindConfig.Kind.Trim();
                var terms = TaxonomyIndexBuilder.GetOrBuildIndex(context, key, itemFields);
                TaxonomyIndexBuilder.MergeEnsureTerms(context, kind, terms);
                if (terms.Count == 0)
                {
                    continue;
                }

                AddTemplateRequirements(context.Config.Taxonomy, kind, kindConfig, requirements);
            }

            return requirements.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var tags = TaxonomyIndexBuilder.GetOrBuildIndex(context, "tags", itemFields);
        TaxonomyIndexBuilder.MergeEnsureTerms(context, "tags", tags);
        if (tags.Count > 0)
        {
            AddTemplateRequirements(context.Config.Taxonomy, "tags", null, requirements);
        }

        var categories = TaxonomyIndexBuilder.GetOrBuildIndex(context, "categories", itemFields);
        TaxonomyIndexBuilder.MergeEnsureTerms(context, "categories", categories);
        if (categories.Count > 0)
        {
            AddTemplateRequirements(context.Config.Taxonomy, "categories", null, requirements);
        }

        return requirements.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context)
    {
        var derived = new List<RoutedContentDocument>();
        var outputMode = NormalizeOutputMode(context.Config.Taxonomy.OutputMode);
        var itemFields = NormalizeItemFields(context.Config.Taxonomy.ItemFields);
        var pageSize = NormalizePageSize(context.Config.Taxonomy.PageSize);
        TaxonomyDataWriter.SetTaxonomyData(context, itemFields);
        if (outputMode == "data")
        {
            return derived;
        }

        var emitContentHtml = outputMode != "fields_only";

        if (context.Config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            var baseUrlPrefix = context.BaseUrl == "/" ? string.Empty : context.BaseUrl;
            foreach (var kindConfig in kinds)
            {
                var key = (kindConfig.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var kind = string.IsNullOrWhiteSpace(kindConfig.Kind) ? key : kindConfig.Kind.Trim();
                var terms = TaxonomyIndexBuilder.GetOrBuildIndex(context, key, itemFields);
                TaxonomyIndexBuilder.MergeEnsureTerms(context, kind, terms);
                TaxonomyMetadataLoader.LoadAndEnrich(context, kind, terms);
                if (terms.Count == 0)
                {
                    continue;
                }

                var templates = TaxonomyTemplateResolver.ResolveTemplates(context.Config.Taxonomy, context.LayoutsDir, kind, context.ResolveTemplateKind, kindConfig);
                var title = string.IsNullOrWhiteSpace(kindConfig.Title) ? kind : kindConfig.Title.Trim();
                var description = string.IsNullOrWhiteSpace(kindConfig.Description) ? null : kindConfig.Description.Trim();
                var singularTitlePrefix = string.IsNullOrWhiteSpace(kindConfig.SingularTitlePrefix)
                    ? title
                    : kindConfig.SingularTitlePrefix.Trim();
                var indexEnabled = kindConfig.IndexEnabled ?? context.Config.Taxonomy.IndexEnabled;

                derived.AddRange(TaxonomyPageCreator.CreateKind(baseUrlPrefix, kind, kindConfig.RoutePrefix, title, description, singularTitlePrefix, terms, templates.IndexTemplate, templates.TermTemplate, emitContentHtml, pageSize, indexEnabled, kindConfig.Hierarchical, context.Config.Site.OutputPathEncoding));
            }

            return derived;
        }

        var tags = TaxonomyIndexBuilder.GetOrBuildIndex(context, "tags", itemFields);
        var categories = TaxonomyIndexBuilder.GetOrBuildIndex(context, "categories", itemFields);
        TaxonomyIndexBuilder.MergeEnsureTerms(context, "tags", tags);
        TaxonomyIndexBuilder.MergeEnsureTerms(context, "categories", categories);
        TaxonomyMetadataLoader.LoadAndEnrich(context, "tags", tags);
        TaxonomyMetadataLoader.LoadAndEnrich(context, "categories", categories);

        if (tags.Count == 0 && categories.Count == 0)
        {
            return derived;
        }

        var prefix = context.BaseUrl == "/" ? string.Empty : context.BaseUrl;

        if (tags.Count > 0)
        {
            var templates = TaxonomyTemplateResolver.ResolveTemplates(context.Config.Taxonomy, context.LayoutsDir, kind: "tags", context.ResolveTemplateKind);
            derived.AddRange(TaxonomyPageCreator.CreateKind(prefix, kind: "tags", routePrefix: null, title: "Tags", description: null, singularTitlePrefix: "Tag", tags, templates.IndexTemplate, templates.TermTemplate, emitContentHtml, pageSize, context.Config.Taxonomy.IndexEnabled, false, context.Config.Site.OutputPathEncoding));
        }

        if (categories.Count > 0)
        {
            var templates = TaxonomyTemplateResolver.ResolveTemplates(context.Config.Taxonomy, context.LayoutsDir, kind: "categories", context.ResolveTemplateKind);
            derived.AddRange(TaxonomyPageCreator.CreateKind(prefix, kind: "categories", routePrefix: null, title: "Categories", description: null, singularTitlePrefix: "Category", categories, templates.IndexTemplate, templates.TermTemplate, emitContentHtml, pageSize, context.Config.Taxonomy.IndexEnabled, false, context.Config.Site.OutputPathEncoding));
        }

        return derived;
    }

    public void AfterBuild(BuildContext context)
    {
        var outputMode = NormalizeOutputMode(context.Config.Taxonomy.OutputMode);
        if (outputMode is not ("both" or "data"))
        {
            return;
        }

        var itemFields = NormalizeItemFields(context.Config.Taxonomy.ItemFields);

        var kindTerms = new List<(string Key, string Kind, string Title, Dictionary<string, TaxonomyTerm> Terms, string? RoutePrefix)>();

        if (context.Config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            foreach (var kindConfig in kinds)
            {
                var key = (kindConfig.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var kind = string.IsNullOrWhiteSpace(kindConfig.Kind) ? key : kindConfig.Kind.Trim();
                var terms = TaxonomyIndexBuilder.GetOrBuildIndex(context, key, itemFields);
                TaxonomyIndexBuilder.MergeEnsureTerms(context, kind, terms);
                TaxonomyMetadataLoader.LoadAndEnrich(context, kind, terms);
                if (terms.Count == 0)
                {
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(kindConfig.Title) ? kind : kindConfig.Title.Trim();
                kindTerms.Add((key, kind, title, terms, kindConfig.RoutePrefix));
            }
        }
        else
        {
            var tags = TaxonomyIndexBuilder.GetOrBuildIndex(context, "tags", itemFields);
            TaxonomyIndexBuilder.MergeEnsureTerms(context, "tags", tags);
            TaxonomyMetadataLoader.LoadAndEnrich(context, "tags", tags);
            if (tags.Count > 0)
            {
                kindTerms.Add(("tags", "tags", "Tags", tags, null));
            }

            var categories = TaxonomyIndexBuilder.GetOrBuildIndex(context, "categories", itemFields);
            TaxonomyIndexBuilder.MergeEnsureTerms(context, "categories", categories);
            TaxonomyMetadataLoader.LoadAndEnrich(context, "categories", categories);
            if (categories.Count > 0)
            {
                kindTerms.Add(("categories", "categories", "Categories", categories, null));
            }
        }

        var outPath = Path.Combine(context.OutputDir, "taxonomy.json");
        Directory.CreateDirectory(context.OutputDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WriteNumber("schema", 2);

        writer.WriteStartArray("kinds");
        foreach (var (key, kind, title, terms, routePrefix) in kindTerms)
        {
            TaxonomyDataWriter.WriteKind(writer, context.BaseUrl, key, kind, title, terms, routePrefix);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        foreach (var (_, kind, _, terms, routePrefix) in kindTerms)
        {
            TaxonomyFeedWriter.WriteFeeds(context.OutputDir, context.Config.Site.Url ?? string.Empty, context.BaseUrl, context.Config.Site.Title, terms, kind, routePrefix);
        }

        foreach (var (_, kind, _, terms, routePrefix) in kindTerms)
        {
            TaxonomyRedirectWriter.WriteRedirects(context.OutputDir, kind, terms, routePrefix);
        }
    }

    internal static string NormalizeOutputMode(string? mode)
    {
        var m = (mode ?? "both").Trim().ToLowerInvariant();
        return m switch
        {
            "both" or "pages" or "data" or "fields_only" => m,
            "fields-only" => "fields_only",
            _ => "both"
        };
    }

    internal static int NormalizePageSize(int pageSize)
    {
        return pageSize <= 0 ? 10 : pageSize;
    }

    internal static IReadOnlyList<string> NormalizeItemFields(IReadOnlyList<string>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var f in fields)
        {
            var key = (f ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (seen.Add(key))
            {
                list.Add(key);
            }
        }

        return list;
    }

    private static void AddTemplateRequirements(
        TaxonomyConfig config,
        string kind,
        TaxonomyKindConfig? kindConfig,
        ISet<string> requirements)
    {
        var kindBaseTemplate = TaxonomyTemplateResolver.FirstNonEmpty(kindConfig?.Template);
        var indexTemplate = TaxonomyTemplateResolver.FirstNonEmpty(kindConfig?.IndexTemplate, kindBaseTemplate);
        var termTemplate = TaxonomyTemplateResolver.FirstNonEmpty(kindConfig?.TermTemplate, kindBaseTemplate);

        if (string.IsNullOrWhiteSpace(indexTemplate))
        {
            requirements.Add("taxonomy_index");
        }

        if (string.IsNullOrWhiteSpace(termTemplate))
        {
            requirements.Add("taxonomy_term");
        }
    }

    internal static int BuildIndexCountForTests => BuildIndexCountForTestsScope.Value;

    internal static void ResetBuildIndexCountForTests()
    {
        BuildIndexCountForTestsScope.Value = 0;
    }
}

internal sealed class TaxonomyTerm
{
    public TaxonomyTerm(string displayName, string slug)
    {
        DisplayName = displayName;
        Slug = slug;
    }

    public string DisplayName { get; }
    public string Slug { get; }
    public string? Description { get; init; }
    public string? Image { get; init; }
    public int Weight { get; init; }
    public bool IsVisible { get; init; } = true;
    public string? ParentSlug { get; init; }
    public IReadOnlyList<string>? Aliases { get; init; }
    public List<TaxonomyPage> Pages { get; init; } = new();
}

internal sealed record TaxonomyPage(string Id, string Title, string Url, DateTimeOffset PublishAt, string? Summary, IReadOnlyDictionary<string, object>? Extra, bool IsPinned, int? PinOrder);
