using Scriban.Runtime;
using Bukit.Content;

namespace Bukit.Rendering.Scriban;

public static class ScribanModelBinder
{
    public static ScriptObject ToScriptObject(PageModel model)
    {
        var root = new ScriptObject();
        root.SetValue("site", ToScriptObject(model.Site), readOnly: true);
        root.SetValue("page", ToScriptObject(model.Page), readOnly: true);
        return root;
    }

    public static ScriptObject ToScriptObject(ListPageModel model)
    {
        var root = new ScriptObject();
        root.SetValue("site", ToScriptObject(model.Site), readOnly: true);

        var listPage = new ScriptObject();
        listPage.SetValue("title", model.Site.Title, readOnly: true);
        listPage.SetValue("url", "/", readOnly: true);
        root.SetValue("page", listPage, readOnly: true);

        var pages = new ScriptArray();
        foreach (var page in model.Pages)
        {
            pages.Add(ToScriptObject(page));
        }

        root.SetValue("pages", pages, readOnly: true);
        return root;
    }

    private static ScriptObject ToScriptObject(SiteModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("name", model.Name, readOnly: true);
        obj.SetValue("title", model.Title, readOnly: true);
        obj.SetValue("url", model.Url, readOnly: true);
        obj.SetValue("description", model.Description, readOnly: true);
        obj.SetValue("base_url", model.BaseUrl == "/" ? string.Empty : model.BaseUrl, readOnly: true);
        obj.SetValue("language", model.Language, readOnly: true);
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
        obj.SetValue("publish_date", model.PublishDate?.DateTime, readOnly: true);
        obj.SetValue("fields", ToFieldsScriptObject(model.Fields), readOnly: true);
        return obj;
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
