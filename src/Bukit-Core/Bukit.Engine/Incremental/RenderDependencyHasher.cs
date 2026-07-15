using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Engine.RouteMetadata;
using Bukit.Engine.Analytics;

namespace Bukit.Engine.Incremental;

internal static class RenderDependencyHasher
{
    internal static string Compute(
        AppConfig config,
        SiteModel siteModel,
        BuildExecutionMode executionMode = BuildExecutionMode.Production)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Title);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Description);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.BaseUrl);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Language);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Url);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, siteModel.BuildYear.ToString(CultureInfo.InvariantCulture));
        hasher.AppendData(newline);

        if (config.Site.Languages is { Count: > 0 })
        {
            foreach (var lang in config.Site.Languages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, lang);
            }
        }

        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.DefaultLanguage);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.SitemapMode);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, SiteModeResolver.ResolveFeedMode(config.Site));
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, SiteModeResolver.ResolveSearchMode(config.Site));
        hasher.AppendData(newline);

        var resolvedAnalytics = AnalyticsConfigNormalizer.Normalize(config.Site.Analytics);
        AppendFramedValue(
            hasher,
            "analytics.pluginEnabled",
            AnalyticsBuildState.ResolvePluginEnabled(config.Site.Plugins).ToString(CultureInfo.InvariantCulture));
        AppendFramedValue(hasher, "analytics.enabled", resolvedAnalytics.Enabled.ToString(CultureInfo.InvariantCulture));
        AppendFramedValue(hasher, "analytics.productionOnly", resolvedAnalytics.ProductionOnly.ToString(CultureInfo.InvariantCulture));
        AppendFramedValue(hasher, "analytics.executionMode", executionMode.ToString());
        AppendFramedValue(
            hasher,
            "analytics.providerCount",
            resolvedAnalytics.Providers.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var provider in resolvedAnalytics.Providers)
        {
            AppendFramedValue(hasher, "analytics.provider.type", provider.Type);
            AppendFramedValue(hasher, "analytics.provider.key", provider.Key);
            foreach (var option in provider.Options.OrderBy(option => option.Key, StringComparer.Ordinal))
            {
                AppendFramedValue(hasher, $"analytics.provider.option.{option.Key}", option.Value);
            }
        }

        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.Enabled.ToString());
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.RenderMode);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.HomeTitleTemplate);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.PageTitleTemplate);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.TitleSeparator);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.DefaultImage);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.TwitterSite);
        hasher.AppendData(newline);

        AppendDictionary(hasher, config.Theme.Params);

        if (config.Theme.Shortcodes is { Count: > 0 })
        {
            foreach (var kv in config.Theme.Shortcodes.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Value);
            }
        }

        if (config.Theme.Components is { Count: > 0 })
        {
            foreach (var kv in config.Theme.Components.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Template);
                if (kv.Value.Props is { Count: > 0 })
                {
                    foreach (var pk in kv.Value.Props.OrderBy(x => x.Key, StringComparer.Ordinal))
                    {
                        hasher.AppendData(newline);
                        IncrementalBuildEngine.AppendUtf8(hasher, pk.Key);
                        hasher.AppendData(newline);
                        IncrementalBuildEngine.AppendUtf8(hasher, pk.Value);
                    }
                }
            }
        }

        IncrementalBuildEngine.AppendUtf8(hasher, config.Theme.ComponentValidation);
        hasher.AppendData(newline);

        IncrementalBuildEngine.AppendUtf8(hasher, config.Build.ListPageContentMode);
        hasher.AppendData(newline);

        AppendStableCollectionConfig(hasher, config);

        AppendStableTaxonomyConfig(hasher, config.Taxonomy);

        AppendRouteMetadataConfig(hasher, config.Content.RouteMetadata);

        if (config.Site.Plugins is { Count: > 0 })
        {
            foreach (var kv in config.Site.Plugins.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (string.Equals(kv.Key, "analytics", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Enabled.ToString());
            }
        }

        var reservedRouteMetadataSource = config.Content.RouteMetadata?.Source;
        AppendModuleSummary(hasher, siteModel.Modules, reservedRouteMetadataSource);
        AppendDataSummary(hasher, siteModel.Data, reservedRouteMetadataSource);
        AppendTopLevelDictionary(hasher, siteModel.DataIndex, reservedRouteMetadataSource);

        var digest = hasher.GetHashAndReset();
        return HashUtil.ToHexLower(digest);
    }

    internal static string ComputeForRoute(
        string baseHash,
        string metadataRouteUrl,
        IReadOnlyDictionary<string, RouteMetadataEntry>? routeMetadata)
    {
        if (routeMetadata is null || !routeMetadata.TryGetValue(metadataRouteUrl, out var metadata))
        {
            return baseHash;
        }

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasher, baseHash);
        AppendRouteMetadataValue(hasher, metadata);
        return HashUtil.ToHexLower(hasher.GetHashAndReset());
    }

    private static void AppendRouteMetadataConfig(IncrementalHash hasher, RouteMetadataConfig? config)
    {
        if (config is null)
        {
            return;
        }

        AppendFramedValue(hasher, "source", config.Source);
        AppendFramedValue(hasher, "routeField", config.RouteField);
        AppendFramedValue(hasher, "titleField", config.TitleField);
        AppendFramedValue(hasher, "summaryField", config.SummaryField);
        AppendFramedValue(hasher, "seoTitleField", config.SeoTitleField);
        AppendFramedValue(hasher, "seoDescriptionField", config.SeoDescriptionField);
        foreach (var route in config.RequiredRoutes.OrderBy(x => x, StringComparer.Ordinal))
        {
            AppendFramedValue(hasher, "requiredRoute", route);
        }
    }

    private static void AppendRouteMetadataValue(IncrementalHash hasher, RouteMetadataEntry metadata)
    {
        AppendFramedValue(hasher, "route", metadata.Route);
        AppendFramedValue(hasher, "title", metadata.Title);
        AppendFramedValue(hasher, "summary", metadata.Summary);
        AppendFramedValue(hasher, "seoTitle", metadata.SeoTitle);
        AppendFramedValue(hasher, "seoDescription", metadata.SeoDescription);
    }

    private static void AppendFramedValue(IncrementalHash hasher, string label, string? value)
    {
        IncrementalBuildEngine.AppendUtf8(hasher, label);
        IncrementalBuildEngine.AppendUtf8(hasher, ":");
        var byteLength = value is null ? -1 : Encoding.UTF8.GetByteCount(value);
        IncrementalBuildEngine.AppendUtf8(hasher, byteLength.ToString(CultureInfo.InvariantCulture));
        IncrementalBuildEngine.AppendUtf8(hasher, ":");
        if (value is not null)
        {
            IncrementalBuildEngine.AppendUtf8(hasher, value);
        }
    }

    private static void AppendDictionary(IncrementalHash hasher, IReadOnlyDictionary<string, object>? dict)
    {
        if (dict is null || dict.Count == 0)
        {
            return;
        }

        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        foreach (var kv in dict.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
            hasher.AppendData(newline);
            AppendObjectValue(hasher, kv.Value);
        }
    }

    private static void AppendTopLevelDictionary(
        IncrementalHash hasher,
        IReadOnlyDictionary<string, object>? dict,
        string? excludedKey)
    {
        if (dict is null || dict.Count == 0)
        {
            return;
        }

        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';
        foreach (var kv in dict.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.Equals(kv.Key, excludedKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
            hasher.AppendData(newline);
            AppendObjectValue(hasher, kv.Value);
        }
    }

    private static void AppendObjectValue(IncrementalHash hasher, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string s)
        {
            IncrementalBuildEngine.AppendUtf8(hasher, s);
            return;
        }

        if (value is bool b)
        {
            IncrementalBuildEngine.AppendUtf8(hasher, b.ToString());
            return;
        }

        if (value is int or long or float or double or decimal)
        {
            IncrementalBuildEngine.AppendUtf8(hasher, value.ToString());
            return;
        }

        if (value is IReadOnlyDictionary<string, object> dictionary)
        {
            AppendDictionary(hasher, dictionary);
            return;
        }

        IncrementalBuildEngine.AppendUtf8(hasher, value.ToString() ?? string.Empty);
    }

    private static void AppendModuleSummary(
        IncrementalHash hasher,
        IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? modules,
        string? excludedSource = null)
    {
        if (modules is null || modules.Count == 0)
        {
            return;
        }

        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        foreach (var kv in modules.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.Equals(kv.Key, excludedSource, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Count.ToString());
            foreach (var m in kv.Value.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, m.Id);
            }
        }
    }

    private static void AppendDataSummary(
        IncrementalHash hasher,
        IReadOnlyDictionary<string, object>? data,
        string? excludedSource = null)
    {
        if (data is null || data.Count == 0)
        {
            return;
        }

        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        foreach (var kv in data.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.Equals(kv.Key, excludedSource, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
        }
    }

    private static void AppendStableCollectionConfig(IncrementalHash hasher, AppConfig config)
    {
        var collections = config.Site.Collections;
        if (collections is null || collections.Count == 0) return;
        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';
        foreach (var kv in collections.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Permalink);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Template);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.ListRoute);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.ListTitle);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.ListDescription);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.ListTemplate);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.SchemaFailMode);
            var pagination = kv.Value.Pagination;
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, pagination.Enabled.ToString());
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, pagination.PageSize.ToString());
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, pagination.UrlPattern);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, pagination.FirstPageUsesListRoute.ToString());
            var output = kv.Value.Output;
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, output.Rss.ToString());
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, output.Sitemap.ToString());
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, output.Archive.ToString());
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, output.FeedPath);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, output.FeedTitle);
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, output.FeedDescription);
            if (output.ArchiveDetail is not null)
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, output.ArchiveDetail.Depth);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, output.ArchiveDetail.Template);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, output.ArchiveDetail.RoutePrefix);
            }
            if (kv.Value.FilteredLists is { Count: > 0 })
            {
                foreach (var fl in kv.Value.FilteredLists.OrderBy(x => x.Field, StringComparer.Ordinal))
                {
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.Field);
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.Operator);
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.Value);
                    if (fl.Values is { Count: > 0 })
                    {
                        foreach (var value in fl.Values.OrderBy(x => x, StringComparer.Ordinal))
                        {
                            hasher.AppendData(newline);
                            IncrementalBuildEngine.AppendUtf8(hasher, value);
                        }
                    }
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.ListRoute);
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.Title);
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.Description);
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.ListTemplate);
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.PageSize?.ToString());
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.UrlPattern);
                    hasher.AppendData(newline);
                    IncrementalBuildEngine.AppendUtf8(hasher, fl.EmptyBehavior);
                }
            }
        }

        AppendStableFieldScopes(hasher, ContentModelSchemaFactory.FromConfig(config).FieldScopes);
    }

    private static void AppendStableFieldScopes(
        IncrementalHash hasher,
        IReadOnlyDictionary<string, IReadOnlyList<CustomFieldDefinition>>? fieldScopes)
    {
        if (fieldScopes is null || fieldScopes.Count == 0) return;
        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';
        foreach (var scope in fieldScopes.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hasher.AppendData(newline);
            IncrementalBuildEngine.AppendUtf8(hasher, scope.Key);
            foreach (var field in scope.Value.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, field.Name);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, field.FieldType);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, field.Label);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, field.Format);
                if (field.Enum is { Count: > 0 })
                {
                    foreach (var value in field.Enum.OrderBy(x => x, StringComparer.Ordinal))
                    {
                        hasher.AppendData(newline);
                        IncrementalBuildEngine.AppendUtf8(hasher, value);
                    }
                }

                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, field.Min?.ToString());
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, field.Max?.ToString());
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, field.Required.ToString());
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, field.Default?.ToString());
            }
        }
    }

    private static void AppendStableTaxonomyConfig(IncrementalHash hasher, TaxonomyConfig taxonomy)
    {
        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.OutputMode);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.PageSize.ToString());
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.IndexEnabled.ToString());
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.PinField);
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, taxonomy.PinOrderField);
        if (taxonomy.ItemFields is { Count: > 0 })
        {
            foreach (var f in taxonomy.ItemFields.OrderBy(x => x, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, f);
            }
        }
        if (taxonomy.PinFieldBySource is { Count: > 0 })
        {
            foreach (var kvPair in taxonomy.PinFieldBySource.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kvPair.Key);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kvPair.Value);
            }
        }
        if (taxonomy.PinOrderFieldBySource is { Count: > 0 })
        {
            foreach (var kvPair in taxonomy.PinOrderFieldBySource.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kvPair.Key);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kvPair.Value);
            }
        }
        if (taxonomy.Kinds is { Count: > 0 })
        {
            foreach (var kind in taxonomy.Kinds.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.Key);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.Kind);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.Title);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.Description);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.SingularTitlePrefix);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.Template);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.IndexTemplate);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.TermTemplate);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.IndexEnabled?.ToString());
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.Hierarchical.ToString());
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.RoutePrefix);
            }
        }
    }
}
