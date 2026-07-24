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
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = TestContent.Markdown()
            };
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin(config).AfterBuild(ctx);
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
            var config = new AppConfig
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
            };
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin(config).AfterBuild(ctx);

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
            var config = new AppConfig
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
            };
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin(config).AfterBuild(ctx);

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
    public void AfterBuild_ProjectsMenuDataWithoutConfigObjects()
    {
        var outDir = GetTempDir();
        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        ["Main"] =
                        [
                            new MenuConfig
                            {
                                Identifier = "parent",
                                Name = "Parent",
                                Url = "/parent/",
                                Weight = 9,
                                Children =
                                [
                                    new MenuConfig
                                    {
                                        Identifier = "child",
                                        Name = "Child",
                                        Url = "/child/",
                                        Weight = 3
                                    }
                                ]
                            }
                        ]
                    }
                },
                Content = TestContent.Markdown()
            };
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin(config).AfterBuild(ctx);

            var menus = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                ctx.Data["menus"]);
            var main = Assert.IsAssignableFrom<IReadOnlyList<object>>(menus["Main"]);
            var parent = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(Assert.Single(main));
            Assert.Equal("parent", parent["identifier"]);
            Assert.Equal("Parent", parent["name"]);
            Assert.Equal("/parent/", parent["url"]);
            Assert.Equal(9, parent["weight"]);
            var children = Assert.IsAssignableFrom<IReadOnlyList<object>>(parent["children"]);
            var child = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(Assert.Single(children));
            Assert.Equal("child", child["identifier"]);
            Assert.Equal(3, child["weight"]);
            Assert.DoesNotContain(
                TraverseValues(menus),
                value => value.GetType().Assembly == typeof(AppConfig).Assembly);
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
            var config = new AppConfig
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
            };
            var ctx = new BuildContext
            {
                RootDir = "/t",
                OutputDir = outDir,
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new MenuPlugin(config).AfterBuild(ctx);

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

    private static IEnumerable<object> TraverseValues(object root)
    {
        var pending = new Stack<object>();
        pending.Push(root);
        while (pending.TryPop(out var value))
        {
            yield return value;
            if (value is IReadOnlyDictionary<string, object> dictionary)
            {
                foreach (var item in dictionary.Values)
                {
                    if (item is not null)
                    {
                        pending.Push(item);
                    }
                }
            }
            else if (value is IEnumerable<object> sequence)
            {
                foreach (var item in sequence)
                {
                    if (item is not null)
                    {
                        pending.Push(item);
                    }
                }
            }
        }
    }
}
