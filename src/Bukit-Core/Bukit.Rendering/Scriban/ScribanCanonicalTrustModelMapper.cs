using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanCanonicalTrustModelMapper
{
    internal static ScriptObject ToScriptObject(ContentRecord model)
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

    internal static ScriptObject ToScriptObject(ProvenanceRecord model)
    {
        var obj = new ScriptObject();
        obj.SetValue("source", model.Source, readOnly: true);
        obj.SetValue("original_source", model.OriginalSource, readOnly: true);
        obj.SetValue("sync_status", model.SyncStatus, readOnly: true);
        return obj;
    }

    internal static ScriptObject ToScriptObject(TrustMetadata model)
    {
        var obj = new ScriptObject();
        obj.SetValue("credibility_score", model.CredibilityScore, readOnly: true);
        obj.SetValue("review_status", model.ReviewStatus, readOnly: true);
        return obj;
    }

    internal static ScriptArray ToScriptArray(IReadOnlyList<EntityRecord> entities)
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
}
