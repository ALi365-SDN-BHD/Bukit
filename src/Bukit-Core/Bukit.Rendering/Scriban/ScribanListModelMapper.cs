using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanListModelMapper
{
    internal static ScriptArray ToPageInfoScriptArray(IReadOnlyList<PageInfo> source)
    {
        var pages = new ScriptArray();
        foreach (var page in source)
        {
            pages.Add(ScribanPageModelMapper.ToScriptObject(page));
        }

        return pages;
    }

    internal static ScriptObject ToScriptObject(ListPaginationModel model)
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

    internal static ScriptObject ToScriptObject(ListCollectionModel model)
    {
        var obj = new ScriptObject();
        obj.SetValue("key", model.Key, readOnly: true);
        return obj;
    }

    internal static ScriptObject ToScriptObject(ListTaxonomyModel model)
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

    internal static ScriptObject ToScriptObject(ListFilterModel model)
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
}
