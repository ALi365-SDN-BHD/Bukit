using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using System.Diagnostics;
using Bukit.Shared;

using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins;

public static class PluginRunner
{
    public static IReadOnlyList<string> CollectTemplateRequirementKinds(BuildContext context)
    {
        var kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (plugin, _) in GetOrderedPlugins(context))
        {
            if (!IsPluginEnabled(context, plugin.Name))
            {
                continue;
            }

            if (plugin is not ITemplateRequirementPlugin templateRequirements)
            {
                continue;
            }

            foreach (var kind in templateRequirements.GetTemplateRequirementKinds(context))
            {
                if (!string.IsNullOrWhiteSpace(kind))
                {
                    kinds.Add(kind.Trim());
                }
            }
        }

        return kinds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<RoutedContentDocument> RunDerivePages(BuildContext context)
        => RunDerivePagesAsync(context).GetAwaiter().GetResult();

    public static async Task<IReadOnlyList<RoutedContentDocument>> RunDerivePagesAsync(
        BuildContext context,
        CancellationToken cancellationToken = default)
    {
        var derived = new List<RoutedContentDocument>();
        var contentRouteUrls = new HashSet<string>(context.RoutedDocuments.Select(x => NormalizeUrl(x.Route.Url)), StringComparer.OrdinalIgnoreCase);
        var contentOutputPaths = new HashSet<string>(context.RoutedDocuments.Select(x => NormalizeOutputPath(x.Route.OutputPath)), StringComparer.OrdinalIgnoreCase);
        var usedRouteUrls = new HashSet<string>(contentRouteUrls, StringComparer.OrdinalIgnoreCase);
        var usedOutputPaths = new HashSet<string>(contentOutputPaths, StringComparer.OrdinalIgnoreCase);
        var deriveConflictPolicy = (context.Config.Site.DeriveConflictPolicy ?? "fail").Trim().ToLowerInvariant();
        var warnOnPluginFailure = string.Equals(context.Config.Site.PluginFailMode, "warn", StringComparison.OrdinalIgnoreCase);

        foreach (var (plugin, _) in GetOrderedPlugins(context))
        {
            if (!IsPluginEnabled(context, plugin.Name))
            {
                continue;
            }

            if (plugin is IHookFilterPlugin hookFilter && !hookFilter.SupportsHook("derive-pages"))
            {
                continue;
            }

            if (plugin is not IDerivePagesPlugin && plugin is not IDerivePagesAsyncPlugin)
            {
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var pages = plugin switch
                {
                    IDerivePagesAsyncPlugin deriveAsync => await deriveAsync.DerivePagesAsync(context, cancellationToken),
                    IDerivePagesPlugin derive => derive.DerivePages(context),
                    _ => Array.Empty<RoutedContentDocument>()
                };

                if (pages.Count > 0)
                {
                    var acceptedPages = ApplyDeriveConflictPolicy(
                        context,
                        plugin.Name,
                        pages,
                        derived,
                        usedRouteUrls,
                        usedOutputPaths,
                        contentRouteUrls,
                        contentOutputPaths,
                        deriveConflictPolicy);
                    if (acceptedPages.Count > 0)
                    {
                        derived.AddRange(acceptedPages);
                    }
                }

                sw.Stop();
                context.PluginExecutions.Add(new PluginExecutionInfo(plugin.Name, "derive-pages", sw.ElapsedMilliseconds, true, null));
                context.Logger.Info($"plugin {plugin.Name} derive-pages {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.PluginExecutions.Add(new PluginExecutionInfo(plugin.Name, "derive-pages", sw.ElapsedMilliseconds, false, ex.Message));
                context.Logger.Error($"plugin {plugin.Name} derive-pages failed: {ex.Message}");
                if (!warnOnPluginFailure)
                {
                    throw;
                }
            }
        }

        return derived;
    }

    private static IReadOnlyList<RoutedContentDocument> ApplyDeriveConflictPolicy(
        BuildContext context,
        string pluginName,
        IReadOnlyList<RoutedContentDocument> pages,
        List<RoutedContentDocument> derived,
        HashSet<string> usedRouteUrls,
        HashSet<string> usedOutputPaths,
        HashSet<string> contentRouteUrls,
        HashSet<string> contentOutputPaths,
        string deriveConflictPolicy)
    {
        var acceptedPages = new List<RoutedContentDocument>();
        foreach (var page in pages)
        {
            var normalizedUrl = NormalizeUrl(page.Route.Url);
            var normalizedOutputPath = NormalizeOutputPath(page.Route.OutputPath);

            var urlInAll = usedRouteUrls.Contains(normalizedUrl);
            var outputInAll = usedOutputPaths.Contains(normalizedOutputPath);
            var urlInContent = contentRouteUrls.Contains(normalizedUrl);
            var outputInContent = contentOutputPaths.Contains(normalizedOutputPath);

            var hasConflict = urlInAll || outputInAll;

            if (!hasConflict)
            {
                usedRouteUrls.Add(normalizedUrl);
                usedOutputPaths.Add(normalizedOutputPath);
                acceptedPages.Add(page);
                continue;
            }

            var conflictTarget = urlInAll
                ? $"url: {page.Route.Url}"
                : $"outputPath: {page.Route.OutputPath}";
            var message = $"Plugin '{pluginName}' derive-pages route conflict on {conflictTarget}";

            if (deriveConflictPolicy == "last-wins")
            {
                var isContentConflict = urlInContent || outputInContent;

                if (isContentConflict)
                {
                    context.Logger.Warn($"{message}. Content route preserved by last-wins.");
                    continue;
                }

                var conflictingIndices = new HashSet<int>();
                for (int i = 0; i < derived.Count; i++)
                {
                    var d = derived[i];
                    if (string.Equals(NormalizeUrl(d.Route.Url), normalizedUrl, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(NormalizeOutputPath(d.Route.OutputPath), normalizedOutputPath, StringComparison.OrdinalIgnoreCase))
                    {
                        conflictingIndices.Add(i);
                    }
                }

                for (int i = derived.Count - 1; i >= 0; i--)
                {
                    if (!conflictingIndices.Contains(i))
                    {
                        continue;
                    }

                    var old = derived[i];
                    var oldUrl = NormalizeUrl(old.Route.Url);
                    var oldOutputPath = NormalizeOutputPath(old.Route.OutputPath);

                    if (!string.Equals(oldUrl, normalizedUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        usedRouteUrls.Remove(oldUrl);
                    }

                    if (!string.Equals(oldOutputPath, normalizedOutputPath, StringComparison.OrdinalIgnoreCase))
                    {
                        usedOutputPaths.Remove(oldOutputPath);
                    }

                    context.Logger.Info($"Derive conflict: {page.Route.Url} [{pluginName}] replaces {old.Route.Url} (last-wins)");
                    derived.RemoveAt(i);
                }

                usedRouteUrls.Add(normalizedUrl);
                usedOutputPaths.Add(normalizedOutputPath);
                acceptedPages.Add(page);
                continue;
            }

            if (deriveConflictPolicy == "warn")
            {
                context.Logger.Warn($"{message}. skipped by deriveConflictPolicy=warn.");
                continue;
            }

            throw new ConfigException(message, DiagnosticCode.PluginExecutionFailed);
        }

        return acceptedPages;
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "/";
        }

        var normalized = url.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        if (!string.Equals(normalized, "/", StringComparison.Ordinal) && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private static string NormalizeOutputPath(string outputPath)
    {
        return (outputPath ?? string.Empty).Replace('\\', '/').Trim();
    }

    public static void RunAfterBuild(BuildContext context)
        => RunAfterBuildAsync(context).GetAwaiter().GetResult();

    public static async Task RunAfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        var warnOnPluginFailure = string.Equals(context.Config.Site.PluginFailMode, "warn", StringComparison.OrdinalIgnoreCase);

        foreach (var (plugin, _) in GetOrderedPlugins(context))
        {
            if (!IsPluginEnabled(context, plugin.Name))
            {
                continue;
            }

            if (plugin is IHookFilterPlugin hookFilter && !hookFilter.SupportsHook("after-build"))
            {
                continue;
            }

            if (plugin is not IAfterBuildPlugin && plugin is not IAfterBuildAsyncPlugin)
            {
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                switch (plugin)
                {
                    case IAfterBuildAsyncPlugin afterAsync:
                        await afterAsync.AfterBuildAsync(context, cancellationToken);
                        break;
                    case IAfterBuildPlugin after:
                        after.AfterBuild(context);
                        break;
                }
                sw.Stop();
                context.PluginExecutions.Add(new PluginExecutionInfo(plugin.Name, "after-build", sw.ElapsedMilliseconds, true, null));
                context.Logger.Info($"plugin {plugin.Name} after-build {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.PluginExecutions.Add(new PluginExecutionInfo(plugin.Name, "after-build", sw.ElapsedMilliseconds, false, ex.Message));
                context.Logger.Error($"plugin {plugin.Name} after-build failed: {ex.Message}");
                if (!warnOnPluginFailure)
                {
                    throw;
                }
            }
        }
    }

    private static bool IsPluginEnabled(BuildContext context, string name)
    {
        if (context.Config.Site.Plugins is null || string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        if (context.Config.Site.Plugins.TryGetValue(name, out var cfg))
        {
            return cfg.Enabled;
        }

        return true;
    }

    private static IEnumerable<(IBukitPlugin Plugin, string Source)> GetOrderedPlugins(BuildContext context)
    {
        return PluginRegistry.GetAllPlugins(context)
            .OrderBy(x => GetOrder(x.Plugin))
            .ThenBy(x => x.Plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Plugin.Version, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetOrder(IBukitPlugin plugin)
    {
        return plugin is IOrderedPlugin ordered ? ordered.Order : 0;
    }
}
