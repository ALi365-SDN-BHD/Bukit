using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanRootModelMapper
{
    internal static ScriptObject ToScriptObject(PageModel model, SharedModelCache? shared = null)
    {
        shared ??= SharedModelCache.Current;
        var root = new ScriptObject();
        root.SetValue("site", ToSite(model.Site, shared), readOnly: true);
        root.SetValue("page", ScribanPageModelMapper.ToScriptObject(model.Page), readOnly: true);
        root.SetValue("pages", shared?.GetPages(model.Pages) ?? ScribanListModelMapper.ToPageInfoScriptArray(model.Pages), readOnly: true);
        if (model.Page.Seo is not null)
        {
            root.SetValue("seo", ScribanSeoModelMapper.ToScriptObject(model.Page.Seo), readOnly: true);
        }

        ScribanDerivedListAliasProjector.AddAliases(root, model.Page.Fields);
        return root;
    }

    internal static ScriptObject ToScriptObject(ListPageModel model, SharedModelCache? shared = null)
    {
        shared ??= SharedModelCache.Current;
        var root = new ScriptObject();
        root.SetValue("site", ToSite(model.Site, shared), readOnly: true);

        root.SetValue("page", ScribanPageModelMapper.ToScriptObject(model.Page ?? new PageInfo
        {
            Title = model.Site.Title,
            Url = "/",
            Content = string.Empty
        }), readOnly: true);

        var pages = ScribanListModelMapper.ToPageInfoScriptArray(model.Pages);
        var itemsSource = model.Items ?? model.Pages;
        if (ReferenceEquals(itemsSource, model.Pages))
        {
            // One converted array under both names; freeze it so neither name can change the other.
            ScriptValueFreezer.Freeze(pages);
            root.SetValue("pages", pages, readOnly: true);
            root.SetValue("items", pages, readOnly: true);
        }
        else
        {
            root.SetValue("pages", pages, readOnly: true);
            root.SetValue("items", ScribanListModelMapper.ToPageInfoScriptArray(itemsSource), readOnly: true);
        }

        if (model.Pagination is not null)
        {
            root.SetValue("pagination", ScribanListModelMapper.ToScriptObject(model.Pagination), readOnly: true);
        }

        if (model.Collection is not null)
        {
            root.SetValue("collection", ScribanListModelMapper.ToScriptObject(model.Collection), readOnly: true);
        }

        if (model.Taxonomy is not null)
        {
            root.SetValue("taxonomy", ScribanListModelMapper.ToScriptObject(model.Taxonomy), readOnly: true);
        }

        if (model.Filter is not null)
        {
            root.SetValue("filter", ScribanListModelMapper.ToScriptObject(model.Filter), readOnly: true);
        }

        var seo = model.Seo ?? model.Page?.Seo;
        if (seo is not null)
        {
            root.SetValue("seo", ScribanSeoModelMapper.ToScriptObject(seo), readOnly: true);
        }

        return root;
    }

    private static ScriptObject ToSite(SiteModel site, SharedModelCache? shared) =>
        shared?.GetSite(site) ?? ScribanSiteModelMapper.ToScriptObject(site);
}
