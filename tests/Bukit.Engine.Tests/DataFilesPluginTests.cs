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

public sealed class DataFilesPluginTests
{
    [Fact]
    public void DerivePages_NoDataDir_ReturnsEmpty()
    {
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = TestContent.Markdown()
            },
            RootDir = Path.Combine(Path.GetTempPath(), "bukit_nonexistent_" + Guid.NewGuid().ToString("N")),
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new DataFilesPlugin().DerivePages(ctx);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_LoadsYamlDataFile()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "authors.yaml"),
                "john:\n  name: John Doe\n  email: john@example.com\n");

            var ctx = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig { Name = "t", Title = "t" },
                    Content = TestContent.Markdown()
                },
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            var derived = new DataFilesPlugin().DerivePages(ctx);
            Assert.Empty(derived);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            var authors = Assert.IsType<Dictionary<string, object>>(dict["authors"]);
            var john = Assert.IsType<Dictionary<string, object>>(authors["john"]);
            Assert.Equal("John Doe", john["name"]);
            Assert.Equal("john@example.com", john["email"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_LoadsJsonDataFile()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "nav.json"),
                """{"items":[{"name":"Home","url":"/"},{"name":"Blog","url":"/blog/"}]}""");

            var ctx = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig { Name = "t", Title = "t" },
                    Content = TestContent.Markdown()
                },
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin().DerivePages(ctx);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            var nav = Assert.IsType<Dictionary<string, object>>(dict["nav"]);
            var items = Assert.IsType<List<object>>(nav["items"]);
            var home = Assert.IsType<Dictionary<string, object>>(items[0]);
            Assert.Equal("Home", home["name"]);
            Assert.Equal("/", home["url"]);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_LoadsMultiLanguageData()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(Path.Combine(dataDir, "zh-CN"));
            Directory.CreateDirectory(Path.Combine(dataDir, "en"));
            File.WriteAllText(Path.Combine(dataDir, "zh-CN", "strings.yaml"), "hello: 你好\n");
            File.WriteAllText(Path.Combine(dataDir, "en", "strings.yaml"), "hello: Hello\n");

            var ctx = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "t",
                        Title = "t",
                        Languages = new[] { "zh-CN", "en" }
                    },
                    Content = TestContent.Markdown()
                },
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin().DerivePages(ctx);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            Assert.Contains("zh-CN", dict.Keys);
            Assert.Contains("en", dict.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_NestedDirectories_LoadsRecursively()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            var subDir = Path.Combine(dataDir, "team");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "members.yaml"), "devs:\n  - Alice\n  - Bob\n");

            var ctx = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig { Name = "t", Title = "t" },
                    Content = TestContent.Markdown()
                },
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin().DerivePages(ctx);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            Assert.Contains("team", dict.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static string GetTempDir() => Path.Combine(Path.GetTempPath(), "bukit_data_test_" + Guid.NewGuid().ToString("N"));
}
