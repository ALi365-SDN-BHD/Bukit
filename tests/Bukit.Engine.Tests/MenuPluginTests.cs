using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MenuPluginTests
{
    [Fact]
    public void AfterBuild_NoMenus_DoesNothing()
    {
        var outDir = GetTempDir();
        try
        {
            var ctx = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig { Name = "t", Title = "t" },
                    Content = TestContent.Markdown()
                },
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin().AfterBuild(ctx);
            Assert.False(File.Exists(Path.Combine(outDir, "menus.json")));
            Assert.False(ctx.Data.ContainsKey("menus"));
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public void AfterBuild_InjectsMenusIntoDataAndWritesJson()
    {
        var outDir = GetTempDir();
        try
        {
            var ctx = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "t",
                        Title = "t",
                        Menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["main"] = new[]
                            {
                                new MenuConfig { Identifier = "home", Name = "Home", Url = "/", Weight = 1 }
                            }
                        }
                    },
                    Content = TestContent.Markdown()
                },
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin().AfterBuild(ctx);

            Assert.True(ctx.Data.ContainsKey("menus"));
            var jsonPath = Path.Combine(outDir, "menus.json");
            Assert.True(File.Exists(jsonPath));

            var json = File.ReadAllText(jsonPath);
            Assert.Contains("\"home\"", json);
            Assert.Contains("\"Home\"", json);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public void AfterBuild_WritesNestedChildren()
    {
        var outDir = GetTempDir();
        try
        {
            var ctx = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "t",
                        Title = "t",
                        Menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["main"] = new[]
                            {
                                new MenuConfig
                                {
                                    Identifier = "blog", Name = "Blog", Url = "/blog/", Weight = 1,
                                    Children = new[]
                                    {
                                        new MenuConfig { Identifier = "tech", Name = "Tech", Url = "/blog/tech/", Weight = 1 }
                                    }
                                }
                            }
                        }
                    },
                    Content = TestContent.Markdown()
                },
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin().AfterBuild(ctx);

            var json = File.ReadAllText(Path.Combine(outDir, "menus.json"));
            Assert.Contains("\"children\"", json);
            Assert.Contains("\"Tech\"", json);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public void AfterBuild_MultipleMenus_AllPresent()
    {
        var outDir = GetTempDir();
        try
        {
            var ctx = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "t",
                        Title = "t",
                        Menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["main"] = new[] { new MenuConfig { Identifier = "home", Name = "Home", Url = "/", Weight = 1 } },
                            ["footer"] = new[] { new MenuConfig { Identifier = "about", Name = "About", Url = "/about/", Weight = 1 } }
                        }
                    },
                    Content = TestContent.Markdown()
                },
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin().AfterBuild(ctx);

            var json = File.ReadAllText(Path.Combine(outDir, "menus.json"));
            Assert.Contains("\"main\"", json);
            Assert.Contains("\"footer\"", json);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    private static string GetTempDir() => Path.Combine(Path.GetTempPath(), "bukit_menu_test_" + Guid.NewGuid().ToString("N"));
}
