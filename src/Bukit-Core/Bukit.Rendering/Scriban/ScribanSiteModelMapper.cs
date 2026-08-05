using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanSiteModelMapper
{
    internal static ScriptObject ToScriptObject(SiteModel model)
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
        if (model.Params is not null)
        {
            obj.SetValue("params", ScribanDynamicValueMapper.ToScriptObject(model.Params), readOnly: true);
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
                foreach (var module in kv.Value)
                {
                    arr.Add(ScribanDynamicValueMapper.ToScriptObject(module));
                }

                modules.SetValue(kv.Key, arr, readOnly: true);
            }

            obj.SetValue("modules", modules, readOnly: true);
        }

        if (model.Data is not null && model.Data.Count > 0)
        {
            var publicData = model.Data
                .Where(entry => !entry.Key.StartsWith("__", StringComparison.Ordinal))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            if (publicData.Count > 0)
            {
                obj.SetValue("data", ScribanDynamicValueMapper.ToScriptObject(publicData), readOnly: true);
            }

            SetInternalProjectionAlias(obj, model.Data, "__data_files", "data_files");
            SetInternalProjectionAlias(obj, model.Data, "__related_pages", "related_pages");
        }

        if (model.DataIndex is not null && model.DataIndex.Count > 0)
        {
            obj.SetValue("data_index", ScribanDynamicValueMapper.ToScriptObject(model.DataIndex), readOnly: true);
        }

        return obj;
    }

    private static void SetInternalProjectionAlias(
        ScriptObject target,
        IReadOnlyDictionary<string, object> data,
        string internalKey,
        string publicAlias)
    {
        if (data.TryGetValue(internalKey, out var value))
        {
            target.SetValue(publicAlias, ScribanDynamicValueMapper.ToScribanValue(value), readOnly: true);
        }
    }
}
