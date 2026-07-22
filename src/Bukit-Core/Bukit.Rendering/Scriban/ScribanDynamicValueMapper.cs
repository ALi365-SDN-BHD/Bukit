using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanDynamicValueMapper
{
    internal static ScriptObject ToScriptObject(ModuleInfo model)
    {
        var obj = new ScriptObject();
        obj.SetValue("id", model.Id, readOnly: true);
        obj.SetValue("title", model.Title, readOnly: true);
        obj.SetValue("slug", model.Slug, readOnly: true);
        obj.SetValue("content", model.Content, readOnly: true);
        obj.SetValue("fields", ToFieldsScriptObject(model.Fields), readOnly: true);
        return obj;
    }

    internal static ScriptObject ToScriptObject(IReadOnlyDictionary<string, object> dict)
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

    internal static ScriptObject ToFieldsScriptObject(IReadOnlyDictionary<string, ContentField>? fields)
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

            var field = kv.Value;
            var fieldObj = new ScriptObject();
            fieldObj.SetValue("type", field.Type, readOnly: true);
            fieldObj.SetValue("value", ToScribanValue(field.Value), readOnly: true);
            obj.SetValue(kv.Key, fieldObj, readOnly: true);
        }

        return obj;
    }

    internal static object ToScribanValue(object? value)
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

        if (value is IReadOnlyDictionary<string, object> readOnlyDictionary)
        {
            return ToScriptObject(readOnlyDictionary);
        }

        if (value is IDictionary<string, object> dictionary)
        {
            return ToScriptObject(new Dictionary<string, object>(dictionary));
        }

        if (value is IEnumerable<object> sequence)
        {
            var arr = new ScriptArray();
            foreach (var item in sequence)
            {
                arr.Add(ToScribanValue(item));
            }

            return arr;
        }

        return value.ToString() ?? string.Empty;
    }
}
