using System.Text.Json;
using Bukit.Engine;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class SearchIndexPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "search-index";
    public string Version => "2.1.0";

    public void AfterBuild(BuildContext context)
    {
        var outPath = Path.Combine(context.OutputDir, "search.json");
        Directory.CreateDirectory(context.OutputDir);
        var emitSnippet = TemplateCapabilitiesResolver.SupportsSearchSnippets(TemplateCapabilitiesResolver.SearchTemplatePath, context.LayoutsDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartArray();

        var itemsByPath = SearchIndexBuilder.BuildItemMap(
            context.Config.Site.SearchIncludeDerived
                ? context.Routed.Concat(context.DerivedRouted)
                : context.Routed);

        foreach (var (key, seo) in context.SeoIndex
                     .Where(x => x.Value.Indexable)
                     .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!itemsByPath.TryGetValue(key, out var item))
            {
                continue;
            }

            SearchIndexBuilder.WriteSearchItem(writer, item, seo.Route, context.BaseUrl, context.BodyStore, emitSnippet);
        }

        writer.WriteEndArray();
        writer.Flush();
    }

}
