using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record DataModuleResult(
    IReadOnlyList<ContentDocument> DataDocuments,
    IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? Modules,
    IReadOnlyDictionary<string, object>? SourceData,
    IReadOnlyDictionary<string, object>? DataIndex,
    IReadOnlyDictionary<string, RouteMetadataEntry>? RouteMetadata);

internal static class VariantDataSitePlanner
{
    internal static async Task<DataModuleResult> PrepareDataModulesAsync(
        IReadOnlyList<ContentDocument> documents,
        string language,
        IContentBodyStore bodyStore,
        IReadOnlyList<ContentSourceConfig>? sources = null,
        RouteMetadataConfig? routeMetadata = null,
        CancellationToken cancellationToken = default)
    {
        var dataDocuments = documents.Where(ContentFieldReader.IsDataItem).ToList();
        var templateDataDocuments = ExcludeRouteMetadataDocuments(dataDocuments, routeMetadata?.Source);
        var modules = await DataModuleBuilder.BuildModulesAsync(templateDataDocuments, language, bodyStore, cancellationToken).ConfigureAwait(false);
        var sourceData = await DataModuleBuilder.BuildDataBySourceAsync(dataDocuments, bodyStore, cancellationToken).ConfigureAwait(false);
        var dataIndex = DataModuleBuilder.BuildDataIndex(templateDataDocuments, sources);
        var routeMetadataIndex = routeMetadata is null
            ? null
            : RouteMetadataIndexBuilder.Build(routeMetadata, sourceData);
        return new DataModuleResult(dataDocuments, modules, sourceData, dataIndex, routeMetadataIndex);
    }

    internal static async Task<DataModuleResult> PrepareDataModulesStageAsync(
        BuildVariantContext context,
        BuildStageMetricsCollector metrics)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await PrepareDataModulesAsync(
            context.Documents,
            context.Config.Site.Language,
            context.BodyStore,
            context.Config.Content.Sources,
            context.Config.Content.RouteMetadata).ConfigureAwait(false);
        stopwatch.Stop();
        metrics.AddDuration("prepareContent", stopwatch.ElapsedMilliseconds);
        return result;
    }

    internal static SiteModel BuildSiteModel(
        AppConfig config,
        string baseUrl,
        IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? modules,
        IReadOnlyDictionary<string, object>? sourceData,
        IReadOnlyDictionary<string, object>? pluginData = null,
        IReadOnlyDictionary<string, object>? dataIndex = null,
        DateTimeOffset? buildStartedAt = null)
    {
        var reservedSource = config.Content.RouteMetadata?.Source;
        var data = ExcludeReservedSource(MergeSiteData(sourceData, pluginData), reservedSource);
        var buildInstant = buildStartedAt ?? DateTimeOffset.UtcNow;
        var buildTimezone = TimeZoneResolver.ResolveOrUtc(config.Site.Timezone);
        return new SiteModel
        {
            Name = config.Site.Name,
            Title = config.Site.Title,
            Url = config.Site.Url,
            Description = config.Site.Description,
            BaseUrl = baseUrl,
            Language = config.Site.Language,
            BuildYear = TimeZoneInfo.ConvertTime(buildInstant, buildTimezone).Year,
            Params = config.Theme.Params,
            Modules = ExcludeReservedSource(modules, reservedSource),
            Data = data,
            DataIndex = ExcludeReservedSource(dataIndex, reservedSource)
        };
    }

    private static IReadOnlyList<ContentDocument> ExcludeRouteMetadataDocuments(
        IReadOnlyList<ContentDocument> dataDocuments,
        string? reservedSource)
    {
        if (string.IsNullOrWhiteSpace(reservedSource))
        {
            return dataDocuments;
        }

        return dataDocuments
            .Where(document => !string.Equals(
                ContentFieldReader.GetText(document.CustomFields, "sourceKey")?.Trim(),
                reservedSource,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, TValue>? ExcludeReservedSource<TValue>(
        IReadOnlyDictionary<string, TValue>? values,
        string? reservedSource)
    {
        if (values is null || string.IsNullOrWhiteSpace(reservedSource) ||
            !values.Keys.Any(key => string.Equals(key, reservedSource, StringComparison.OrdinalIgnoreCase)))
        {
            return values;
        }

        var filtered = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            if (!string.Equals(key, reservedSource, StringComparison.OrdinalIgnoreCase))
            {
                filtered[key] = value;
            }
        }

        return filtered.Count == 0 ? null : filtered;
    }

    private static IReadOnlyDictionary<string, object>? MergeSiteData(
        IReadOnlyDictionary<string, object>? sourceData,
        IReadOnlyDictionary<string, object>? pluginData)
    {
        if ((sourceData is null || sourceData.Count == 0) &&
            (pluginData is null || pluginData.Count == 0))
        {
            return null;
        }

        var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (sourceData is not null)
        {
            foreach (var item in sourceData)
            {
                merged[item.Key] = item.Value;
            }
        }

        if (pluginData is not null)
        {
            foreach (var item in pluginData)
            {
                merged[item.Key] = item.Value;
            }
        }

        return merged;
    }
}
