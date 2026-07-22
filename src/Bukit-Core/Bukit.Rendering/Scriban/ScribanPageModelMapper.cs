using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanPageModelMapper
{
    internal static ScriptObject ToScriptObject(PageInfo model)
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
        obj.SetValue("fields", ScribanDynamicValueMapper.ToFieldsScriptObject(model.Fields), readOnly: true);
        if (model.ContentRecord is not null)
        {
            var contentRecord = ScribanCanonicalTrustModelMapper.ToScriptObject(model.ContentRecord);
            obj.SetValue("content_model", contentRecord, readOnly: true);
            obj.SetValue("content_record", contentRecord, readOnly: true);
        }

        if (model.Entities is not null)
        {
            obj.SetValue("entities", ScribanCanonicalTrustModelMapper.ToScriptArray(model.Entities), readOnly: true);
        }

        if (model.Provenance is not null)
        {
            obj.SetValue("provenance", ScribanCanonicalTrustModelMapper.ToScriptObject(model.Provenance), readOnly: true);
        }

        if (model.Trust is not null)
        {
            obj.SetValue("trust", ScribanCanonicalTrustModelMapper.ToScriptObject(model.Trust), readOnly: true);
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
            obj.SetValue("seo", ScribanSeoModelMapper.ToScriptObject(model.Seo), readOnly: true);
        }

        return obj;
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
}
