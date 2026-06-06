using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using System.Text;
using System.Text.Json;
using Bukit.Shared;
using Bukit.Shared.Notion;
namespace Bukit.Content.Notion;

public sealed class NotionContentProvider : IContentProvider, IRawContentProvider
{
    private readonly NotionProviderOptions _options;
    private readonly ILogger? _logger;
    private readonly Func<NotionApiClient> _clientFactory;

    public NotionContentProvider(NotionProviderOptions options, ILogger? logger = null)
        : this(options, logger, () => new NotionApiClient(options))
    {
    }

    internal NotionContentProvider(NotionProviderOptions options, ILogger? logger, Func<NotionApiClient> clientFactory)
    {
        _options = options;
        _logger = logger;
        _clientFactory = clientFactory;
    }

    public async Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DatabaseId))
        {
            throw new ContentException("Notion DatabaseId is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            throw new ContentException("Notion Token is required.");
        }

        using var client = _clientFactory();

        var drafts = new List<PageDraft>();
        var maxItems = _options.MaxItems is > 0 ? _options.MaxItems : null;
        var policyMode = NotionFieldHelper.NormalizePolicyMode(_options.FieldPolicyMode);
        var allowed = policyMode == "whitelist" ? NotionFieldHelper.BuildAllowedSet(_options.AllowedFields) : null;
        var resolvedProperties = await NotionDatabaseSchemaResolver.ResolveAsync(client, _options, cancellationToken);
        string? startCursor = null;
        var pageHtmlCache = NotionCacheManager.CreatePageHtmlCache(_options);
        var relationTargetCache = NotionRelationTargetCache.Create(_options.CacheMode, _options.CacheDir);

        try
        {
            while (true)
            {
                var query = NotionDatabaseQueryBuilder.Build(
                    _options,
                    startCursor,
                    resolvedProperties.FilterProperty,
                    resolvedProperties.SortProperty,
                    resolvedProperties.IncludeSlugProperty);
                using var doc = await client.PostAsync(NotionApiUrls.DatabaseQuery(_options.DatabaseId), query, cancellationToken);
                var root = doc.RootElement;

                if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                {
                    throw new ContentException("Notion query response missing results.");
                }

                var hitMax = false;
                foreach (var page in results.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (maxItems is not null && drafts.Count >= maxItems.Value)
                    {
                        hitMax = true;
                        break;
                    }

                    var pageId = GetString(page, "id");
                    if (string.IsNullOrWhiteSpace(pageId))
                    {
                        continue;
                    }

                    var props = page.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
                    EnsureNoCaseInsensitiveConflicts(props, pageId);
                    var pm = _options.PropertyMap;
                    var title = NotionPropertyParser.ExtractTitle(props, pm) ?? pageId;
                    var slug = NotionPropertyParser.ExtractSlug(props, pm) ?? Slugify(title) ?? pageId.Replace("-", string.Empty, StringComparison.Ordinal);
                    var type = NotionPropertyParser.ExtractType(props, pm);
                    var publishAt = NotionPropertyParser.ExtractPublishAt(props, pm) ?? DateTimeOffset.UtcNow;

                    var lastEditedTime = GetString(page, "last_edited_time");

                    var fields = NotionPropertyParser.ExtractFields(props, policyMode, allowed, out var relationKeys);
                    fields = NotionFieldHelper.InjectPageCoverAndIcon(fields, page);
                    var mutableFields = new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase)
                    {
                        ["source"] = new("text", "notion"),
                        ["notionPageId"] = new("text", pageId),
                        ["bodyFingerprint"] = new("text", string.IsNullOrWhiteSpace(lastEditedTime) ? pageId : lastEditedTime)
                    };
                    if (!string.IsNullOrWhiteSpace(type))
                    {
                        mutableFields["type"] = new ContentField("text", type);
                    }

                    PromoteTextFieldAlias(mutableFields, NotionPropertyParser.NormalizeFieldKey(pm?.Language ?? "language"), "language");
                    PromoteTextFieldAlias(mutableFields, NotionPropertyParser.NormalizeFieldKey(pm?.I18nKey ?? "i18n_key"), "i18nKey");
                    PromoteTextFieldAlias(mutableFields, "i18nkey", "i18nKey");
                    PromoteTextFieldAlias(mutableFields, "url", "url");
                    PromoteTextFieldAlias(mutableFields, "outputpath", "outputPath");
                    PromoteTextFieldAlias(mutableFields, "template", "template");
                    PromoteTextFieldAlias(mutableFields, NotionPropertyParser.NormalizeFieldKey(pm?.Summary ?? "summary"), "summary");
                    PromoteTextFieldAlias(mutableFields, NotionPropertyParser.NormalizeFieldKey(pm?.Collection ?? "collection"), "collection");
                    NormalizeTaxonomyField(mutableFields, "tags");
                    NormalizeTaxonomyField(mutableFields, "categories");
                    NotionPropertyParser.ExtractSeoFields(mutableFields, props, pm);

                    drafts.Add(new PageDraft(pageId, title, slug, type ?? string.Empty, publishAt, lastEditedTime, mutableFields, relationKeys));
                }

                if (hitMax)
                {
                    break;
                }

                if (root.TryGetProperty("has_more", out var hasMoreEl) && hasMoreEl.ValueKind == JsonValueKind.True)
                {
                    startCursor = GetString(root, "next_cursor");
                    if (string.IsNullOrWhiteSpace(startCursor))
                    {
                        break;
                    }

                    continue;
                }

                break;
            }
        }
        finally
        {
            var stats = client.GetStats();
            _logger?.Info($"event=notion.stats requests={stats.RequestCount} throttle_wait_count={stats.ThrottleWaitCount} throttle_wait_ms={stats.ThrottleWaitTotalMs}");
        }

        var targets = drafts.Select(d =>
        {
            var url = ContentFieldReader.GetText(d.Fields, "url");
            url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
            return new RelationTargetInfo(d.PageId, d.Title, d.Slug, d.Type, url);
        });
        var draftIndex = NotionDraftIndex<PageDraft>.From(drafts, static d => d.PageId);
        var pageIndex = NotionRelationLinkBuilder.BuildIndex(targets);
        pageIndex = await NotionRelationResolver.ResolveMissingTaxonomyRelationTargetsAsync(client, drafts, pageIndex, relationTargetCache, _options.RenderConcurrency ?? 0, _logger, cancellationToken);
        var items = new List<ContentItem>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            var fields = d.Fields;
            if (d.RelationKeys is { Count: > 0 })
            {
                fields = NotionRelationLinkBuilder.EnrichFields(fields, d.RelationKeys, pageIndex);
            }

            fields = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "tags");
            fields = NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(fields, "categories");

            items.Add(new ContentItem(
                Id: d.PageId,
                Title: d.Title,
                Slug: d.Slug,
                PublishAt: d.PublishAt,
                ContentHtml: null,
                Fields: fields,
                BodyKey: d.PageId
            ));
        }

        return new ContentLoadResult(items, new NotionBodyStore(async (item, ct) =>
        {
            if (!_options.RenderContent)
            {
                return string.Empty;
            }

            var draft = draftIndex.GetRequired(item.BodyKey ?? item.Id);

            using var bodyClient = _clientFactory();
            var renderer = new NotionBlocksRenderer(bodyClient);
            var html = await NotionCacheManager.GetOrRenderPageHtmlAsync(renderer, pageHtmlCache, draft.PageId, draft.LastEditedTime, ct, _logger);

            if (string.IsNullOrWhiteSpace(ContentFieldReader.GetText(item.Fields, "summary")) &&
                _options.AutoSummary &&
                !string.IsNullOrWhiteSpace(html))
            {
                var extracted = NotionAutoSummary.ExtractFromHtml(html, _options.AutoSummaryMaxLength);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    if (item.Fields is Dictionary<string, ContentField> mutableFields)
                    {
                        mutableFields["summary"] = new ContentField("text", extracted);
                    }
                }
            }

            return html;
        }));
    }

    public async Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
    {
        var result = await LoadAsync(cancellationToken);
        var documents = result.Items.Select(ToRawContentDocument).ToArray();
        return new RawContentLoadResult(documents, result.BodyStore);
    }

    private static RawContentDocument ToRawContentDocument(ContentItem item)
    {
        var fields = item.Fields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        var properties = fields
            .Where(kv => !IsSourceKey(kv.Key))
            .ToDictionary(
                kv => kv.Key,
                kv => ToRawContentValue(kv.Value.Value),
                StringComparer.OrdinalIgnoreCase);
        var externalId = ContentFieldReader.GetText(fields, "notionPageId") ?? item.Id;

        return new RawContentDocument(
            SourceId: item.Id,
            SourceKind: "notion",
            Title: item.Title,
            Slug: item.Slug,
            PublishedAt: item.PublishAt,
            Body: new RawBody(item.ContentHtml, item.BodyKey, null, null),
            Properties: properties,
            Source: new ContentSourceInfo("notion", null, null, externalId, null, null, "loaded"),
            CustomFields: fields);
    }

    private static bool IsSourceKey(string key)
    {
        return string.Equals(key, "source", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "notionPageId", StringComparison.OrdinalIgnoreCase);
    }

    private static void PromoteTextFieldAlias(Dictionary<string, ContentField> fields, string sourceKey, string targetKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) ||
            string.IsNullOrWhiteSpace(targetKey) ||
            !fields.TryGetValue(sourceKey, out var field) ||
            field.Value is null)
        {
            return;
        }

        var text = field.Value.ToString()?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            fields[targetKey] = new ContentField("text", text);
        }
    }

    private static void NormalizeTaxonomyField(Dictionary<string, ContentField> fields, string key)
    {
        if (!fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return;
        }

        if (field.Value is string text)
        {
            text = text.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                fields[key] = new ContentField("text", text);
            }

            return;
        }

        if (field.Value is IEnumerable<string> strings)
        {
            var values = strings.Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray();
            if (values.Length > 0)
            {
                fields[key] = new ContentField("list", values);
            }

            return;
        }

        if (field.Value is IEnumerable<object> objects)
        {
            var values = objects.Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray();
            if (values.Length > 0)
            {
                fields[key] = new ContentField("list", values);
            }
        }
    }

    private static RawContentValue ToRawContentValue(object? value)
    {
        return value switch
        {
            bool => new RawContentValue("bool", value),
            int or long or double or float => new RawContentValue("number", value),
            IEnumerable<string> => new RawContentValue("list", value),
            IEnumerable<object> => new RawContentValue("list", value),
            _ => new RawContentValue("text", value)
        };
    }

    internal sealed record PageDraft(
        string PageId,
        string Title,
        string Slug,
        string Type,
        DateTimeOffset PublishAt,
        string? LastEditedTime,
        IReadOnlyDictionary<string, ContentField> Fields,
        IReadOnlyList<string> RelationKeys);

    internal static string? Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var sb = new StringBuilder();
        var lastDash = false;
        foreach (var ch in text.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastDash = false;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_')
            {
                if (!lastDash && sb.Length > 0)
                {
                    sb.Append('-');
                    lastDash = true;
                }

                continue;
            }
        }

        var slug = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }

    internal static string ExtractPlainText(JsonElement richTextArray)
    {
        if (richTextArray.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var item in richTextArray.EnumerateArray())
        {
            if (item.TryGetProperty("plain_text", out var plainTextEl) && plainTextEl.ValueKind == JsonValueKind.String)
            {
                sb.Append(plainTextEl.GetString());
            }
        }

        return sb.ToString();
    }

    internal static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v))
        {
            return null;
        }

        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    internal static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    internal static bool TryParseDateTimeOffset(string text, out DateTimeOffset dto)
    {
        dto = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return DateTimeOffset.TryParse(text, out dto);
    }

    private static void EnsureNoCaseInsensitiveConflicts(JsonElement properties, string pageId)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in properties.EnumerateObject())
        {
            var name = prop.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (seen.TryGetValue(name, out var existing))
            {
                throw new ContentException(
                    $"Notion properties have conflicting names ignoring case: '{existing}' and '{name}' (page: {pageId}). " +
                    "Rename one of them to a unique name (case-insensitive).");
            }

            seen[name] = name;
        }
    }
}
