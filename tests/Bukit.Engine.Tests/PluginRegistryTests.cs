using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Engine.Plugins.BuiltIn;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginRegistryTests
{
    [Fact]
    public void BuiltInPluginSource_RegistersExactlyOneAnalyticsPluginWithLockedMetadata()
    {
        var plugins = new BuiltInPluginSource(CreateContext().Config).GetPlugins().ToList();
        var analytics = Assert.Single(plugins, x => x.Name == "analytics");

        var typed = Assert.IsType<AnalyticsPlugin>(analytics);
        Assert.Equal("1.0.0", typed.Version);
        Assert.Equal(1000, typed.Order);
        Assert.True(typed.SupportsHook(HtmlTransformHooks.HtmlTransform));
        Assert.False(typed.SupportsHook("after-build"));
        Assert.IsAssignableFrom<IHtmlTransformPlugin>(typed);
    }

    [Fact]
    public void GetAllPlugins_ExplicitConfig_SameContextAndReference_ReusesCachedInstances()
    {
        PluginRegistry.ResetCacheForTests();
        var context = CreateContext();
        var config = context.Config;

        var first = PluginRegistry.GetAllPlugins(context, config).ToList();
        var second = PluginRegistry.GetAllPlugins(context, config).ToList();

        Assert.Equal(1, PluginRegistry.CacheBuildCountForTests);
        Assert.Equal(first.Count, second.Count);
        Assert.All(first.Zip(second), pair => Assert.Same(pair.First.Plugin, pair.Second.Plugin));
    }

    [Fact]
    public void GetAllPlugins_ExplicitConfig_SameContextAndDifferentReference_RebuildsCachedInstances()
    {
        PluginRegistry.ResetCacheForTests();
        var context = CreateContext();
        var firstConfig = context.Config;
        var secondConfig = firstConfig with
        {
            Site = firstConfig.Site with { Title = "Second configuration" }
        };

        var first = PluginRegistry.GetAllPlugins(context, firstConfig).ToList();
        var second = PluginRegistry.GetAllPlugins(context, secondConfig).ToList();
        var third = PluginRegistry.GetAllPlugins(context, secondConfig).ToList();

        Assert.Equal(2, PluginRegistry.CacheBuildCountForTests);
        Assert.Equal(first.Count, second.Count);
        Assert.All(first.Zip(second), pair => Assert.NotSame(pair.First.Plugin, pair.Second.Plugin));
        Assert.All(second.Zip(third), pair => Assert.Same(pair.First.Plugin, pair.Second.Plugin));
    }

    [Fact]
    public void GetAllPlugins_ExplicitConfig_PreservesLockedRegistrationOrderAndSource()
    {
        PluginRegistry.ResetCacheForTests();
        var context = CreateContext();

        var registrations = PluginRegistry.GetAllPlugins(context, context.Config)
            .Select(item => (item.Plugin.Name, item.Plugin.Version, item.Source))
            .ToArray();

        Assert.Equal(
            [
                ("analytics", "1.0.0", "built-in"),
                ("data-files", "1.0.0", "built-in"),
                ("pages-index", "1.1.0", "built-in"),
                ("taxonomy", "3.0.0", "built-in"),
                ("pagination", "2.0.0", "built-in"),
                ("archive", "2.0.0", "built-in"),
                ("related-content", "1.0.0", "built-in"),
                ("alias", "1.0.0", "built-in"),
                ("menu", "1.0.0", "built-in"),
                ("image-processing", "1.0.0", "built-in")
            ],
            registrations);
    }

    [Fact]
    public void GetAllPlugins_CachesTheSingleAnalyticsPluginAsBuiltIn()
    {
        PluginRegistry.ResetCacheForTests();
        var context = CreateContext();

        var first = PluginRegistry.GetAllPlugins(context).Where(x => x.Plugin.Name == "analytics").ToList();
        var second = PluginRegistry.GetAllPlugins(context).Where(x => x.Plugin.Name == "analytics").ToList();

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal("built-in", first[0].Source);
        Assert.Same(first[0].Plugin, second[0].Plugin);
        Assert.Equal(1, PluginRegistry.CacheBuildCountForTests);
    }

    [Fact]
    public void GetAllPlugins_ReturnsNonEmptyList()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugins = PluginRegistry.GetAllPlugins(ctx).ToList();
        var names = plugins.Select(x => x.Plugin.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("pages-index", names);
        Assert.Contains("taxonomy", names);
        Assert.Contains("pagination", names);
        Assert.Contains("archive", names);
        Assert.Contains("related-content", names);
        Assert.Contains("alias", names);
        Assert.Contains("data-files", names);
        Assert.Contains("menu", names);
        Assert.Contains("image-processing", names);
        Assert.DoesNotContain("sitemap", names);
        Assert.DoesNotContain("search-index", names);
        Assert.DoesNotContain("feed", names);
        Assert.DoesNotContain("llms-txt", names);
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
        var ctx2 = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test2", Title = "test2" },
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var plugins = PluginRegistry.GetAllPlugins(ctx).ToList();

        var validSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "built-in"
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
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        _ = PluginRegistry.GetAllPlugins(ctx).ToList();
        Assert.True(PluginRegistry.CacheBuildCountForTests > 0);

        PluginRegistry.ResetCacheForTests();
        Assert.Equal(0, PluginRegistry.CacheBuildCountForTests);
    }

    private static BuildContext CreateContext()
        => new()
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = TestContent.Markdown()
            },
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
}
