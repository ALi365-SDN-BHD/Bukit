using System.Text;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class TaxonomyPlugin : IBukitPlugin, IDerivePagesPlugin, IAfterBuildPlugin
{
    private const string IndexCacheKey = "__taxonomy_index_cache";
    private static readonly AsyncLocal<int> BuildIndexCountForTestsScope = new();

    public string Name => "taxonomy";
    public string Version => "2.5.0";

    public IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        var derived = new List<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>();
        var outputMode = NormalizeOutputMode(context.Config.Taxonomy.OutputMode);
        var itemFields = NormalizeItemFields(context.Config.Taxonomy.ItemFields);
        var pageSize = NormalizePageSize(context.Config.Taxonomy.PageSize);
        SetTaxonomyData(context, itemFields);
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
                var terms = GetOrBuildIndex(context, key, itemFields);
                MergeEnsureTerms(context, kind, terms);
                if (terms.Count == 0)
                {
                    continue;
                }

                var templates = ResolveTemplates(context.Config.Taxonomy, context.LayoutsDir, kind, kindConfig);
                var title = string.IsNullOrWhiteSpace(kindConfig.Title) ? kind : kindConfig.Title.Trim();
                var singularTitlePrefix = string.IsNullOrWhiteSpace(kindConfig.SingularTitlePrefix)
                    ? title
                    : kindConfig.SingularTitlePrefix.Trim();
                var indexEnabled = kindConfig.IndexEnabled ?? context.Config.Taxonomy.IndexEnabled;

                derived.AddRange(CreateKind(baseUrlPrefix, kind, title, singularTitlePrefix, terms, templates.IndexTemplate, templates.TermTemplate, emitContentHtml, pageSize, indexEnabled, context.Config.Site.OutputPathEncoding));
            }

            return derived;
        }

        var tags = GetOrBuildIndex(context, "tags", itemFields);
        var categories = GetOrBuildIndex(context, "categories", itemFields);
        MergeEnsureTerms(context, "tags", tags);
        MergeEnsureTerms(context, "categories", categories);

        if (tags.Count == 0 && categories.Count == 0)
        {
            return derived;
        }

        var prefix = context.BaseUrl == "/" ? string.Empty : context.BaseUrl;

        if (tags.Count > 0)
        {
            var templates = ResolveTemplates(context.Config.Taxonomy, context.LayoutsDir, kind: "tags");
            derived.AddRange(CreateKind(prefix, kind: "tags", title: "Tags", singularTitlePrefix: "Tag", tags, templates.IndexTemplate, templates.TermTemplate, emitContentHtml, pageSize, context.Config.Taxonomy.IndexEnabled, context.Config.Site.OutputPathEncoding));
        }

        if (categories.Count > 0)
        {
            var templates = ResolveTemplates(context.Config.Taxonomy, context.LayoutsDir, kind: "categories");
            derived.AddRange(CreateKind(prefix, kind: "categories", title: "Categories", singularTitlePrefix: "Category", categories, templates.IndexTemplate, templates.TermTemplate, emitContentHtml, pageSize, context.Config.Taxonomy.IndexEnabled, context.Config.Site.OutputPathEncoding));
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
        var outPath = Path.Combine(context.OutputDir, "taxonomy.json");
        Directory.CreateDirectory(context.OutputDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WriteNumber("schema", 1);

        writer.WriteStartArray("kinds");
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
                var terms = GetOrBuildIndex(context, key, itemFields);
                MergeEnsureTerms(context, kind, terms);
                if (terms.Count == 0)
                {
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(kindConfig.Title) ? kind : kindConfig.Title.Trim();
                WriteKind(writer, context.BaseUrl, key, kind, title, terms);
            }
        }
        else
        {
            var tags = GetOrBuildIndex(context, "tags", itemFields);
            MergeEnsureTerms(context, "tags", tags);
            if (tags.Count > 0)
            {
                WriteKind(writer, context.BaseUrl, key: "tags", kind: "tags", title: "Tags", tags);
            }

            var categories = GetOrBuildIndex(context, "categories", itemFields);
            MergeEnsureTerms(context, "categories", categories);
            if (categories.Count > 0)
            {
                WriteKind(writer, context.BaseUrl, key: "categories", kind: "categories", title: "Categories", categories);
            }
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }

    private static Dictionary<string, TaxonomyTerm> GetOrBuildIndex(
        BuildContext context,
        string key,
        IReadOnlyList<string> itemFields)
    {
        Dictionary<string, Dictionary<string, TaxonomyTerm>> cache;
        if (context.Data.TryGetValue(IndexCacheKey, out var cacheObj)
            && cacheObj is Dictionary<string, Dictionary<string, TaxonomyTerm>> existingCache)
        {
            cache = existingCache;
        }
        else
        {
            cache = new Dictionary<string, Dictionary<string, TaxonomyTerm>>(StringComparer.OrdinalIgnoreCase);
            context.Data[IndexCacheKey] = cache;
        }

        var indexKey = $"{key}|{string.Join(",", itemFields)}";
        if (!cache.TryGetValue(indexKey, out var terms))
        {
            terms = BuildIndexCore(context.Routed, key, itemFields, context.Config.Taxonomy);
            cache[indexKey] = terms;
        }

        return terms;
    }

    private static Dictionary<string, TaxonomyTerm> BuildIndexCore(
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        string key,
        IReadOnlyList<string> itemFields,
        TaxonomyConfig config)
    {
        BuildIndexCountForTestsScope.Value++;
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase);

        foreach (var (item, route) in routed)
        {
            var values = GetStringList(item.Meta, key);
            if (values is null || values.Count == 0)
            {
                continue;
            }

            var summary = item.Meta.TryGetValue("summary", out var summaryObj) ? summaryObj?.ToString() : null;
            var extra = ExtractExtraFields(item, itemFields);
            var sourceKey = GetSourceKey(item.Meta);
            var pinField = ResolvePinField(config, sourceKey);
            var pinOrderField = ResolvePinOrderField(config, sourceKey);
            var isPinned = TryGetPinned(item, pinField);
            var pinOrder = TryGetPinOrder(item, pinOrderField);
            if (pinOrder.HasValue)
            {
                isPinned = true;
            }
            foreach (var raw in values)
            {
                var display = (raw ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(display))
                {
                    continue;
                }

                var slug = Slugify(display);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }

                if (!terms.TryGetValue(slug, out var term))
                {
                    term = new TaxonomyTerm(display, slug);
                    terms[slug] = term;
                }

                term.Pages.Add(new TaxonomyPage(item.Title, route.Url, item.PublishAt, summary, extra, isPinned, pinOrder));
            }
        }

        foreach (var term in terms.Values)
        {
            term.Pages.Sort(ComparePages);
        }

        return terms;
    }

    private static IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> CreateKind(
        string baseUrlPrefix,
        string kind,
        string title,
        string singularTitlePrefix,
        Dictionary<string, TaxonomyTerm> terms,
        string indexTemplate,
        string termTemplate,
        bool emitContentHtml,
        int pageSize,
        bool indexEnabled,
        string outputPathEncoding)
    {
        var derived = new List<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>();
        var items = terms.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

        var now = DateTimeOffset.UtcNow;
        if (indexEnabled)
        {
            derived.Add(CreateIndexPage(baseUrlPrefix, kind, title, items, indexTemplate, publishAt: now, emitContentHtml, outputPathEncoding));
        }

        foreach (var term in items)
        {
            if (term.Pages.Count == 0)
            {
                derived.Add(CreateTermPage(
                    baseUrlPrefix,
                    kind,
                    singularTitlePrefix,
                    term,
                    termTemplate,
                    publishAt: now,
                    emitContentHtml,
                    pageSize,
                    page: 1,
                    totalPages: 1,
                    items: Array.Empty<TaxonomyPage>(),
                    outputPathEncoding));
                continue;
            }

            var totalPages = (int)Math.Ceiling(term.Pages.Count / (double)pageSize);
            for (var page = 1; page <= totalPages; page++)
            {
                var skip = (page - 1) * pageSize;
                var chunk = term.Pages.Skip(skip).Take(pageSize).ToList();
                var publishAt = chunk.Count == 0 ? now : chunk.Max(x => x.PublishAt);
                derived.Add(CreateTermPage(
                    baseUrlPrefix,
                    kind,
                    singularTitlePrefix,
                    term,
                    termTemplate,
                    publishAt,
                    emitContentHtml,
                    pageSize,
                    page,
                    totalPages,
                    chunk,
                    outputPathEncoding));
            }
        }

        return derived;
    }

    private static (ContentItem Item, RouteInfo Route, DateTimeOffset LastModified) CreateIndexPage(
        string baseUrlPrefix,
        string kind,
        string title,
        IReadOnlyList<TaxonomyTerm> terms,
        string template,
        DateTimeOffset publishAt,
        bool emitContentHtml,
        string outputPathEncoding)
    {
        var html = string.Empty;
        if (emitContentHtml)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<ul>");
            foreach (var term in terms)
            {
                var href = $"{baseUrlPrefix}/{kind}/{term.Slug}/";
                sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{EscapeHtml(term.DisplayName)}</a> <small>({term.Pages.Count})</small></li>");
            }
            sb.AppendLine("</ul>");
            html = sb.ToString();
        }

        var url = "/" + kind + "/";
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);
        var route = new RouteInfo(url, outputPath, template);
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "page"
        };

        var termsValue = new List<object>(terms.Count);
        foreach (var term in terms)
        {
            termsValue.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = term.DisplayName,
                ["slug"] = term.Slug,
                ["url"] = "/" + kind + "/" + term.Slug + "/",
                ["count"] = term.Pages.Count
            });
        }

        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["terms"] = new ContentField("list", termsValue)
        };

        var item = new ContentItem(
            Id: $"{kind}-index",
            Title: title,
            Slug: kind,
            PublishAt: publishAt,
            ContentHtml: html,
            Meta: meta,
            Fields: fields);

        return (item, route, publishAt);
    }

    private static (ContentItem Item, RouteInfo Route, DateTimeOffset LastModified) CreateTermPage(
        string baseUrlPrefix,
        string kind,
        string singularTitlePrefix,
        TaxonomyTerm term,
        string template,
        DateTimeOffset publishAt,
        bool emitContentHtml,
        int pageSize,
        int page,
        int totalPages,
        IReadOnlyList<TaxonomyPage> items,
        string outputPathEncoding)
    {
        var html = string.Empty;
        if (emitContentHtml)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<ul>");

            foreach (var pageItem in items)
            {
                var href = $"{baseUrlPrefix}{pageItem.Url}";
                sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{EscapeHtml(pageItem.Title)}</a></li>");
            }

            sb.AppendLine("</ul>");
            html = sb.ToString();
        }

        var isFirstPage = page <= 1;
        var url = isFirstPage
            ? "/" + kind + "/" + term.Slug + "/"
            : "/" + kind + "/" + term.Slug + "/page/" + page + "/";
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);
        var route = new RouteInfo(url, outputPath, template);
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "page"
        };

        var itemsValue = new List<object>(items.Count);
        foreach (var pageItem in items)
        {
            var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = pageItem.Title,
                ["url"] = pageItem.Url,
                ["publish_date"] = pageItem.PublishAt.DateTime
            };
            if (!string.IsNullOrWhiteSpace(pageItem.Summary))
            {
                obj["summary"] = pageItem.Summary!;
            }

            if (pageItem.Extra is not null)
            {
                foreach (var kv in pageItem.Extra)
                {
                    if (!obj.ContainsKey(kv.Key))
                    {
                        obj[kv.Key] = kv.Value;
                    }
                }
            }

            itemsValue.Add(obj);
        }

        var taxonomyValue = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = kind,
            ["term"] = term.DisplayName,
            ["slug"] = term.Slug,
            ["count"] = term.Pages.Count
        };

        var paginationValue = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = page,
            ["page_size"] = pageSize,
            ["total"] = term.Pages.Count,
            ["total_pages"] = totalPages,
            ["has_prev"] = page > 1,
            ["has_next"] = page < totalPages
        };

        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["items"] = new ContentField("list", itemsValue),
            ["taxonomy"] = new ContentField("object", taxonomyValue),
            ["pagination"] = new ContentField("object", paginationValue)
        };

        var item = new ContentItem(
            Id: page <= 1 ? $"{kind}-{term.Slug}" : $"{kind}-{term.Slug}-page-{page}",
            Title: page <= 1 ? $"{singularTitlePrefix}: {term.DisplayName}" : $"{singularTitlePrefix}: {term.DisplayName} (Page {page})",
            Slug: term.Slug,
            PublishAt: publishAt,
            ContentHtml: html,
            Meta: meta,
            Fields: fields);

        return (item, route, publishAt);
    }

    private static (string IndexTemplate, string TermTemplate) ResolveTemplates(TaxonomyConfig config, string layoutsDir, string kind, TaxonomyKindConfig? kindConfig = null)
    {
        var legacyKindConfig = kind.Equals("tags", StringComparison.OrdinalIgnoreCase)
            ? config.Templates.Tags
            : (kind.Equals("categories", StringComparison.OrdinalIgnoreCase) ? config.Templates.Categories : new TaxonomyKindTemplateConfig());
        var conventionalIndexTemplate = TemplateCapabilitiesResolver.SupportsTaxonomy(TemplateCapabilitiesResolver.TaxonomyIndexTemplatePath, layoutsDir)
            ? TemplateCapabilitiesResolver.TaxonomyIndexTemplatePath
            : null;
        var conventionalTermTemplate = TemplateCapabilitiesResolver.SupportsTaxonomy(TemplateCapabilitiesResolver.TaxonomyTermTemplatePath, layoutsDir)
            ? TemplateCapabilitiesResolver.TaxonomyTermTemplatePath
            : null;

        var baseTemplate = string.IsNullOrWhiteSpace(config.Template) ? "pages/page.html" : config.Template;
        var kindBaseTemplate = FirstNonEmpty(kindConfig?.Template, legacyKindConfig.Template, baseTemplate) ?? "pages/page.html";
        var indexTemplate = FirstNonEmpty(kindConfig?.IndexTemplate, legacyKindConfig.IndexTemplate, config.IndexTemplate, conventionalIndexTemplate, kindBaseTemplate)
            ?? kindBaseTemplate;
        var termTemplate = FirstNonEmpty(kindConfig?.TermTemplate, legacyKindConfig.TermTemplate, config.TermTemplate, conventionalTermTemplate, kindBaseTemplate)
            ?? kindBaseTemplate;

        indexTemplate = EnsureTemplateExists(indexTemplate, layoutsDir, "pages/page.html");
        termTemplate = EnsureTemplateExists(termTemplate, layoutsDir, "pages/page.html");

        return (indexTemplate, termTemplate);
    }

    private static string EnsureTemplateExists(string template, string layoutsDir, string fallback)
    {
        var fullPath = Path.Combine(layoutsDir, template.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(fullPath) ? template : fallback;
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c))
            {
                return c!.Trim();
            }
        }

        return null;
    }

    private static string NormalizeOutputMode(string? mode)
    {
        var m = (mode ?? "both").Trim().ToLowerInvariant();
        return m switch
        {
            "both" or "pages" or "data" or "fields_only" => m,
            "fields-only" => "fields_only",
            _ => "both"
        };
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize <= 0 ? 10 : pageSize;
    }

    private static IReadOnlyList<string> NormalizeItemFields(IReadOnlyList<string>? fields)
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

    private static IReadOnlyDictionary<string, object>? ExtractExtraFields(ContentItem item, IReadOnlyList<string> itemFields)
    {
        if (itemFields.Count == 0)
        {
            return null;
        }

        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in itemFields)
        {
            if (TryGetItemValue(item, key, out var value))
            {
                dict[key] = value!;
                continue;
            }

            if (key.Equals("date", StringComparison.OrdinalIgnoreCase))
            {
                dict["date"] = item.PublishAt.UtcDateTime.ToString("yyyy-MM-dd");
            }
        }

        return dict.Count == 0 ? null : dict;
    }

    private static bool TryGetItemValue(ContentItem item, string key, out object? value)
    {
        value = null;

        if (item.Meta.TryGetValue(key, out var metaValue) && metaValue is not null)
        {
            if (metaValue is string s)
            {
                var trimmed = s.Trim();
                if (trimmed.Length == 0)
                {
                    return false;
                }

                value = trimmed;
                return true;
            }

            value = metaValue;
            return true;
        }

        if (item.Fields is not null && item.Fields.TryGetValue(key, out var field) && field.Value is not null)
        {
            value = field.Value;
            return true;
        }

        return false;
    }

    private static void WriteExtraJson(Utf8JsonWriter writer, IReadOnlyDictionary<string, object>? extra)
    {
        if (extra is null || extra.Count == 0)
        {
            return;
        }

        foreach (var kv in extra)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value is null)
            {
                continue;
            }

            if (kv.Key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("url", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("publishAt", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("publish_date", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("summary", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            WriteJsonValue(writer, kv.Key, kv.Value);
        }
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string name, object value)
    {
        switch (value)
        {
            case string s:
                writer.WriteString(name, s);
                return;
            case bool b:
                writer.WriteBoolean(name, b);
                return;
            case int i:
                writer.WriteNumber(name, i);
                return;
            case long l:
                writer.WriteNumber(name, l);
                return;
            case double d:
                writer.WriteNumber(name, d);
                return;
            case decimal m:
                writer.WriteNumber(name, m);
                return;
            case DateTime dt:
                writer.WriteString(name, dt.ToString("O"));
                return;
            case DateTimeOffset dto:
                writer.WriteString(name, dto.ToString("O"));
                return;
            case IEnumerable<object> seq:
                writer.WriteStartArray(name);
                foreach (var x in seq)
                {
                    if (x is null)
                    {
                        continue;
                    }

                    writer.WriteStringValue(x.ToString());
                }
                writer.WriteEndArray();
                return;
            default:
                writer.WriteString(name, value.ToString());
                return;
        }
    }

    private static void SetTaxonomyData(BuildContext context, IReadOnlyList<string> itemFields)
    {
        var taxonomy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

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
                var terms = GetOrBuildIndex(context, key, itemFields);
                MergeEnsureTerms(context, kind, terms);
                if (terms.Count == 0)
                {
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(kindConfig.Title) ? kind : kindConfig.Title.Trim();
                taxonomy[kind] = BuildKindData(key, kind, title, terms);
            }
        }
        else
        {
            var tags = GetOrBuildIndex(context, "tags", itemFields);
            MergeEnsureTerms(context, "tags", tags);
            if (tags.Count > 0)
            {
                taxonomy["tags"] = BuildKindData(key: "tags", kind: "tags", title: "Tags", tags);
            }

            var categories = GetOrBuildIndex(context, "categories", itemFields);
            MergeEnsureTerms(context, "categories", categories);
            if (categories.Count > 0)
            {
                taxonomy["categories"] = BuildKindData(key: "categories", kind: "categories", title: "Categories", categories);
            }
        }

        if (taxonomy.Count > 0)
        {
            context.Data["taxonomy"] = taxonomy;
        }
    }

    internal static int BuildIndexCountForTests => BuildIndexCountForTestsScope.Value;

    internal static void ResetBuildIndexCountForTests()
    {
        BuildIndexCountForTestsScope.Value = 0;
    }

    private static int ComparePages(TaxonomyPage a, TaxonomyPage b)
    {
        if (a.IsPinned && !b.IsPinned)
        {
            return -1;
        }

        if (!a.IsPinned && b.IsPinned)
        {
            return 1;
        }

        if (a.IsPinned && b.IsPinned)
        {
            var cmp = ComparePinOrder(a.PinOrder, b.PinOrder);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        var publishAtCmp = b.PublishAt.CompareTo(a.PublishAt);
        if (publishAtCmp != 0)
        {
            return publishAtCmp;
        }

        var titleCmp = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        if (titleCmp != 0)
        {
            return titleCmp;
        }

        return string.Compare(a.Url, b.Url, StringComparison.OrdinalIgnoreCase);
    }

    private static int ComparePinOrder(int? a, int? b)
    {
        if (a.HasValue && !b.HasValue)
        {
            return -1;
        }

        if (!a.HasValue && b.HasValue)
        {
            return 1;
        }

        if (a.HasValue && b.HasValue)
        {
            return a.Value.CompareTo(b.Value);
        }

        return 0;
    }

    private static string? GetSourceKey(IReadOnlyDictionary<string, object> meta)
    {
        if (!meta.TryGetValue("sourceKey", out var obj) || obj is null)
        {
            return null;
        }

        var text = obj.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string ResolvePinField(TaxonomyConfig config, string? sourceKey)
    {
        if (!string.IsNullOrWhiteSpace(sourceKey) &&
            config.PinFieldBySource is not null &&
            config.PinFieldBySource.TryGetValue(sourceKey, out var field) &&
            !string.IsNullOrWhiteSpace(field))
        {
            return field.Trim();
        }

        return string.IsNullOrWhiteSpace(config.PinField) ? "pinned" : config.PinField.Trim();
    }

    private static string? ResolvePinOrderField(TaxonomyConfig config, string? sourceKey)
    {
        if (!string.IsNullOrWhiteSpace(sourceKey) &&
            config.PinOrderFieldBySource is not null &&
            config.PinOrderFieldBySource.TryGetValue(sourceKey, out var field) &&
            !string.IsNullOrWhiteSpace(field))
        {
            return field.Trim();
        }

        return string.IsNullOrWhiteSpace(config.PinOrderField) ? null : config.PinOrderField.Trim();
    }

    private static bool TryGetPinned(ContentItem item, string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return false;
        }

        if (!TryGetItemValue(item, field, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            decimal m => m != 0,
            string s => ParseBoolLike(s),
            _ => ParseBoolLike(value.ToString() ?? string.Empty)
        };
    }

    private static int? TryGetPinOrder(ContentItem item, string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        if (!TryGetItemValue(item, field, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int i => i,
            long l => l is > int.MaxValue or < int.MinValue ? null : (int)l,
            double d => double.IsNaN(d) || double.IsInfinity(d) ? null : (int)Math.Round(d),
            decimal m => m is > int.MaxValue or < int.MinValue ? null : (int)m,
            string s => int.TryParse(s.Trim(), out var i) ? i : null,
            _ => int.TryParse(value.ToString(), out var i) ? i : null
        };
    }

    private static bool ParseBoolLike(string raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return false;
        }

        if (bool.TryParse(s, out var b))
        {
            return b;
        }

        if (s.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (s.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (s.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (s.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, object> BuildKindData(
        string key,
        string kind,
        string title,
        Dictionary<string, TaxonomyTerm> terms)
    {
        var termsValue = new List<object>();
        var itemsByTerm = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in terms.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            termsValue.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = term.DisplayName,
                ["slug"] = term.Slug,
                ["url"] = "/" + kind + "/" + term.Slug + "/",
                ["count"] = term.Pages.Count
            });

            var itemsValue = new List<object>(term.Pages.Count);
            foreach (var page in term.Pages)
            {
                var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = page.Title,
                    ["url"] = page.Url,
                    ["publish_date"] = page.PublishAt.DateTime
                };
                if (!string.IsNullOrWhiteSpace(page.Summary))
                {
                    obj["summary"] = page.Summary!;
                }

                if (page.Extra is not null)
                {
                    foreach (var kv in page.Extra)
                    {
                        if (!obj.ContainsKey(kv.Key))
                        {
                            obj[kv.Key] = kv.Value;
                        }
                    }
                }

                itemsValue.Add(obj);
            }

            itemsByTerm[term.Slug] = itemsValue;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["key"] = key,
            ["kind"] = kind,
            ["title"] = title,
            ["terms"] = termsValue,
            ["items_by_term"] = itemsByTerm
        };
    }

    private static void WriteKind(
        Utf8JsonWriter writer,
        string baseUrl,
        string key,
        string kind,
        string title,
        Dictionary<string, TaxonomyTerm> terms)
    {
        writer.WriteStartObject();
        writer.WriteString("key", key);
        writer.WriteString("kind", kind);
        writer.WriteString("title", title);

        writer.WriteStartArray("terms");
        foreach (var term in terms.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartObject();
            writer.WriteString("title", term.DisplayName);
            writer.WriteString("slug", term.Slug);
            writer.WriteString("url", NormalizeUrl(baseUrl, "/" + kind + "/" + term.Slug + "/"));
            writer.WriteNumber("count", term.Pages.Count);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteStartObject("itemsByTerm");
        foreach (var term in terms.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartArray(term.Slug);
            foreach (var page in term.Pages)
            {
                writer.WriteStartObject();
                writer.WriteString("title", page.Title);
                writer.WriteString("url", NormalizeUrl(baseUrl, page.Url));
                writer.WriteString("publishAt", page.PublishAt.ToString("O"));
                if (!string.IsNullOrWhiteSpace(page.Summary))
                {
                    writer.WriteString("summary", page.Summary);
                }
                WriteExtraJson(writer, page.Extra);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static string NormalizeUrl(string baseUrl, string url)
    {
        var u = url.StartsWith('/') ? url : "/" + url;
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl == "/")
        {
            return u;
        }

        var b = baseUrl.StartsWith('/') ? baseUrl : "/" + baseUrl;
        if (b.Length > 1 && b.EndsWith('/'))
        {
            b = b.TrimEnd('/');
        }

        return b + u;
    }

    private static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var v) || v is null)
        {
            return null;
        }

        if (v is string s)
        {
            var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (v is IEnumerable<object> seq)
        {
            var list = seq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return list.Count == 0 ? null : list;
        }

        return null;
    }

    private static void MergeEnsureTerms(BuildContext context, string kind, Dictionary<string, TaxonomyTerm> terms)
    {
        if (!context.Data.TryGetValue("taxonomy_ensure_terms", out var obj) || obj is null)
        {
            return;
        }

        if (obj is not Dictionary<string, List<Dictionary<string, object>>> map)
        {
            return;
        }

        if (!map.TryGetValue(kind, out var list) || list is null || list.Count == 0)
        {
            return;
        }

        foreach (var termObj in list)
        {
            if (termObj is null)
            {
                continue;
            }

            var title = termObj.TryGetValue("title", out var t) && t is not null ? (t.ToString() ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var slug = termObj.TryGetValue("slug", out var s) && s is not null ? (s.ToString() ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = Slugify(title);
            }

            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }

            if (slug.Contains("..") || Path.IsPathRooted(slug) || slug.Contains('/') || slug.Contains('\\'))
            {
                slug = Slugify(slug);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }
            }

            if (!terms.ContainsKey(slug))
            {
                terms[slug] = new TaxonomyTerm(title, slug);
            }
        }
    }

    private static string Slugify(string text)
    {
        var trimmed = text.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(trimmed.Length);
        var dash = false;

        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                dash = false;
                continue;
            }

            if (ch is ' ' or '-' or '_' or '.')
            {
                if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
            }
        }

        var s = sb.ToString().Trim('-');
        return s;
    }

    private static string EscapeHtml(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    private static string EscapeAttr(string value)
    {
        return EscapeHtml(value);
    }

    private sealed class TaxonomyTerm
    {
        public TaxonomyTerm(string displayName, string slug)
        {
            DisplayName = displayName;
            Slug = slug;
        }

        public string DisplayName { get; }
        public string Slug { get; }
        public List<TaxonomyPage> Pages { get; } = new();
    }

    private sealed record TaxonomyPage(string Title, string Url, DateTimeOffset PublishAt, string? Summary, IReadOnlyDictionary<string, object>? Extra, bool IsPinned, int? PinOrder);
}
