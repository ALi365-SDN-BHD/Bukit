using Bukit.Content;
using Bukit.Routing;
using System.Diagnostics;

using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins;

public static class PluginRunner
{
    public static IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> RunDerivePages(BuildContext context)
        => RunDerivePagesAsync(context).GetAwaiter().GetResult();

    public static async Task<IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>> RunDerivePagesAsync(
        BuildContext context,
        CancellationToken cancellationToken = default)
    {
        var derived = new List<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>();
        var usedRouteUrls = new HashSet<string>(context.Routed.Select(x => NormalizeUrl(x.Route.Url)), StringComparer.OrdinalIgnoreCase);
        var usedOutputPaths = new HashSet<string>(context.Routed.Select(x => NormalizeOutputPath(x.Route.OutputPath)), StringComparer.OrdinalIgnoreCase);
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
                    _ => Array.Empty<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>()
                };

                if (pages.Count > 0)
                {
                    var acceptedPages = ApplyDeriveConflictPolicy(
                        context,
                        plugin.Name,
                        pages,
                        usedRouteUrls,
                        usedOutputPaths,
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

    private static IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> ApplyDeriveConflictPolicy(
        BuildContext context,
        string pluginName,
        IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> pages,
        HashSet<string> usedRouteUrls,
        HashSet<string> usedOutputPaths,
        string deriveConflictPolicy)
    {
        var acceptedPages = new List<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>();
        foreach (var page in pages)
        {
            var normalizedUrl = NormalizeUrl(page.Route.Url);
            var normalizedOutputPath = NormalizeOutputPath(page.Route.OutputPath);
            var urlTaken = !usedRouteUrls.Add(normalizedUrl);
            var outputTaken = !usedOutputPaths.Add(normalizedOutputPath);
            if (!urlTaken && !outputTaken)
            {
                acceptedPages.Add(page);
                continue;
            }

            if (!urlTaken)
            {
                usedRouteUrls.Remove(normalizedUrl);
            }

            if (!outputTaken)
            {
                usedOutputPaths.Remove(normalizedOutputPath);
            }

            var conflictTarget = urlTaken
                ? $"url: {page.Route.Url}"
                : $"outputPath: {page.Route.OutputPath}";
            var message = $"Plugin '{pluginName}' derive-pages route conflict on {conflictTarget}";

            if (deriveConflictPolicy == "last-wins")
            {
                acceptedPages.Add(page);
                continue;
            }

            if (deriveConflictPolicy == "warn")
            {
                context.Logger.Warn($"{message}. skipped by deriveConflictPolicy=warn.");
                continue;
            }

            throw new InvalidOperationException(message);
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
