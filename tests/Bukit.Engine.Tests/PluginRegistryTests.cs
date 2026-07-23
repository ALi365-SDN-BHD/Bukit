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
        var plugins = new BuiltInPluginSource(CreateConfig()).GetPlugins().ToList();
        var analytics = Assert.Single(plugins, x => x.Name == "analytics");

        var typed = Assert.IsType<AnalyticsPlugin>(analytics);
        Assert.Equal("1.0.0", typed.Version);
        Assert.Equal(1000, typed.Order);
        Assert.True(typed.SupportsHook(HtmlTransformHooks.HtmlTransform));
        Assert.False(typed.SupportsHook("after-build"));
        Assert.IsAssignableFrom<IHtmlTransformPlugin>(typed);
    }

    [Fact]
    public void GetAllPlugins_SameSession_ReusesInstances()
    {
        PluginRegistry.ResetBuildCountForTests();
        var context = CreateContext();
        var config = CreateConfig();
        var session = PluginExecutionSession.Create(config, BuildExecutionMode.Production);

        var first = PluginRegistry.GetAllPlugins(context, session).ToList();
        var second = PluginRegistry.GetAllPlugins(context, session).ToList();

        Assert.Equal(1, PluginRegistry.RegistrationBuildCountForTests);
        Assert.Equal(first.Count, second.Count);
        Assert.All(first.Zip(second), pair => Assert.Same(pair.First.Plugin, pair.Second.Plugin));
    }

    [Fact]
    public void GetAllPlugins_DifferentSessions_IsolateInstances()
    {
        PluginRegistry.ResetBuildCountForTests();
        var context = CreateContext();
        var firstConfig = CreateConfig();
        var secondConfig = firstConfig with
        {
            Site = firstConfig.Site with { Title = "Second configuration" }
        };

        var firstSession = PluginExecutionSession.Create(
            firstConfig,
            BuildExecutionMode.Production);
        var secondSession = PluginExecutionSession.Create(
            secondConfig,
            BuildExecutionMode.Production);
        var first = PluginRegistry.GetAllPlugins(context, firstSession).ToList();
        var second = PluginRegistry.GetAllPlugins(context, secondSession).ToList();
        var third = PluginRegistry.GetAllPlugins(context, secondSession).ToList();

        Assert.Equal(2, PluginRegistry.RegistrationBuildCountForTests);
        Assert.Equal(first.Count, second.Count);
        Assert.All(first.Zip(second), pair => Assert.NotSame(pair.First.Plugin, pair.Second.Plugin));
        Assert.All(second.Zip(third), pair => Assert.Same(pair.First.Plugin, pair.Second.Plugin));
    }

    [Fact]
    public void GetAllPlugins_Session_PreservesLockedRegistrationOrderAndSource()
    {
        PluginRegistry.ResetBuildCountForTests();
        var context = CreateContext();
        var config = CreateConfig();
        var session = PluginExecutionSession.Create(config, BuildExecutionMode.Production);

        var registrations = PluginRegistry.GetAllPlugins(context, session)
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
    public void GetAllPlugins_CompatibilityCallsUseIsolatedSessions()
    {
        PluginRegistry.ResetBuildCountForTests();
        var context = CreateContext();

        var first = PluginRegistry.GetAllPlugins(context).Where(x => x.Plugin.Name == "analytics").ToList();
        var second = PluginRegistry.GetAllPlugins(context).Where(x => x.Plugin.Name == "analytics").ToList();

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal("built-in", first[0].Source);
        Assert.NotSame(first[0].Plugin, second[0].Plugin);
        Assert.Equal(2, PluginRegistry.RegistrationBuildCountForTests);
    }

    [Fact]
    public void GetAllPlugins_ReturnsNonEmptyList()
    {
        PluginRegistry.ResetBuildCountForTests();
        var ctx = new BuildContext
        {
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
        PluginRegistry.ResetBuildCountForTests();
        var ctx = new BuildContext
        {
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
        PluginRegistry.ResetBuildCountForTests();
        var ctx = new BuildContext
        {
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
    public void GetAllPlugins_CompatibilityCallBuildsOneShortLivedSession()
    {
        PluginRegistry.ResetBuildCountForTests();
        var ctx = new BuildContext
        {
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var firstCallCount = PluginRegistry.RegistrationBuildCountForTests;
        var first = PluginRegistry.GetAllPlugins(ctx).ToList();
        var afterFirstCallCount = PluginRegistry.RegistrationBuildCountForTests;

        var second = PluginRegistry.GetAllPlugins(ctx).ToList();
        var afterSecondCallCount = PluginRegistry.RegistrationBuildCountForTests;

        Assert.Equal(firstCallCount + 1, afterFirstCallCount);
        Assert.Equal(afterFirstCallCount + 1, afterSecondCallCount);
        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public void GetAllPlugins_ContainsKnownBuiltInPlugins()
    {
        PluginRegistry.ResetBuildCountForTests();
        var ctx = new BuildContext
        {
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
        PluginRegistry.ResetBuildCountForTests();
        var ctx1 = new BuildContext
        {
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
        var ctx2 = new BuildContext
        {
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var countBefore = PluginRegistry.RegistrationBuildCountForTests;
        var plugins1 = PluginRegistry.GetAllPlugins(ctx1).ToList();
        var countAfter1 = PluginRegistry.RegistrationBuildCountForTests;

        var plugins2 = PluginRegistry.GetAllPlugins(ctx2).ToList();
        var countAfter2 = PluginRegistry.RegistrationBuildCountForTests;

        Assert.Equal(countBefore + 1, countAfter1);
        Assert.Equal(countAfter1 + 1, countAfter2);
    }

    [Fact]
    public void GetAllPlugins_EachPluginHasValidSource()
    {
        PluginRegistry.ResetBuildCountForTests();
        var ctx = new BuildContext
        {
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
    public void ResetBuildCountForTests_ResetsBuildCount()
    {
        PluginRegistry.ResetBuildCountForTests();
        Assert.Equal(0, PluginRegistry.RegistrationBuildCountForTests);

        var ctx = new BuildContext
        {
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        _ = PluginRegistry.GetAllPlugins(ctx).ToList();
        Assert.True(PluginRegistry.RegistrationBuildCountForTests > 0);

        PluginRegistry.ResetBuildCountForTests();
        Assert.Equal(0, PluginRegistry.RegistrationBuildCountForTests);
    }

    private static BuildContext CreateContext()
        => new()
        {
            RootDir = "/test/no-plugins-dir",
            OutputDir = "/test/out",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

    private static AppConfig CreateConfig()
        => new()
        {
            Site = new SiteConfig { Name = "test", Title = "test" },
            Content = TestContent.Markdown()
        };
}
