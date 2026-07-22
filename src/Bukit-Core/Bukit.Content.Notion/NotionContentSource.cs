using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Shared;
using Bukit.Shared.Notion;
namespace Bukit.Content.Notion;

public sealed class NotionContentSource
{
    private readonly NotionContentSourceOptions _options;
    private readonly ILogger? _logger;
    private readonly Func<NotionContentClient> _clientFactory;

    public NotionContentSource(NotionContentSourceOptions options, ILogger? logger = null)
        : this(options, logger, () => new NotionContentClient(options))
    {
    }

    internal NotionContentSource(NotionContentSourceOptions options, ILogger? logger, Func<NotionContentClient> clientFactory)
    {
        _options = options;
        _logger = logger;
        _clientFactory = clientFactory;
    }

    public async Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
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
        var policyMode = NotionFieldProjectionHelper.NormalizePolicyMode(_options.FieldPolicyMode);
        var allowed = policyMode == "whitelist" ? NotionFieldProjectionHelper.BuildAllowedSet(_options.AllowedFields) : null;
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
                    var title = NotionContentPropertyParser.ExtractTitle(props, pm) ?? pageId;
                    var slug = NotionContentPropertyParser.ExtractSlug(props, pm) ?? Slugify(title) ?? pageId.Replace("-", string.Empty, StringComparison.Ordinal);
                    var type = NotionContentPropertyParser.ExtractType(props, pm);
                    var collection = NotionContentPropertyParser.ExtractCollection(props, pm);
                    var publishAt = ResolvePublishAt(page, props, pm, pageId);

                    var lastEditedTime = GetString(page, "last_edited_time");

                    var projectedValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["source"] = "notion",
                        ["notionPageId"] = pageId,
                        ["bodyFingerprint"] = string.IsNullOrWhiteSpace(lastEditedTime) ? pageId : lastEditedTime
                    };
                    if (DateTimeOffset.TryParse(
                            lastEditedTime,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out var lastEditedAt))
                    {
                        projectedValues["last_edited_time"] = lastEditedAt;
                    }
                    if (!string.IsNullOrWhiteSpace(type))
                    {
                        projectedValues["type"] = type;
                    }
                    if (!string.IsNullOrWhiteSpace(collection))
                    {
                        projectedValues["collection"] = collection;
                    }

                    var fields = NotionContentPropertyParser.ExtractFields(props, policyMode, allowed, out var relationKeys);
                    fields = NotionFieldProjectionHelper.InjectPageCoverAndIcon(fields, page);
                    NotionFieldProjectionHelper.ProjectTextField(fields, projectedValues, NotionContentPropertyParser.NormalizeFieldKey(pm?.Language ?? "language"), "language");
                    NotionFieldProjectionHelper.ProjectTextField(fields, projectedValues, NotionContentPropertyParser.NormalizeFieldKey(pm?.I18nKey ?? "i18n_key"), "i18nKey");
                    NotionFieldProjectionHelper.ProjectTextField(fields, projectedValues, "i18nkey", "i18nKey");
                    NotionFieldProjectionHelper.ProjectTextField(fields, projectedValues, "url", "url");
                    NotionFieldProjectionHelper.ProjectTextField(fields, projectedValues, "outputpath", "outputPath");
                    NotionFieldProjectionHelper.ProjectTextField(fields, projectedValues, "template", "template");
                    NotionFieldProjectionHelper.ProjectTextField(fields, projectedValues, NotionContentPropertyParser.NormalizeFieldKey(pm?.Summary ?? "summary"), "summary");
                    NotionFieldProjectionHelper.ProjectTaxonomyField(fields, projectedValues, "tags");
                    NotionFieldProjectionHelper.ProjectTaxonomyField(fields, projectedValues, "categories");
                    NotionContentPropertyParser.ProjectSeoFields(projectedValues, props, pm);
                    NotionContentPropertyParser.ProjectCanonicalFields(projectedValues, props, pm, pageId);

                    fields = ContentFieldReader.WithValues(fields, projectedValues);
                    drafts.Add(new PageDraft(pageId, title, slug, type ?? string.Empty, publishAt, lastEditedTime, fields, relationKeys));
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
        var items = new List<RawContentDocument>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            var fields = d.Fields;
            if (d.RelationKeys is { Count: > 0 })
            {
                fields = NotionRelationLinkBuilder.EnrichFields(fields, d.RelationKeys, pageIndex);
            }

            var taxonomyValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(taxonomyValues, fields, "tags");
            NotionTaxonomyPromoter.ProjectRelationTaxonomyTerms(taxonomyValues, fields, "categories");
            fields = ContentFieldReader.WithValues(fields, taxonomyValues);

            items.Add(new RawContentDocument(
                Id: d.PageId,
                Title: d.Title,
                Slug: d.Slug,
                PublishAt: d.PublishAt,
                Body: new RawBody(BodyKey: d.PageId),
                Properties: RawContentValue.FromFields(fields),
                Source: new ContentSourceInfo("notion", SourcePath: d.PageId, ExternalId: d.PageId, SyncStatus: "loaded"),
                CustomFields: fields
            ));
        }

        return new RawContentLoadResult(items, new NotionBodyStore(async (item, ct) =>
        {
            if (!_options.RenderContent)
            {
                return string.Empty;
            }

            var draft = draftIndex.GetRequired(item.Body.BodyKey ?? item.Id);

            using var bodyClient = _clientFactory();
            var renderer = new NotionBlocksRenderer(bodyClient.Transport);
            var html = await NotionCacheManager.GetOrRenderPageHtmlAsync(renderer, pageHtmlCache, draft.PageId, draft.LastEditedTime, ct, _logger);

            if (string.IsNullOrWhiteSpace(ContentFieldReader.GetText(item.CustomFields, "summary")) &&
                _options.AutoSummary &&
                !string.IsNullOrWhiteSpace(html))
            {
                var extracted = NotionAutoSummary.ExtractFromHtml(html, _options.AutoSummaryMaxLength);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    if (item.CustomFields is Dictionary<string, ContentField> mutableFields)
                    {
                        mutableFields["summary"] = new ContentField("text", extracted);
                    }
                }
            }

            return html;
        }));
    }

    private static DateTimeOffset ResolvePublishAt(
        JsonElement page,
        JsonElement properties,
        NotionPropertyMapConfig? propertyMap,
        string pageId)
    {
        var mappedPublishAt = NotionContentPropertyParser.ExtractPublishAt(properties, propertyMap);
        if (mappedPublishAt is not null)
        {
            return mappedPublishAt.Value;
        }

        var createdTime = GetString(page, "created_time");
        if (DateTimeOffset.TryParse(
                createdTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var createdAt))
        {
            return createdAt;
        }

        var publishAtName = propertyMap?.PublishAt ?? "PublishAt";
        var publishAtValue = TryGetPropertyIgnoreCase(properties, publishAtName, out var property)
            ? property.GetRawText()
            : "<missing>";
        throw new ContentException(
            $"Notion page '{pageId}' has no valid publish date: PublishAt '{publishAtName}'={publishAtValue}; created_time={createdTime ?? "<missing>"}.");
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
