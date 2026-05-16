using System.Text;
using System.Text.Json;
using System.Net;
using System.Buffers;
using System.Diagnostics;
using Bukit.Shared;

namespace Bukit.Content.Notion;

public sealed class NotionContentProvider : IContentProvider
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
        var policyMode = NormalizePolicyMode(_options.FieldPolicyMode);
        var allowed = policyMode == "whitelist" ? BuildAllowedSet(_options.AllowedFields) : null;
        var resolvedProperties = await NotionDatabaseSchemaResolver.ResolveAsync(client, _options, cancellationToken);
        string? startCursor = null;
        var pageHtmlCache = CreatePageHtmlCache(_options);
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
                    var title = ExtractTitle(props) ?? pageId;
                    var slug = ExtractSlug(props) ?? Slugify(title) ?? pageId.Replace("-", string.Empty, StringComparison.Ordinal);
                    var type = ExtractType(props) ?? "post";
                    var publishAt = ExtractPublishAt(props) ?? DateTimeOffset.UtcNow;

                    var lastEditedTime = GetString(page, "last_edited_time");

                    var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = type,
                        ["source"] = "notion",
                        ["notionPageId"] = pageId,
                        ["bodyFingerprint"] = string.IsNullOrWhiteSpace(lastEditedTime) ? pageId : lastEditedTime
                    };

                    var fields = ExtractFields(props, policyMode, allowed, out var relationKeys);
                    fields = InjectPageCoverAndIcon(fields, page);
                    PromoteFieldToMeta(fields, meta, "language", "language");
                    PromoteFieldToMeta(fields, meta, "i18n_key", "i18nKey");
                    PromoteFieldToMeta(fields, meta, "i18nkey", "i18nKey");
                    PromoteFieldToMeta(fields, meta, "url", "url");
                    PromoteFieldToMeta(fields, meta, "outputpath", "outputPath");
                    PromoteFieldToMeta(fields, meta, "template", "template");
                    PromoteFieldToMeta(fields, meta, "summary", "summary");
                    PromoteTaxonomyFieldToMeta(fields, meta, "tags");
                    PromoteTaxonomyFieldToMeta(fields, meta, "categories");

                    drafts.Add(new PageDraft(pageId, title, slug, type, publishAt, lastEditedTime, meta, fields, relationKeys));
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
            var url = d.Meta.TryGetValue("url", out var u) ? u?.ToString() : null;
            url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
            return new RelationTargetInfo(d.PageId, d.Title, d.Slug, d.Type, url);
        });
        var draftIndex = NotionDraftIndex<PageDraft>.From(drafts, static d => d.PageId);
        var pageIndex = NotionRelationLinkBuilder.BuildIndex(targets);
        pageIndex = await ResolveMissingTaxonomyRelationTargetsAsync(client, drafts, pageIndex, relationTargetCache, cancellationToken);
        var items = new List<ContentItem>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            var fields = d.Fields;
            if (d.RelationKeys is { Count: > 0 })
            {
                fields = NotionRelationLinkBuilder.EnrichFields(fields, d.RelationKeys, pageIndex);
            }

            NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(d.Meta, fields, "tags");
            NotionTaxonomyPromoter.PromoteRelationTaxonomyTerms(d.Meta, fields, "categories");

            items.Add(new ContentItem(
                Id: d.PageId,
                Title: d.Title,
                Slug: d.Slug,
                PublishAt: d.PublishAt,
                ContentHtml: null,
                Meta: d.Meta,
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
            var html = await GetOrRenderPageHtmlAsync(renderer, pageHtmlCache, draft.PageId, draft.LastEditedTime, ct, _logger);

            if ((!item.Meta.TryGetValue("summary", out var summaryObj) || string.IsNullOrWhiteSpace(summaryObj?.ToString())) &&
                IsAutoSummaryEnabled() &&
                !string.IsNullOrWhiteSpace(html))
            {
                var extracted = NotionAutoSummary.ExtractFromHtml(html, GetAutoSummaryMaxLength());
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    if (item.Meta is Dictionary<string, object> mutableMeta)
                    {
                        mutableMeta["summary"] = extracted;
                    }
                }
            }

            return html;
        }));
    }

    private async Task<IReadOnlyDictionary<string, RelationTargetInfo>> ResolveMissingTaxonomyRelationTargetsAsync(
        NotionApiClient client,
        IReadOnlyList<PageDraft> drafts,
        IReadOnlyDictionary<string, RelationTargetInfo> existingIndex,
        NotionRelationTargetCache? relationTargetCache,
        CancellationToken cancellationToken)
    {
        var planStopwatch = Stopwatch.StartNew();
        var maxResolve = 200;
        var candidates = drafts.Select(static d => new NotionRelationResolveCandidate(d.RelationKeys, d.Fields));
        var missing = NotionRelationResolvePlan.BuildMissingIds(candidates, existingIndex, maxResolve);
        planStopwatch.Stop();
        _logger?.Info($"event=notion.relation.plan candidates={drafts.Count} missing={missing.Count} max_resolve={maxResolve} plan_ms={planStopwatch.ElapsedMilliseconds}");

        if (missing.Count == 0)
        {
            return existingIndex;
        }

        var concurrency = _options.RenderConcurrency is > 0 ? _options.RenderConcurrency.Value : 4;
        using var sem = new SemaphoreSlim(concurrency, concurrency);
        var tasks = new Task<RelationTargetInfo?>[missing.Count];
        var resolveStopwatch = Stopwatch.StartNew();
        var cacheHits = 0;
        for (var i = 0; i < missing.Count; i++)
        {
            var pageId = missing[i];
            if (relationTargetCache is not null)
            {
                var cached = await relationTargetCache.TryReadAsync(pageId, cancellationToken);
                if (cached is not null)
                {
                    tasks[i] = Task.FromResult<RelationTargetInfo?>(cached);
                    cacheHits++;
                    continue;
                }
            }

            tasks[i] = ResolveOneAsync(pageId);
        }

        await Task.WhenAll(tasks);
        resolveStopwatch.Stop();

        Dictionary<string, RelationTargetInfo>? merged = null;
        var resolvedCount = 0;
        for (var i = 0; i < tasks.Length; i++)
        {
            var t = await tasks[i];
            if (t is null)
            {
                continue;
            }

            merged ??= new Dictionary<string, RelationTargetInfo>(existingIndex, StringComparer.OrdinalIgnoreCase);
            merged[t.PageId] = t;
            resolvedCount++;
        }

        _logger?.Info($"event=notion.relation.resolve requested={missing.Count} resolved={resolvedCount} cache_hits={cacheHits} concurrency={concurrency} resolve_ms={resolveStopwatch.ElapsedMilliseconds}");

        return merged ?? existingIndex;

        async Task<RelationTargetInfo?> ResolveOneAsync(string pageId)
        {
            await sem.WaitAsync(cancellationToken);
            try
            {
                using var doc = await client.GetAsync(NotionApiUrls.Pages(pageId), cancellationToken);
                var page = doc.RootElement;
                var props = page.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
                var title = ExtractTitle(props) ?? pageId;
                var slug = ExtractSlug(props) ?? Slugify(title) ?? pageId.Replace("-", string.Empty, StringComparison.Ordinal);
                var type = ExtractType(props) ?? "page";

                var url = GetString(page, "url");
                url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();

                var target = new RelationTargetInfo(pageId, title, slug, type, url);
                if (relationTargetCache is not null)
                {
                    await relationTargetCache.WriteAsync(target, cancellationToken);
                }

                return target;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"event=notion.relation.resolve_failed pageId={pageId} message={ex.Message}");
                return null;
            }
            finally
            {
                sem.Release();
            }
        }
    }

    private sealed record PageDraft(
        string PageId,
        string Title,
        string Slug,
        string Type,
        DateTimeOffset PublishAt,
        string? LastEditedTime,
        Dictionary<string, object> Meta,
        IReadOnlyDictionary<string, ContentField> Fields,
        IReadOnlyList<string> RelationKeys);

    private static PageHtmlCache? CreatePageHtmlCache(NotionProviderOptions options)
    {
        var mode = NormalizeCacheMode(options.CacheMode);
        if (mode == "off")
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.CacheDir))
        {
            return null;
        }

        var root = options.CacheDir!.Trim();
        var pagesDir = Path.Combine(root, "pages");
        Directory.CreateDirectory(pagesDir);
        return new PageHtmlCache(mode, root, pagesDir);
    }

    private static string NormalizeCacheMode(string? mode)
    {
        return (mode ?? "off").Trim().ToLowerInvariant() switch
        {
            "readonly" => "readonly",
            "readwrite" => "readwrite",
            _ => "off"
        };
    }

    private static async Task<string> GetOrRenderPageHtmlAsync(
        NotionBlocksRenderer renderer,
        PageHtmlCache? cache,
        string pageId,
        string? lastEditedTime,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        if (cache is null)
        {
            return await renderer.RenderPageAsync(pageId, cancellationToken);
        }

        var cachePath = Path.Combine(cache.PagesDir, $"{pageId}.json");
        if (File.Exists(cachePath))
        {
            try
            {
                var json = await File.ReadAllBytesAsync(cachePath, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var version = root.TryGetProperty("version", out var v) && v.TryGetInt32(out var vv) ? vv : 0;
                var cachedLastEdited = root.TryGetProperty("lastEditedTime", out var let) && let.ValueKind == JsonValueKind.String ? let.GetString() : null;
                var cachedHtml = root.TryGetProperty("html", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;

                if (version == 1 &&
                    string.Equals(cachedLastEdited, lastEditedTime, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(cachedHtml))
                {
                    return cachedHtml!;
                }
            }
            catch (Exception ex)
            {
                logger?.Warn($"event=notion.cache.read_failed pageId={pageId} message={ex.Message}");
            }
        }

        if (cache.Mode == "readonly")
        {
            throw new ContentException($"Notion cache miss in readonly mode for page: {pageId}");
        }

        var html = await renderer.RenderPageAsync(pageId, cancellationToken);
        if (cache.Mode == "readwrite")
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", 1);
                if (lastEditedTime is null)
                {
                    writer.WriteNull("lastEditedTime");
                }
                else
                {
                    writer.WriteString("lastEditedTime", lastEditedTime);
                }
                writer.WriteString("html", html);
                writer.WriteEndObject();
            }

            await File.WriteAllBytesAsync(cachePath, buffer.WrittenMemory.ToArray(), cancellationToken);
        }

        return html;
    }

    private sealed record PageHtmlCache(string Mode, string RootDir, string PagesDir);

    private static void PromoteFieldToMeta(IReadOnlyDictionary<string, ContentField> fields, Dictionary<string, object> meta, string fieldKey, string metaKey)
    {
        if (!fields.TryGetValue(fieldKey, out var field))
        {
            return;
        }

        if (field.Value is null)
        {
            return;
        }

        var text = field.Value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        meta[metaKey] = text.Trim();
    }

    private static bool IsAutoSummaryEnabled() => EnvironmentHelper.IsAutoSummaryEnabled();

    private static int GetAutoSummaryMaxLength() => EnvironmentHelper.GetAutoSummaryMaxLength();

    private static void PromoteTaxonomyFieldToMeta(IReadOnlyDictionary<string, ContentField> fields, Dictionary<string, object> meta, string fieldKey)
    {
        if (!fields.TryGetValue(fieldKey, out var field) || field.Value is null)
        {
            return;
        }

        if (field.Value is string s)
        {
            var text = s.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                meta[fieldKey] = text;
            }
            return;
        }

        if (field.Value is IEnumerable<string> stringSeq)
        {
            var list = stringSeq
                .Select(x => x?.Trim() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<object>()
                .ToList();

            if (list.Count > 0)
            {
                meta[fieldKey] = list;
            }
            return;
        }

        if (field.Value is IEnumerable<object> objSeq)
        {
            var list = objSeq
                .Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<object>()
                .ToList();

            if (list.Count > 0)
            {
                meta[fieldKey] = list;
            }
        }
    }

    private static string NormalizePolicyMode(string? mode)
    {
        var m = (mode ?? "whitelist").Trim().ToLowerInvariant();
        return m is "all" ? "all" : "whitelist";
    }

    private static HashSet<string>? BuildAllowedSet(IReadOnlyList<string>? allowed)
    {
        if (allowed is null || allowed.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in allowed)
        {
            var k = NormalizeFieldKey(a);
            if (!string.IsNullOrWhiteSpace(k))
            {
                set.Add(k);
            }
        }

        return set;
    }

    private static IReadOnlyDictionary<string, ContentField> ExtractFields(
        JsonElement properties,
        string policyMode,
        HashSet<string>? allowed,
        out IReadOnlyList<string> relationKeys)
    {
        var relations = new List<string>();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (properties.ValueKind != JsonValueKind.Object)
        {
            relationKeys = relations;
            return fields;
        }

        foreach (var prop in properties.EnumerateObject())
        {
            var rawName = prop.Name;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                continue;
            }

            var key = NormalizeFieldKey(rawName);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (IsReservedNotionField(key))
            {
                continue;
            }

            if (policyMode == "whitelist" && allowed is not null && !allowed.Contains(key))
            {
                continue;
            }

            if (TryParseNotionPropertyToField(prop.Value, out var field, out var notionType))
            {
                fields[key] = field;
                if (string.Equals(notionType, "relation", StringComparison.OrdinalIgnoreCase))
                {
                    relations.Add(key);
                }
            }
        }

        relationKeys = relations;
        return fields;
    }

    private static IReadOnlyDictionary<string, ContentField> InjectPageCoverAndIcon(
        IReadOnlyDictionary<string, ContentField> fields, JsonElement page)
    {
        var coverUrl = ExtractPageFileUrl(page, "cover");
        var iconUrl = ExtractPageIconUrl(page);

        if (string.IsNullOrWhiteSpace(coverUrl) && string.IsNullOrWhiteSpace(iconUrl))
        {
            return fields;
        }

        var mutable = new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(coverUrl) && !mutable.ContainsKey("cover"))
        {
            mutable["cover"] = new ContentField("file", coverUrl);
        }

        if (!string.IsNullOrWhiteSpace(iconUrl) && !mutable.ContainsKey("icon"))
        {
            mutable["icon"] = new ContentField("file", iconUrl);
        }

        return mutable;
    }

    private static string? ExtractPageFileUrl(JsonElement page, string propertyName)
    {
        if (!page.TryGetProperty(propertyName, out var container) || container.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var fileType = GetString(container, "type");
        if (fileType == "external" &&
            container.TryGetProperty("external", out var ext) &&
            ext.ValueKind == JsonValueKind.Object)
        {
            return GetString(ext, "url");
        }

        if (fileType == "file" &&
            container.TryGetProperty("file", out var file) &&
            file.ValueKind == JsonValueKind.Object)
        {
            return GetString(file, "url");
        }

        return null;
    }

    private static string? ExtractPageIconUrl(JsonElement page)
    {
        if (!page.TryGetProperty("icon", out var icon) || icon.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var iconType = GetString(icon, "type");

        if (iconType == "external" &&
            icon.TryGetProperty("external", out var ext) &&
            ext.ValueKind == JsonValueKind.Object)
        {
            return GetString(ext, "url");
        }

        if (iconType == "file" &&
            icon.TryGetProperty("file", out var file) &&
            file.ValueKind == JsonValueKind.Object)
        {
            return GetString(file, "url");
        }

        // Emoji icons don't have URLs
        return null;
    }

    private static bool IsReservedNotionField(string normalizedKey)
    {
        return normalizedKey is "published" or "title" or "slug" or "type" or "publishat" or "publish_at";
    }

    private static string NormalizeFieldKey(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(trimmed.Length);
        var underscore = false;

        foreach (var ch in trimmed)
        {
            var lower = char.ToLowerInvariant(ch);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(lower);
                underscore = false;
                continue;
            }

            if (!underscore)
            {
                sb.Append('_');
                underscore = true;
            }
        }

        return sb.ToString().Trim('_');
    }

    private static bool TryParseNotionPropertyToField(JsonElement property, out ContentField field, out string notionType)
    {
        field = default!;
        notionType = string.Empty;
        if (!property.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var type = typeEl.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        notionType = type;
        switch (type)
        {
            case "title":
                {
                    var text = ExtractPlainTextArray(property, "title");
                    field = new ContentField("text", text);
                    return !string.IsNullOrWhiteSpace(text);
                }
            case "rich_text":
                {
                    var text = ExtractPlainTextArray(property, "rich_text");
                    field = new ContentField("text", text);
                    return !string.IsNullOrWhiteSpace(text);
                }
            case "url":
                {
                    if (property.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
                    {
                        var text = u.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "email":
                {
                    if (property.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String)
                    {
                        var text = e.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "phone_number":
                {
                    if (property.TryGetProperty("phone_number", out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        var text = p.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "number":
                {
                    if (property.TryGetProperty("number", out var n) && n.ValueKind is JsonValueKind.Number)
                    {
                        field = new ContentField("number", n.GetDouble());
                        return true;
                    }
                    return false;
                }
            case "checkbox":
                {
                    if (property.TryGetProperty("checkbox", out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        field = new ContentField("bool", b.GetBoolean());
                        return true;
                    }
                    return false;
                }
            case "date":
                {
                    if (property.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Object &&
                        d.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String)
                    {
                        var text = start.GetString();
                        if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var dto))
                        {
                            field = new ContentField("date", dto);
                            return true;
                        }
                    }
                    return false;
                }
            case "created_time":
                {
                    if (property.TryGetProperty("created_time", out var ct) && ct.ValueKind == JsonValueKind.String)
                    {
                        var text = ct.GetString() ?? string.Empty;
                        if (TryParseDateTimeOffset(text, out var dto))
                        {
                            field = new ContentField("date", dto);
                            return true;
                        }
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "last_edited_time":
                {
                    if (property.TryGetProperty("last_edited_time", out var lt) && lt.ValueKind == JsonValueKind.String)
                    {
                        var text = lt.GetString() ?? string.Empty;
                        if (TryParseDateTimeOffset(text, out var dto))
                        {
                            field = new ContentField("date", dto);
                            return true;
                        }
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "created_by":
                {
                    if (property.TryGetProperty("created_by", out var cb) && cb.ValueKind == JsonValueKind.Object)
                    {
                        var text = ExtractUserNameOrId(cb);
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "last_edited_by":
                {
                    if (property.TryGetProperty("last_edited_by", out var lb) && lb.ValueKind == JsonValueKind.Object)
                    {
                        var text = ExtractUserNameOrId(lb);
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "multi_select":
                {
                    if (property.TryGetProperty("multi_select", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        var list = arr.EnumerateArray()
                            .Select(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x!.Trim())
                            .ToList();

                        field = new ContentField("list", list);
                        return list.Count > 0;
                    }
                    return false;
                }
            case "select":
                {
                    if (property.TryGetProperty("select", out var sel) && sel.ValueKind == JsonValueKind.Object &&
                        sel.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    {
                        var text = n.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "status":
                {
                    if (property.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Object &&
                        status.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    {
                        var text = n.GetString() ?? string.Empty;
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "formula":
                {
                    if (property.TryGetProperty("formula", out var f) && f.ValueKind == JsonValueKind.Object)
                    {
                        return TryParseFormulaToField(f, out field);
                    }
                    return false;
                }
            case "people":
                {
                    if (property.TryGetProperty("people", out var people) && people.ValueKind == JsonValueKind.Array)
                    {
                        var list = people.EnumerateArray()
                            .Select(x => x.ValueKind == JsonValueKind.Object ? ExtractUserNameOrId(x) : string.Empty)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

                        field = new ContentField("list", list);
                        return list.Count > 0;
                    }
                    return false;
                }
            case "relation":
                {
                    if (property.TryGetProperty("relation", out var rel) && rel.ValueKind == JsonValueKind.Array)
                    {
                        var list = rel.EnumerateArray()
                            .Select(x => x.ValueKind == JsonValueKind.Object && x.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x!.Trim())
                            .ToList();

                        field = new ContentField("list", list);
                        return list.Count > 0;
                    }
                    return false;
                }
            case "rollup":
                {
                    if (property.TryGetProperty("rollup", out var rollup) && rollup.ValueKind == JsonValueKind.Object)
                    {
                        return TryParseRollupToField(rollup, out field);
                    }
                    return false;
                }
            case "unique_id":
                {
                    if (property.TryGetProperty("unique_id", out var uid) && uid.ValueKind == JsonValueKind.Object)
                    {
                        var text = BuildUniqueIdString(uid);
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "verification":
                {
                    if (property.TryGetProperty("verification", out var ver) && ver.ValueKind == JsonValueKind.Object)
                    {
                        var state = ver.TryGetProperty("state", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                        var text = (state ?? string.Empty).Trim();
                        field = new ContentField("text", text);
                        return !string.IsNullOrWhiteSpace(text);
                    }
                    return false;
                }
            case "files":
                {
                    if (property.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
                    {
                        var urls = new List<string>();
                        foreach (var f in files.EnumerateArray())
                        {
                            if (f.ValueKind != JsonValueKind.Object)
                            {
                                continue;
                            }

                            if (!f.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String)
                            {
                                continue;
                            }

                            var ft = t.GetString();
                            string? fileUrl = null;
                            if (string.Equals(ft, "external", StringComparison.OrdinalIgnoreCase) &&
                                f.TryGetProperty("external", out var ex) && ex.ValueKind == JsonValueKind.Object &&
                                ex.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
                            {
                                fileUrl = url.GetString();
                            }
                            else if (string.Equals(ft, "file", StringComparison.OrdinalIgnoreCase) &&
                                     f.TryGetProperty("file", out var ff) && ff.ValueKind == JsonValueKind.Object &&
                                     ff.TryGetProperty("url", out var furl) && furl.ValueKind == JsonValueKind.String)
                            {
                                fileUrl = furl.GetString();
                            }

                            if (!string.IsNullOrWhiteSpace(fileUrl))
                            {
                                urls.Add(fileUrl);
                            }
                        }

                        if (urls.Count == 1)
                        {
                            field = new ContentField("file", urls[0]);
                            return true;
                        }

                        if (urls.Count > 1)
                        {
                            field = new ContentField("files", urls.AsReadOnly());
                            return true;
                        }
                    }
                    return false;
                }
            default:
                return false;
        }
    }

    private static bool TryParseDateTimeOffset(string text, out DateTimeOffset dto)
    {
        dto = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return DateTimeOffset.TryParse(text, out dto);
    }

    private static string ExtractUserNameOrId(JsonElement user)
    {
        if (user.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (user.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
        {
            var n = name.GetString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(n))
            {
                return n.Trim();
            }
        }

        if (user.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
        {
            var s = id.GetString() ?? string.Empty;
            return s.Trim();
        }

        return string.Empty;
    }

    private static string BuildUniqueIdString(JsonElement uniqueId)
    {
        if (uniqueId.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var prefix = uniqueId.TryGetProperty("prefix", out var p) && p.ValueKind == JsonValueKind.String ? (p.GetString() ?? string.Empty).Trim() : string.Empty;
        var numberText = string.Empty;
        if (uniqueId.TryGetProperty("number", out var n))
        {
            if (n.ValueKind == JsonValueKind.Number && n.TryGetInt64(out var num))
            {
                numberText = num.ToString();
            }
            else if (n.ValueKind == JsonValueKind.String)
            {
                numberText = (n.GetString() ?? string.Empty).Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(numberText))
        {
            return prefix;
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return numberText;
        }

        return $"{prefix}-{numberText}";
    }

    private static bool TryParseRollupToField(JsonElement rollup, out ContentField field)
    {
        field = default!;
        if (!rollup.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var type = typeEl.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        if (type == "number" && rollup.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number)
        {
            field = new ContentField("number", n.GetDouble());
            return true;
        }

        if (type == "date" && rollup.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Object &&
            d.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String)
        {
            var text = start.GetString() ?? string.Empty;
            if (TryParseDateTimeOffset(text, out var dto))
            {
                field = new ContentField("date", dto);
                return true;
            }
        }

        if (type == "array" && rollup.TryGetProperty("array", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            var list = new List<object>();
            foreach (var item in arr.EnumerateArray())
            {
                if (TryParseNotionPropertyToField(item, out var inner, out _) && inner.Value is not null)
                {
                    list.Add(inner.Value);
                }
            }

            field = new ContentField("list", list);
            return list.Count > 0;
        }

        return false;
    }

    private static string ExtractPlainTextArray(JsonElement property, string key)
    {
        if (!property.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.TryGetProperty("plain_text", out var t) && t.ValueKind == JsonValueKind.String)
            {
                var s = t.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(s.Trim());
                }
            }
        }

        return sb.ToString();
    }

    private static string? ExtractTitle(JsonElement properties)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetPropertyIgnoreCase(properties, "Title", out var titleProp))
        {
            var text = ExtractTitleProperty(titleProp);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        foreach (var prop in properties.EnumerateObject())
        {
            var v = prop.Value;
            if (GetString(v, "type") == "title")
            {
                var text = ExtractTitleProperty(v);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? ExtractTitleProperty(JsonElement prop)
    {
        if (prop.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!prop.TryGetProperty("title", out var titleArray) || titleArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var item in titleArray.EnumerateArray())
        {
            if (item.TryGetProperty("plain_text", out var plain) && plain.ValueKind == JsonValueKind.String)
            {
                sb.Append(plain.GetString());
            }
        }

        return sb.ToString().Trim();
    }

    private static string? ExtractSlug(JsonElement properties)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetPropertyIgnoreCase(properties, "Slug", out var slugProp))
        {
            return null;
        }

        var type = GetString(slugProp, "type");
        if (type == "rich_text" && slugProp.TryGetProperty("rich_text", out var rt))
        {
            var text = ExtractPlainText(rt);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        if (type == "formula" && slugProp.TryGetProperty("formula", out var f) && f.ValueKind == JsonValueKind.Object)
        {
            var value = GetString(f, "string");
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    private static string? ExtractType(JsonElement properties)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetPropertyIgnoreCase(properties, "Type", out var typeProp))
        {
            return null;
        }

        var t = GetString(typeProp, "type");
        if (t == "select" && typeProp.TryGetProperty("select", out var sel) && sel.ValueKind == JsonValueKind.Object)
        {
            return GetString(sel, "name");
        }

        if (t == "multi_select" && typeProp.TryGetProperty("multi_select", out var ms) && ms.ValueKind == JsonValueKind.Array)
        {
            var first = ms.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                return GetString(first, "name");
            }
        }

        return null;
    }

    private static DateTimeOffset? ExtractPublishAt(JsonElement properties)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetPropertyIgnoreCase(properties, "PublishAt", out var dateProp))
        {
            var value = ReadDateProperty(dateProp);
            if (value is not null)
            {
                return value;
            }
        }

        if (TryGetPropertyIgnoreCase(properties, "Date", out var dateProp2))
        {
            var value = ReadDateProperty(dateProp2);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadDateProperty(JsonElement prop)
    {
        var type = GetString(prop, "type");
        if (type != "date")
        {
            return null;
        }

        if (!prop.TryGetProperty("date", out var dateObj) || dateObj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var start = GetString(dateObj, "start");
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(start, out var dto))
        {
            return dto;
        }

        return null;
    }

    private static string? Slugify(string text)
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

    private static string ExtractPlainText(JsonElement richTextArray)
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

    private static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v))
        {
            return null;
        }

        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
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

    private static bool TryParseFormulaToField(JsonElement formula, out ContentField field)
    {
        field = default!;
        var type = GetString(formula, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        switch (type)
        {
            case "string":
                {
                    var text = GetString(formula, "string") ?? string.Empty;
                    field = new ContentField("text", text);
                    return !string.IsNullOrWhiteSpace(text);
                }
            case "number":
                {
                    if (formula.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number)
                    {
                        field = new ContentField("number", n.GetDouble());
                        return true;
                    }
                    return false;
                }
            case "boolean":
                {
                    if (formula.TryGetProperty("boolean", out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        field = new ContentField("bool", b.GetBoolean());
                        return true;
                    }
                    return false;
                }
            case "date":
                {
                    if (formula.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.Object &&
                        d.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.String)
                    {
                        var text = start.GetString();
                        if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var dto))
                        {
                            field = new ContentField("date", dto);
                            return true;
                        }
                    }
                    return false;
                }
        }

        return false;
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
