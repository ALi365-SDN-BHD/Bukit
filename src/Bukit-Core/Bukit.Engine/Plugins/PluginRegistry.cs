using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Analytics;
using Bukit.Shared;

namespace Bukit.Engine.Plugins;

internal interface IPluginSource
{
    IEnumerable<IBukitPlugin> GetPlugins();
}

internal sealed class BuiltInPluginSource : IPluginSource
{
    private readonly AppConfig _config;
    private readonly AnalyticsBuildState _analyticsBuildState;

    internal BuiltInPluginSource(AppConfig config)
        : this(
            config,
            AnalyticsBuildState.Create(
                config,
                BuildExecutionMode.Production))
    {
    }

    internal BuiltInPluginSource(
        AppConfig config,
        AnalyticsBuildState analyticsBuildState)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(analyticsBuildState);
        _config = config;
        _analyticsBuildState = analyticsBuildState;
    }

    public IEnumerable<IBukitPlugin> GetPlugins()
    {
        yield return new BuiltIn.AnalyticsPlugin(_config, _analyticsBuildState);
        yield return new BuiltIn.DataFilesPlugin(_config);
        yield return new BuiltIn.PagesIndexPlugin(_config);
        yield return new BuiltIn.TaxonomyPlugin(_config);
        yield return new BuiltIn.PaginationPlugin(_config);
        yield return new BuiltIn.ArchivePlugin(_config);
        yield return new BuiltIn.RelatedContentPlugin(_config);
        yield return new BuiltIn.AliasPlugin(_config);
        yield return new BuiltIn.MenuPlugin(_config);
        yield return new BuiltIn.ImageProcessingPlugin(_config);
    }
}

public static class PluginRegistry
{
    private static readonly AppConfig CompatibilityConfig = new()
    {
        Site = new SiteConfig
        {
            Name = "plugin-compatibility",
            Title = "Plugin Compatibility"
        },
        Content = new ContentConfig()
    };

    private static int _registrationBuildCount;

    public static IEnumerable<(IBukitPlugin Plugin, string Source)> GetAllPlugins(BuildContext context)
        => GetAllPlugins(context, PluginExecutionSession.CreateCompatibility());

    internal static AppConfig CompatibilityConfiguration => CompatibilityConfig;

    internal static IEnumerable<(IBukitPlugin Plugin, string Source)> GetAllPlugins(
        BuildContext context,
        PluginExecutionSession session)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(session);
        foreach (var item in session.Registrations)
        {
            yield return item;
        }
    }

    internal static IReadOnlyList<(IBukitPlugin Plugin, string Source)> BuildPlugins(
        AppConfig config,
        AnalyticsBuildState analyticsBuildState)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(IBukitPlugin Plugin, string Source)>();

        var sources = new (IPluginSource Source, string Name)[]
        {
            (new BuiltInPluginSource(config, analyticsBuildState), "built-in")
        };

        foreach (var (source, name) in sources)
        {
            foreach (var plugin in source.GetPlugins())
            {
                if (plugin is not null)
                {
                    var key = $"{plugin.Name}@{plugin.Version}";
                    if (seen.Add(key))
                    {
                        result.Add((plugin, name));
                    }
                }
            }
        }

        Interlocked.Increment(ref _registrationBuildCount);
        return result;
    }

    internal static int RegistrationBuildCountForTests => _registrationBuildCount;

    internal static void ResetBuildCountForTests()
    {
        _registrationBuildCount = 0;
    }
}
