using Bukit.Content;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using System.Diagnostics;
using Bukit.Shared;

using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins;

public static class PluginRunner
{
    internal static CollectedHtmlTransforms CollectHtmlTransforms(
        BuildContext context,
        BuildExecutionMode executionMode)
        => CollectHtmlTransforms(
            context,
            PluginExecutionSession.CreateCompatibility(executionMode),
            executionMode);

    internal static CollectedHtmlTransforms CollectHtmlTransforms(
        BuildContext context,
        AppConfig config,
        BuildExecutionMode executionMode)
        => CollectHtmlTransforms(
            context,
            PluginExecutionSession.Create(config, executionMode),
            executionMode);

    internal static CollectedHtmlTransforms CollectHtmlTransforms(
        BuildContext context,
        PluginExecutionSession session,
        BuildExecutionMode executionMode)
        => CollectHtmlTransforms(
            context,
            executionMode,
            session.Policy,
            session.Registrations.Select(item => item.Plugin));

    internal static CollectedHtmlTransforms CollectHtmlTransforms(
        BuildContext context,
        BuildExecutionMode executionMode,
        IEnumerable<IBukitPlugin> plugins)
        => CollectHtmlTransforms(
            context,
            executionMode,
            PluginExecutionPolicy.From(PluginRegistry.CompatibilityConfiguration.Site),
            plugins);

    internal static CollectedHtmlTransforms CollectHtmlTransforms(
        BuildContext context,
        BuildExecutionMode executionMode,
        PluginExecutionPolicy policy,
        IEnumerable<IBukitPlugin> plugins)
    {
        var transforms = new List<TrackedHtmlTransform>();

        foreach (var plugin in plugins
                     .OrderBy(GetOrder)
                     .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase))
        {
            if (!policy.IsPluginEnabled(plugin.Name))
            {
                continue;
            }

            if (plugin is IHookFilterPlugin hookFilter &&
                !hookFilter.SupportsHook(HtmlTransformHooks.HtmlTransform))
            {
                continue;
            }

            if (plugin is not IHtmlTransformPlugin htmlTransformPlugin)
            {
                continue;
            }

            var transform = htmlTransformPlugin.CreateHtmlTransform(
                new HtmlTransformPluginContext(context, executionMode));
            transforms.Add(new TrackedHtmlTransform(
                plugin.Name,
                transform,
                policy.WarnOnPluginFailure,
                context));
        }

        return new CollectedHtmlTransforms(context, transforms);
    }

    public static IReadOnlyList<string> CollectTemplateRequirementKinds(BuildContext context)
        => CollectTemplateRequirementKinds(
            context,
            PluginExecutionSession.CreateCompatibility());

    internal static IReadOnlyList<string> CollectTemplateRequirementKinds(
        BuildContext context,
        AppConfig config)
        => CollectTemplateRequirementKinds(
            context,
            PluginExecutionSession.Create(config, BuildExecutionMode.Production));

    internal static IReadOnlyList<string> CollectTemplateRequirementKinds(
        BuildContext context,
        PluginExecutionSession session)
        => CollectTemplateRequirementKinds(
            context,
            session.Policy,
            session.Registrations);

    internal static IReadOnlyList<string> CollectTemplateRequirementKinds(
        BuildContext context,
        PluginExecutionPolicy policy,
        IEnumerable<(IBukitPlugin Plugin, string Source)> plugins)
    {
        var kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (plugin, _) in GetOrderedPlugins(plugins))
        {
            if (!policy.IsPluginEnabled(plugin.Name))
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
        => RunDerivePagesAsync(
                context,
                PluginExecutionSession.CreateCompatibility())
            .GetAwaiter()
            .GetResult();

    public static async Task<IReadOnlyList<RoutedContentDocument>> RunDerivePagesAsync(
        BuildContext context,
        CancellationToken cancellationToken = default)
        => await RunDerivePagesAsync(
            context,
            PluginExecutionSession.CreateCompatibility(),
            cancellationToken);

    internal static Task<IReadOnlyList<RoutedContentDocument>> RunDerivePagesAsync(
        BuildContext context,
        AppConfig config,
        CancellationToken cancellationToken = default)
        => RunDerivePagesAsync(
            context,
            PluginExecutionSession.Create(config, BuildExecutionMode.Production),
            cancellationToken);

    internal static Task<IReadOnlyList<RoutedContentDocument>> RunDerivePagesAsync(
        BuildContext context,
        PluginExecutionSession session,
        CancellationToken cancellationToken = default)
        => RunDerivePagesAsync(
            context,
            session.Policy,
            session.Registrations,
            cancellationToken);

    internal static async Task<IReadOnlyList<RoutedContentDocument>> RunDerivePagesAsync(
        BuildContext context,
        PluginExecutionPolicy policy,
        CancellationToken cancellationToken = default)
        => await RunDerivePagesAsync(
            context,
            policy,
            PluginExecutionSession.CreateCompatibility().Registrations,
            cancellationToken);

    internal static async Task<IReadOnlyList<RoutedContentDocument>> RunDerivePagesAsync(
        BuildContext context,
        PluginExecutionPolicy policy,
        IEnumerable<(IBukitPlugin Plugin, string Source)> plugins,
        CancellationToken cancellationToken)
    {
        var derived = new List<RoutedContentDocument>();
        var contentRouteUrls = new HashSet<string>(context.RoutedDocuments.Select(x => NormalizeUrl(x.Route.Url)), StringComparer.OrdinalIgnoreCase);
        var contentOutputPaths = new HashSet<string>(context.RoutedDocuments.Select(x => NormalizeOutputPath(x.Route.OutputPath)), StringComparer.OrdinalIgnoreCase);
        var usedRouteUrls = new HashSet<string>(contentRouteUrls, StringComparer.OrdinalIgnoreCase);
        var usedOutputPaths = new HashSet<string>(contentOutputPaths, StringComparer.OrdinalIgnoreCase);

        foreach (var (plugin, _) in GetOrderedPlugins(plugins))
        {
            if (!policy.IsPluginEnabled(plugin.Name))
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
                cancellationToken.ThrowIfCancellationRequested();
                var pages = plugin switch
                {
                    IDerivePagesAsyncPlugin deriveAsync => await deriveAsync.DerivePagesAsync(context, cancellationToken),
                    IDerivePagesPlugin derive => derive.DerivePages(context),
                    _ => Array.Empty<RoutedContentDocument>()
                };

                cancellationToken.ThrowIfCancellationRequested();

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
                        policy.DeriveConflictPolicy);
                    if (acceptedPages.Count > 0)
                    {
                        derived.AddRange(acceptedPages);
                    }
                }

                sw.Stop();
                context.PluginExecutions.Add(new PluginExecutionInfo(plugin.Name, "derive-pages", sw.ElapsedMilliseconds, true, null));
                context.Logger.Info($"plugin {plugin.Name} derive-pages {sw.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.PluginExecutions.Add(new PluginExecutionInfo(plugin.Name, "derive-pages", sw.ElapsedMilliseconds, false, ex.Message));
                context.Logger.Error($"plugin {plugin.Name} derive-pages failed: {ex.Message}");
                if (!policy.WarnOnPluginFailure)
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
        => RunAfterBuildAsync(
                context,
                PluginExecutionSession.CreateCompatibility())
            .GetAwaiter()
            .GetResult();

    public static async Task RunAfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default)
        => await RunAfterBuildAsync(
            context,
            PluginExecutionSession.CreateCompatibility(),
            cancellationToken);

    internal static Task RunAfterBuildAsync(
        BuildContext context,
        AppConfig config,
        CancellationToken cancellationToken = default)
        => RunAfterBuildAsync(
            context,
            PluginExecutionSession.Create(config, BuildExecutionMode.Production),
            cancellationToken);

    internal static Task RunAfterBuildAsync(
        BuildContext context,
        PluginExecutionSession session,
        CancellationToken cancellationToken = default)
        => RunAfterBuildAsync(
            context,
            session.Policy,
            session.Registrations,
            cancellationToken);

    internal static async Task RunAfterBuildAsync(
        BuildContext context,
        PluginExecutionPolicy policy,
        CancellationToken cancellationToken = default)
        => await RunAfterBuildAsync(
            context,
            policy,
            PluginExecutionSession.CreateCompatibility().Registrations,
            cancellationToken);

    internal static async Task RunAfterBuildAsync(
        BuildContext context,
        PluginExecutionPolicy policy,
        IEnumerable<(IBukitPlugin Plugin, string Source)> plugins,
        CancellationToken cancellationToken)
    {
        foreach (var (plugin, _) in GetOrderedPlugins(plugins))
        {
            if (!policy.IsPluginEnabled(plugin.Name))
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
                cancellationToken.ThrowIfCancellationRequested();
                switch (plugin)
                {
                    case IAfterBuildAsyncPlugin afterAsync:
                        await afterAsync.AfterBuildAsync(context, cancellationToken);
                        break;
                    case IAfterBuildPlugin after:
                        after.AfterBuild(context);
                        break;
                }
                cancellationToken.ThrowIfCancellationRequested();
                sw.Stop();
                context.PluginExecutions.Add(new PluginExecutionInfo(plugin.Name, "after-build", sw.ElapsedMilliseconds, true, null));
                context.Logger.Info($"plugin {plugin.Name} after-build {sw.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                context.PluginExecutions.Add(new PluginExecutionInfo(plugin.Name, "after-build", sw.ElapsedMilliseconds, false, ex.Message));
                context.Logger.Error($"plugin {plugin.Name} after-build failed: {ex.Message}");
                if (!policy.WarnOnPluginFailure)
                {
                    throw;
                }
            }
        }
    }

    private static IEnumerable<(IBukitPlugin Plugin, string Source)> GetOrderedPlugins(
        IEnumerable<(IBukitPlugin Plugin, string Source)> plugins)
    {
        return plugins
            .OrderBy(x => GetOrder(x.Plugin))
            .ThenBy(x => x.Plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Plugin.Version, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetOrder(IBukitPlugin plugin)
    {
        return plugin is IOrderedPlugin ordered ? ordered.Order : 0;
    }
}
