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
                writer.AppendUtf8(item.Id);
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

            writer.AppendNewline();
            writer.AppendUtf8(entry.Key);
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
