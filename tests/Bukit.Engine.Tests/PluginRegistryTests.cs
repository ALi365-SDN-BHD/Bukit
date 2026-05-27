using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginRegistryTests
{
    [Fact]
    public void GetAllPlugins_ReturnsNonEmptyList()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugins = PluginRegistry.GetAllPlugins(ctx).ToList();

        Assert.NotEmpty(plugins);
        Assert.All(plugins, x => Assert.NotEmpty(x.Plugin.Name));
        Assert.All(plugins, x => Assert.NotEmpty(x.Plugin.Version));
    }

    [Fact]
    public void GetAllPlugins_ReturnsPluginsWithSources()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugins = PluginRegistry.GetAllPlugins(ctx).ToList();

        var builtInPlugins = plugins.Where(x => x.Source == "built-in").ToList();
        Assert.NotEmpty(builtInPlugins);
        Assert.Contains(builtInPlugins, x => x.Plugin.Name == "pages-index");
    }

    [Fact]
    public void GetAllPlugins_NoDuplicateNames()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugins = PluginRegistry.GetAllPlugins(ctx).ToList();

        var keys = plugins.Select(x => $"{x.Plugin.Name}@{x.Plugin.Version}").ToList();
        Assert.Equal(keys.Distinct(StringComparer.OrdinalIgnoreCase).Count(), keys.Count);
    }

    [Fact]
    public void GetAllPlugins_UsesCache_OnSecondCall()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var firstCallCount = PluginRegistry.CacheBuildCountForTests;
        var first = PluginRegistry.GetAllPlugins(ctx).ToList();
        var afterFirstCallCount = PluginRegistry.CacheBuildCountForTests;

        var second = PluginRegistry.GetAllPlugins(ctx).ToList();
        var afterSecondCallCount = PluginRegistry.CacheBuildCountForTests;

        Assert.Equal(firstCallCount + 1, afterFirstCallCount);
        Assert.Equal(afterFirstCallCount, afterSecondCallCount);
        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public void GetAllPlugins_ContainsKnownBuiltInPlugins()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugins = PluginRegistry.GetAllPlugins(ctx).ToList();
        var names = plugins.Select(x => x.Plugin.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("pages-index", names);
        Assert.Contains("taxonomy", names);
        Assert.Contains("sitemap", names);
        Assert.Contains("search-index", names);
        Assert.Contains("pagination", names);
        Assert.Contains("archive", names);
        Assert.Contains("feed", names);
        Assert.Contains("related-content", names);
        Assert.Contains("alias", names);
        Assert.Contains("data-files", names);
        Assert.Contains("menu", names);
        Assert.Contains("image-processing", names);
    }

    [Fact]
    public void GetAllPlugins_DifferentContexts_BuildSeparateCaches()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx1 = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
        var ctx2 = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test2", Title = "test2" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var countBefore = PluginRegistry.CacheBuildCountForTests;
        var plugins1 = PluginRegistry.GetAllPlugins(ctx1).ToList();
        var countAfter1 = PluginRegistry.CacheBuildCountForTests;

        var plugins2 = PluginRegistry.GetAllPlugins(ctx2).ToList();
        var countAfter2 = PluginRegistry.CacheBuildCountForTests;

        Assert.Equal(countBefore + 1, countAfter1);
        Assert.Equal(countAfter1 + 1, countAfter2);
    }

    [Fact]
    public void GetAllPlugins_EachPluginHasValidSource()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugins = PluginRegistry.GetAllPlugins(ctx).ToList();

        var validSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "built-in", "generated", "external", "external-protocol"
        };
        Assert.All(plugins, x => Assert.True(validSources.Contains(x.Source), $"Source '{x.Source}' is not valid for plugin '{x.Plugin.Name}'"));
    }

    [Fact]
    public void ResetCacheForTests_ResetsBuildCount()
    {
        PluginRegistry.ResetCacheForTests();
        Assert.Equal(0, PluginRegistry.CacheBuildCountForTests);

        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        _ = PluginRegistry.GetAllPlugins(ctx).ToList();
        Assert.True(PluginRegistry.CacheBuildCountForTests > 0);

        PluginRegistry.ResetCacheForTests();
        Assert.Equal(0, PluginRegistry.CacheBuildCountForTests);
    }
}
