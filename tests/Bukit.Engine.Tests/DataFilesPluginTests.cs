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
        var config = CreateConfig();
        var ctx = new BuildContext
        {
            RootDir = Path.Combine(Path.GetTempPath(), "bukit_nonexistent_" + Guid.NewGuid().ToString("N")),
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = new DataFilesPlugin(config).DerivePages(ctx);
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

            var config = CreateConfig();
            var ctx = new BuildContext
            {
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            var derived = new DataFilesPlugin(config).DerivePages(ctx);
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

            var config = CreateConfig();
            var ctx = new BuildContext
            {
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin(config).DerivePages(ctx);
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

            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "t",
                    Title = "t",
                    Languages = new[] { "zh-CN", "en" }
                },
                Content = TestContent.Markdown()
            };
            var ctx = new BuildContext
            {
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin(config).DerivePages(ctx);
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

            var config = CreateConfig();
            var ctx = new BuildContext
            {
                RootDir = root,
                OutputDir = "/t/out",
                BaseUrl = "/",
                LayoutsDir = "/t/l",
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = new ConsoleLogger(LogLevel.Error)
            };

            new DataFilesPlugin(config).DerivePages(ctx);
            Assert.True(ctx.Data.TryGetValue("__data_files", out var val));
            var dict = Assert.IsType<Dictionary<string, object>>(val);
            Assert.Contains("team", dict.Keys);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_TomlFile_ThrowsUnsupportedFormatWithRelativePath()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "settings.toml"), "title = \"Bukit\"");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains("unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("data/settings.toml", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("broken.json", "{\"items\": [}")]
    [InlineData("broken.yaml", "items: [")]
    public void DerivePages_MalformedSupportedFile_ThrowsWithRelativePath(string fileName, string content)
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, fileName), content);

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains($"data/{fileName}", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_DuplicateLogicalKeyAcrossFormats_Throws()
    {
        var root = GetTempDir();
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "nav.json"), "{}");
            File.WriteAllText(Path.Combine(dataDir, "nav.yaml"), "items: []");

            var exception = Assert.Throws<ConfigException>(() =>
                new DataFilesPlugin(CreateConfig()).DerivePages(CreateContext(root)));

            Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("nav", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(root, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DerivePages_DifferentCreationOrder_ProducesSameOrdinalEnumerationOrder()
    {
        var firstRoot = GetTempDir();
        var secondRoot = GetTempDir();
        try
        {
            CreateOrderedFixture(firstRoot, reverse: false);
            CreateOrderedFixture(secondRoot, reverse: true);
            var firstContext = CreateContext(firstRoot);
            var secondContext = CreateContext(secondRoot);

            new DataFilesPlugin(CreateConfig()).DerivePages(firstContext);
            new DataFilesPlugin(CreateConfig()).DerivePages(secondContext);

            var first = Assert.IsType<Dictionary<string, object>>(firstContext.Data["__data_files"]);
            var second = Assert.IsType<Dictionary<string, object>>(secondContext.Data["__data_files"]);
            Assert.Equal(new[] { "a", "z", "b", "y" }, first.Keys);
            Assert.Equal(first.Keys, second.Keys);
        }
        finally
        {
            if (Directory.Exists(firstRoot)) Directory.Delete(firstRoot, true);
            if (Directory.Exists(secondRoot)) Directory.Delete(secondRoot, true);
        }
    }

    private static string GetTempDir() => Path.Combine(Path.GetTempPath(), "bukit_data_test_" + Guid.NewGuid().ToString("N"));

    private static BuildContext CreateContext(string root) => new()
    {
        RootDir = root,
        OutputDir = Path.Combine(root, "out"),
        BaseUrl = "/",
        LayoutsDir = Path.Combine(root, "layouts"),
        RoutedDocuments = Array.Empty<RoutedContentDocument>(),
        Logger = new ConsoleLogger(LogLevel.Error)
    };

    private static void CreateOrderedFixture(string root, bool reverse)
    {
        var dataDir = Path.Combine(root, "data");
        Directory.CreateDirectory(dataDir);
        var entries = new (string Path, string Content)[]
        {
            (Path.Combine(dataDir, "z.json"), "{}"),
            (Path.Combine(dataDir, "a.json"), "{}"),
            (Path.Combine(dataDir, "y", "entry.json"), "{}"),
            (Path.Combine(dataDir, "b", "entry.json"), "{}")
        };
        foreach (var entry in reverse ? entries.Reverse() : entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(entry.Path)!);
            File.WriteAllText(entry.Path, entry.Content);
        }
    }

    private static AppConfig CreateConfig() => new()
    {
        Site = new SiteConfig { Name = "t", Title = "t" },
        Content = TestContent.Markdown()
    };
}
