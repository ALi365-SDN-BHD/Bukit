using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// The default language occupies the output root, so its first path segment must not
/// shadow another configured language code.
/// </summary>
internal static class I18nReservedPrefixGuard
{
    internal static void ValidateRoutes(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> routedDocuments,
        IReadOnlyList<RoutedContentDocument> derivedDocuments,
        IReadOnlyList<RouteInfo> listRoutes)
    {
        var reserved = ReservedPrefixes(config);
        if (reserved.Count == 0)
        {
            return;
        }

        foreach (var routed in routedDocuments)
        {
            Check(routed.Route.OutputPath, $"document '{routed.Document.Id}'", reserved);
        }

        foreach (var derived in derivedDocuments)
        {
            Check(derived.Route.OutputPath, $"derived document '{derived.Document.Id}'", reserved);
        }

        foreach (var route in listRoutes)
        {
            Check(route.OutputPath, $"list route '{route.Url}'", reserved);
        }
    }

    internal static void ValidateStaticRoutes(AppConfig config, IReadOnlyList<RouteInfo> routes)
    {
        var reserved = ReservedPrefixes(config);
        foreach (var route in routes)
        {
            Check(route.OutputPath, $"static route '{route.Url}'", reserved);
        }
    }

    internal static void ValidateAssetPlan(AppConfig config, IReadOnlyList<AssetOutputItem> items)
    {
        var reserved = ReservedPrefixes(config);
        if (reserved.Count == 0)
        {
            return;
        }

        foreach (var item in items.Where(item =>
                     item.Category is AssetOutputCategory.Static or AssetOutputCategory.Assets or AssetOutputCategory.Media))
        {
            Check(item.Destination, $"asset '{item.Source}'", reserved);
        }
    }

    internal static void ValidatePluginOutputs(AppConfig config, IReadOnlyList<PluginOutputTrackingInfo> outputs)
    {
        var reserved = ReservedPrefixes(config);
        foreach (var output in outputs)
        {
            Check(output.Path, $"plugin '{output.Plugin}' output", reserved);
        }
    }

    internal static void ValidateWorktreeOutputs(IReadOnlyList<string> paths, IReadOnlyList<string> reserved)
    {
        foreach (var path in paths)
        {
            Check(path, "worktree result", reserved);
        }
    }

    internal static Exception SelectPrimaryFailure(
        IReadOnlyDictionary<string, Exception> failures,
        string defaultLanguage)
    {
        if (failures.TryGetValue(defaultLanguage, out var rootLanguageFailure) &&
            IsReservedPrefixFailure(rootLanguageFailure))
        {
            return rootLanguageFailure;
        }

        return failures
            .OrderBy(failure => string.Equals(failure.Key, defaultLanguage, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(failure => failure.Key, StringComparer.OrdinalIgnoreCase)
            .First()
            .Value;
    }

    private static bool IsReservedPrefixFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is BukitException { Code: DiagnosticCode.I18nRouteReservedLanguagePrefix } ||
                current.Message.Contains("i18n-route-reserved-language-prefix", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static string? FindReservedPrefix(string relativePath, IReadOnlyList<string> reserved)
    {
        var normalized = BuildPathUtils.NormalizeRelPath(relativePath).TrimStart('/');
        if (normalized.Length == 0)
        {
            return null;
        }

        var first = normalized.Split('/')[0];
        return reserved.FirstOrDefault(code => string.Equals(code, first, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ReservedPrefixes(AppConfig config)
        => I18nOutputLayout.Create(
                config.Site,
                I18nOutputMerger.GetLanguages(config.Site),
                config.Site.BaseUrl)
            .ReservedPrefixes;

    private static void Check(string path, string source, IReadOnlyList<string> reserved)
    {
        var prefix = FindReservedPrefix(path, reserved);
        if (prefix is not null)
        {
            throw new BukitException(
                $"Default language output '{BuildPathUtils.NormalizeRelPath(path)}' from {source} uses reserved language prefix '{prefix}'.",
                DiagnosticCode.I18nRouteReservedLanguagePrefix);
        }
    }
}
