using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanSeoModelMapper
{
    internal static ScriptObject ToScriptObject(SeoModel model)
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
}
