using System.Security.Cryptography;
using Bukit.Config;
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

        if (config.Site.Collections is { Count: > 0 })
        {
            foreach (var kv in config.Site.Collections.OrderBy(x => x.Key, StringComparer.Ordinal))
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
            }
        }

        if (config.Taxonomy.Kinds is { Count: > 0 })
        {
            foreach (var kind in config.Taxonomy.Kinds.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kind.Key);
            }
        }

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

        if (config.Site.ExternalPlugins is { Count: > 0 })
        {
            foreach (var kv in config.Site.ExternalPlugins.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Key);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Runtime);
                hasher.AppendData(newline);
                IncrementalBuildEngine.AppendUtf8(hasher, kv.Value.Entry);
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
}
