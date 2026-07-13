using Scriban.Runtime;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Rendering.Scriban;

public static class ScribanModelBinder
{
    public static ScriptObject ToScriptObject(PageModel model)
    {
        var root = new ScriptObject();
        root.SetValue("site", ToScriptObject(model.Site), readOnly: true);
        root.SetValue("page", ToScriptObject(model.Page), readOnly: true);
        if (model.Page.Seo is not null)
        {
            root.SetValue("seo", ToScriptObject(model.Page.Seo), readOnly: true);
        }

        AddDerivedListAliases(root, model.Page.Fields);
        return root;
    }

    public static ScriptObject ToScriptObject(ListPageModel model)
    {
        var root = new ScriptObject();
        root.SetValue("site", ToScriptObject(model.Site), readOnly: true);

        root.SetValue("page", ToScriptObject(model.Page ?? new PageInfo
        {
            Title = model.Site.Title,
            Url = "/",
            Content = string.Empty
        }), readOnly: true);

        var pages = ToPageInfoScriptArray(model.Pages);
        var items = ToPageInfoScriptArray(model.Items ?? model.Pages);

        root.SetValue("pages", pages, readOnly: true);
        root.SetValue("items", items, readOnly: true);

        if (model.Pagination is not null)
        {
            root.SetValue("pagination", ToScriptObject(model.Pagination), readOnly: true);
        }

        if (model.Collection is not null)
        {
            root.SetValue("collection", ToScriptObject(model.Collection), readOnly: true);
        }

        if (model.Taxonomy is not null)
        {
            root.SetValue("taxonomy", ToScriptObject(model.Taxonomy), readOnly: true);
        }

        if (model.Filter is not null)
        {
            root.SetValue("filter", ToScriptObject(model.Filter), readOnly: true);
        }

        var seo = model.Seo ?? model.Page?.Seo;
        if (seo is not null)
        {
            root.SetValue("seo", ToScriptObject(seo), readOnly: true);
        }

        return root;
    }

    private static ScriptArray ToPageInfoScriptArray(IReadOnlyList<PageInfo> source)
    {
        var pages = new ScriptArray();
        foreach (var page in source)
        {
            pages.Add(ToScriptObject(page));
        }

        return pages;
    }

    private static ScriptObject ToScriptObject(SiteModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("name", model.Name, readOnly: true);
        obj.SetValue("title", model.Title, readOnly: true);
        obj.SetValue("url", model.Url, readOnly: true);
        obj.SetValue("description", model.Description, readOnly: true);
        obj.SetValue("base_url", model.BaseUrl, readOnly: true);
        obj.SetValue("base_path", model.BaseUrl, readOnly: true);
        obj.SetValue("language", model.Language, readOnly: true);
        obj.SetValue("build_year", model.BuildYear, readOnly: true);
        obj.SetValue("analytics", ToScriptObject(model.Analytics), readOnly: true);
        if (model.Params is not null)
        {
            obj.SetValue("params", ToScriptObject(model.Params), readOnly: true);
        }

        if (model.Modules is not null && model.Modules.Count > 0)
        {
            var modules = new ScriptObject();
            foreach (var kv in model.Modules)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    continue;
                }

                var arr = new ScriptArray();
                foreach (var m in kv.Value)
                {
                    arr.Add(ToScriptObject(m));
                }

                modules.SetValue(kv.Key, arr, readOnly: true);
            }

            obj.SetValue("modules", modules, readOnly: true);
        }

        if (model.Data is not null && model.Data.Count > 0)
        {
            obj.SetValue("data", ToScriptObject(model.Data), readOnly: true);
        }

        if (model.DataIndex is not null && model.DataIndex.Count > 0)
        {
            obj.SetValue("data_index", ToScriptObject(model.DataIndex), readOnly: true);
        }

        return obj;
    }

    private static ScriptObject ToScriptObject(ModuleInfo model)
    {
        var obj = new ScriptObject();
        obj.SetValue("id", model.Id, readOnly: true);
        obj.SetValue("title", model.Title, readOnly: true);
        obj.SetValue("slug", model.Slug, readOnly: true);
        obj.SetValue("content", model.Content, readOnly: true);
        obj.SetValue("fields", ToFieldsScriptObject(model.Fields), readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(PageInfo model)
    {
        var obj = new ScriptObject();
        obj.SetValue("title", model.Title, readOnly: true);
        obj.SetValue("url", model.Url, readOnly: true);
        obj.SetValue("content", model.Content, readOnly: true);
        obj.SetValue("summary", model.Summary, readOnly: true);
        obj.SetValue("table_of_contents", ToTableOfContentsScriptArray(model.TableOfContents), readOnly: true);
        obj.SetValue("tableOfContents", ToTableOfContentsScriptArray(model.TableOfContents), readOnly: true);
        obj.SetValue("publish_date", model.PublishDate?.DateTime, readOnly: true);
        obj.SetValue("updated_at", model.UpdatedAt?.DateTime, readOnly: true);
        obj.SetValue("fields", ToFieldsScriptObject(model.Fields), readOnly: true);
        if (model.ContentRecord is not null)
        {
            var contentRecord = ToScriptObject(model.ContentRecord);
            obj.SetValue("content_model", contentRecord, readOnly: true);
            obj.SetValue("content_record", contentRecord, readOnly: true);
        }

        if (model.Entities is not null)
        {
            obj.SetValue("entities", ToScriptArray(model.Entities), readOnly: true);
        }

        if (model.Provenance is not null)
        {
            obj.SetValue("provenance", ToScriptObject(model.Provenance), readOnly: true);
        }

        if (model.Trust is not null)
        {
            obj.SetValue("trust", ToScriptObject(model.Trust), readOnly: true);
        }

        if (model.Representations is not null)
        {
            var representations = new ScriptArray();
            foreach (var representation in model.Representations)
            {
                representations.Add(representation);
            }

            obj.SetValue("representations", representations, readOnly: true);
        }

        if (model.Seo is not null)
        {
            obj.SetValue("seo", ToScriptObject(model.Seo), readOnly: true);
        }

        return obj;
    }

    private static void AddDerivedListAliases(ScriptObject root, IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (!IsDerivedListLikePage(fields))
        {
            return;
        }

        if (TryGetFieldValue(fields, "items", out var items))
        {
            var itemsValue = ToScribanValue(items);
            root.SetValue("items", itemsValue, readOnly: true);
            root.SetValue("pages", itemsValue, readOnly: true);
        }

        if (TryGetFieldValue(fields, "pagination", out var pagination))
        {
            root.SetValue("pagination", ToScribanValue(pagination), readOnly: true);
        }

        if (TryGetFieldValue(fields, "taxonomy", out var taxonomy))
        {
            root.SetValue("taxonomy", ToScribanValue(taxonomy), readOnly: true);
        }

        if (TryGetFieldValue(fields, "filter", out var filter))
        {
            root.SetValue("filter", ToScribanValue(filter), readOnly: true);
        }

        if (TryGetFieldValue(fields, "collection", out var collection))
        {
            root.SetValue("collection", ToCollectionAlias(collection), readOnly: true);
        }
    }

    private static object ToCollectionAlias(object? value)
    {
        if (value is IReadOnlyDictionary<string, object> or IDictionary<string, object>)
        {
            return ToScribanValue(value);
        }

        var obj = new ScriptObject();
        obj.SetValue("key", value?.ToString(), readOnly: true);
        return obj;
    }

    private static bool IsDerivedListLikePage(IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null)
        {
            return false;
        }

        if (!TryGetFieldValue(fields, "items", out _))
        {
            return false;
        }

        if (!TryGetFieldValue(fields, "pagination", out _) && !TryGetFieldValue(fields, "taxonomy", out _))
        {
            return false;
        }

        return TryGetFieldValue(fields, "type", out var type) &&
               string.Equals(type?.ToString(), "derived", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFieldValue(IReadOnlyDictionary<string, ContentField>? fields, string key, out object? value)
    {
        value = null;
        if (fields is null)
        {
            return false;
        }

        foreach (var kv in fields)
        {
            if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = kv.Value.Value;
            return true;
        }

        return false;
    }

    private static ScriptArray ToTableOfContentsScriptArray(IReadOnlyList<TableOfContentsEntry>? entries)
    {
        var arr = new ScriptArray();
        if (entries is null)
        {
            return arr;
        }

        foreach (var entry in entries)
        {
            var obj = new ScriptObject();
            obj.SetValue("level", entry.Level, readOnly: true);
            obj.SetValue("text", entry.Text, readOnly: true);
            obj.SetValue("id", entry.Id, readOnly: true);
            obj.SetValue("url", "#" + entry.Id, readOnly: true);
            arr.Add(obj);
        }

        return arr;
    }

    private static ScriptObject ToScriptObject(AnalyticsModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("enabled", model.Enabled, readOnly: true);
        obj.SetValue("googleAnalyticsId", model.GoogleAnalyticsId, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(SeoModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("title", model.Title, readOnly: true);
        obj.SetValue("document_title", model.DocumentTitle, readOnly: true);
        obj.SetValue("description", model.Description, readOnly: true);
        obj.SetValue("canonical", model.Canonical, readOnly: true);
        obj.SetValue("prev", model.Prev, readOnly: true);
        obj.SetValue("next", model.Next, readOnly: true);
        obj.SetValue("robots", model.Robots, readOnly: true);
        obj.SetValue("og", ToScriptObject(model.Og), readOnly: true);
        obj.SetValue("twitter", ToScriptObject(model.Twitter), readOnly: true);
        obj.SetValue("article", ToScriptObject(model.Article), readOnly: true);

        var alternates = new ScriptArray();
        foreach (var alternate in model.Alternates)
        {
            alternates.Add(ToScriptObject(alternate));
        }

        obj.SetValue("alternates", alternates, readOnly: true);

        var jsonLd = new ScriptArray();
        foreach (var json in model.JsonLd)
        {
            jsonLd.Add(json);
        }

        obj.SetValue("json_ld", jsonLd, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(SeoOpenGraphModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("title", model.Title, readOnly: true);
        obj.SetValue("description", model.Description, readOnly: true);
        obj.SetValue("url", model.Url, readOnly: true);
        obj.SetValue("image", model.Image, readOnly: true);
        obj.SetValue("type", model.Type, readOnly: true);
        obj.SetValue("site_name", model.SiteName, readOnly: true);
        obj.SetValue("locale", model.Locale, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(SeoTwitterModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("card", model.Card, readOnly: true);
        obj.SetValue("title", model.Title, readOnly: true);
        obj.SetValue("description", model.Description, readOnly: true);
        obj.SetValue("image", model.Image, readOnly: true);
        obj.SetValue("site", model.Site, readOnly: true);
        obj.SetValue("creator", model.Creator, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(SeoArticleModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("published_time", model.PublishedTime?.ToString("O"), readOnly: true);
        obj.SetValue("modified_time", model.ModifiedTime?.ToString("O"), readOnly: true);
        obj.SetValue("author", model.Author, readOnly: true);
        obj.SetValue("author_type", model.AuthorType, readOnly: true);

        var tags = new ScriptArray();
        foreach (var tag in model.Tags)
        {
            tags.Add(tag);
        }

        obj.SetValue("tags", tags, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(SeoAlternateModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("hreflang", model.Hreflang, readOnly: true);
        obj.SetValue("href", model.Href, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(ListPaginationModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("page", model.Page, readOnly: true);
        obj.SetValue("page_size", model.PageSize, readOnly: true);
        obj.SetValue("total_pages", model.TotalPages, readOnly: true);
        obj.SetValue("total_items", model.TotalItems, readOnly: true);
        obj.SetValue("total", model.TotalItems, readOnly: true);
        obj.SetValue("has_prev", model.HasPrev, readOnly: true);
        obj.SetValue("has_next", model.HasNext, readOnly: true);
        obj.SetValue("prev_url", model.PrevUrl, readOnly: true);
        obj.SetValue("next_url", model.NextUrl, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(ListCollectionModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("key", model.Key, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(ListTaxonomyModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("kind", model.Kind, readOnly: true);
        obj.SetValue("term", model.Term, readOnly: true);
        obj.SetValue("slug", model.Slug, readOnly: true);
        obj.SetValue("route_prefix", model.RoutePrefix, readOnly: true);
        obj.SetValue("routePrefix", model.RoutePrefix, readOnly: true);
        obj.SetValue("url", model.Url, readOnly: true);
        obj.SetValue("is_index", model.IsIndex, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(ListFilterModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("field", model.Field, readOnly: true);
        obj.SetValue("operator", model.Operator, readOnly: true);
        obj.SetValue("value", model.Value, readOnly: true);

        var values = new ScriptArray();
        foreach (var value in model.Values)
        {
            values.Add(value);
        }

        obj.SetValue("values", values, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(ContentRecord model)
    {
        var obj = new ScriptObject();
        obj.SetValue("id", model.Identity.Id, readOnly: true);
        obj.SetValue("slug", model.Identity.Slug, readOnly: true);
        obj.SetValue("canonical_url_key", model.Identity.CanonicalUrlKey, readOnly: true);
        obj.SetValue("content_type", model.Identity.ContentType, readOnly: true);
        obj.SetValue("status", model.Identity.Status, readOnly: true);
        obj.SetValue("title", model.Presentation.Title, readOnly: true);
        obj.SetValue("summary", model.Presentation.Summary, readOnly: true);
        obj.SetValue("language", model.Presentation.Language, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(ContentRoutePolicy model)
    {
        var obj = new ScriptObject();
        obj.SetValue("url", model.Url, readOnly: true);
        obj.SetValue("output_path", model.OutputPath, readOnly: true);
        obj.SetValue("template", model.Template, readOnly: true);
        obj.SetValue("permalink_pattern", model.PermalinkPattern, readOnly: true);
        obj.SetValue("list_group", model.ListGroup, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(ContentPublishPolicy model)
    {
        var obj = new ScriptObject();
        obj.SetValue("draft", model.Draft, readOnly: true);
        obj.SetValue("noindex", model.NoIndex, readOnly: true);
        obj.SetValue("nofollow", model.NoFollow, readOnly: true);
        obj.SetValue("exclude_from_feed", model.ExcludeFromFeed, readOnly: true);
        obj.SetValue("exclude_from_search", model.ExcludeFromSearch, readOnly: true);
        obj.SetValue("exclude_from_sitemap", model.ExcludeFromSitemap, readOnly: true);
        obj.SetValue("is_data_module", model.IsDataModule, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(ProvenanceRecord model)
    {
        var obj = new ScriptObject();
        obj.SetValue("source", model.Source, readOnly: true);
        obj.SetValue("original_source", model.OriginalSource, readOnly: true);
        obj.SetValue("sync_status", model.SyncStatus, readOnly: true);
        return obj;
    }

    private static ScriptObject ToScriptObject(TrustMetadata model)
    {
        var obj = new ScriptObject();
        obj.SetValue("credibility_score", model.CredibilityScore, readOnly: true);
        obj.SetValue("review_status", model.ReviewStatus, readOnly: true);
        return obj;
    }

    private static ScriptArray ToScriptArray(IReadOnlyList<EntityRecord> entities)
    {
        var arr = new ScriptArray();
        foreach (var entity in entities)
        {
            var obj = new ScriptObject();
            obj.SetValue("type", entity.Type, readOnly: true);
            obj.SetValue("name", entity.Name, readOnly: true);
            obj.SetValue("description", entity.Description, readOnly: true);
            arr.Add(obj);
        }

        return arr;
    }

    private static ScriptObject ToScriptObject(IReadOnlyDictionary<string, object> dict)
    {
        var obj = new ScriptObject();
        foreach (var kv in dict)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            obj.SetValue(kv.Key, ToScribanValue(kv.Value), readOnly: true);
        }

        return obj;
    }

    private static ScriptObject ToFieldsScriptObject(IReadOnlyDictionary<string, ContentField>? fields)
    {
        var obj = new ScriptObject();
        if (fields is null || fields.Count == 0)
        {
            return obj;
        }

        foreach (var kv in fields)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            var f = kv.Value;
            var fieldObj = new ScriptObject();
            fieldObj.SetValue("type", f.Type, readOnly: true);
            fieldObj.SetValue("value", ToScribanValue(f.Value), readOnly: true);
            obj.SetValue(kv.Key, fieldObj, readOnly: true);
        }

        return obj;
    }

    private static object ToScribanValue(object? value)
    {
        if (value is null)
        {
            return null!;
        }

        if (value is string or bool or int or long or float or double or decimal or DateTime or DateTimeOffset)
        {
            return value;
        }

        if (value is ModuleInfo module)
        {
            return ToScriptObject(module);
        }

        if (value is IReadOnlyDictionary<string, object> roDict)
        {
            return ToScriptObject(roDict);
        }

        if (value is IDictionary<string, object> dict)
        {
            return ToScriptObject(new Dictionary<string, object>(dict));
        }

        if (value is IEnumerable<object> seq)
        {
            var arr = new ScriptArray();
            foreach (var x in seq)
            {
                arr.Add(ToScribanValue(x));
            }

            return arr;
        }

        return value.ToString() ?? string.Empty;
    }
}
