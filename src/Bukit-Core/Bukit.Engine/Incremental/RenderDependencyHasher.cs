using System.Security.Cryptography;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

internal static class RenderDependencyHasher
{
    internal static string Compute(AppConfig config, SiteModel siteModel)
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

        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Analytics.Enabled.ToString());
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Analytics.GoogleAnalyticsId);
        hasher.AppendData(newline);

        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.Enabled.ToString());
        hasher.AppendData(newline);
        IncrementalBuildEngine.AppendUtf8(hasher, config.Site.Seo.RenderMode);
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

        if (config.Site.Plugins is { Count: > 0 })
        {
            foreach (var kv in config.Site.Plugins.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Enabled.ToString());
            }
        }

        AppendModuleSummary(hasher, siteModel.Modules);
        AppendDataSummary(hasher, siteModel.Data);

        var digest = hasher.GetHashAndReset();
        return HashUtil.ToHexLower(digest);
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

        IncrementalBuildEngine.AppendUtf8(hasher, value.ToString() ?? string.Empty);
    }

    private static void AppendModuleSummary(IncrementalHash hasher, IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? modules)
    {
        if (modules is null || modules.Count == 0)
        {
            return;
        }

        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        foreach (var kv in modules.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
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

    private static void AppendDataSummary(IncrementalHash hasher, IReadOnlyDictionary<string, object>? data)
    {
        if (data is null || data.Count == 0)
        {
            return;
        }

        Span<byte> newline = stackalloc byte[1];
        newline[0] = (byte)'\n';

        foreach (var kv in data.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
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
