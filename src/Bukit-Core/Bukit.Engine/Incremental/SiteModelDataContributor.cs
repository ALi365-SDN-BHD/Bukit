using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

internal sealed class SiteModelDataContributor : IRenderDependencyContributor
{
    public string Name => "site-model-data";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var excludedSource = context.Config.Content.RouteMetadata?.Source;
        AppendModules(writer, context.SiteModel.Modules, excludedSource);
        AppendData(writer, context.SiteModel.Data, excludedSource);
        AppendDataIndex(writer, context.SiteModel.DataIndex, excludedSource);
    }

    private static void AppendModules(
        RenderDependencyHashWriter writer,
        IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? modules,
        string? excludedSource)
    {
        if (modules is null || modules.Count == 0)
        {
            return;
        }

        foreach (var module in modules.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.Equals(module.Key, excludedSource, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            writer.AppendLabeledCanonicalValue("site.modules.key", module.Key);
            writer.AppendLabeledCanonicalValue("site.modules.count", module.Value.Count);
            foreach (var item in module.Value.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                writer.AppendLabeledCanonicalValue("site.modules.item", new Dictionary<string, object?>
                {
                    ["id"] = item.Id,
                    ["title"] = item.Title,
                    ["slug"] = item.Slug,
                    ["content"] = item.Content,
                    ["fields"] = item.Fields
                });
            }
        }
    }

    private static void AppendData(
        RenderDependencyHashWriter writer,
        IReadOnlyDictionary<string, object>? data,
        string? excludedSource)
    {
        if (data is null || data.Count == 0)
        {
            return;
        }

        foreach (var entry in data.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.Equals(entry.Key, excludedSource, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Key.StartsWith("__", StringComparison.Ordinal))
            {
                var publicAlias = entry.Key switch
                {
                    "__data_files" => "data_files",
                    "__related_pages" => "related_pages",
                    _ => null
                };
                if (publicAlias is not null)
                {
                    writer.AppendLabeledCanonicalValue($"site.{publicAlias}", entry.Value);
                }

                continue;
            }

            writer.AppendLabeledCanonicalValue($"site.data.{entry.Key}", entry.Value);
        }
    }

    private static void AppendDataIndex(
        RenderDependencyHashWriter writer,
        IReadOnlyDictionary<string, object>? dataIndex,
        string? excludedSource)
    {
        if (dataIndex is null || dataIndex.Count == 0)
        {
            return;
        }

        foreach (var entry in dataIndex.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.Equals(entry.Key, excludedSource, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            writer.AppendLabeledCanonicalValue($"site.data_index.{entry.Key}", entry.Value);
        }
    }
}
