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

            writer.AppendNewline();
            writer.AppendUtf8(module.Key);
            writer.AppendNewline();
            writer.AppendUtf8(module.Value.Count.ToString());
            foreach (var item in module.Value.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                writer.AppendNewline();
                writer.AppendCanonicalValue(new Dictionary<string, object?>
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

            // Engine-internal context keys (double-underscore convention) carry live
            // pipeline objects, not template-visible site data; they are owned by their
            // own stages and must not enter the canonical value encoder.
            if (entry.Key.StartsWith("__", StringComparison.Ordinal))
            {
                continue;
            }

            writer.AppendNewline();
            writer.AppendUtf8(entry.Key);
            writer.AppendNewline();
            writer.AppendCanonicalValue(entry.Value);
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

            writer.AppendNewline();
            writer.AppendUtf8(entry.Key);
            writer.AppendNewline();
            writer.AppendObjectValue(entry.Value);
        }
    }
}
