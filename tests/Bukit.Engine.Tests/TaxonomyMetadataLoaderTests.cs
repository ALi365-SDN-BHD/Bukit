using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyMetadataLoaderTests
{
    private static BuildContext CreateContext() => new()
    {
        RootDir = "/tmp/test",
        OutputDir = "/tmp/test",
        BaseUrl = "/",
        LayoutsDir = "/tmp/test",
        RoutedDocuments = Array.Empty<RoutedContentDocument>(),
        Logger = new ConsoleLogger(LogLevel.Error)
    };

    private sealed class WarningLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }

    [Fact]
    public void LoadFromIndexFiles_UnreadableIndex_WarnsAndKeepsTheTerm()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "bukit-term-unreadable-" + Guid.NewGuid().ToString("N"));
        var index = Path.Combine(root, "content", "_taxonomy", "tags", "tech", "_index.md");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(index)!);
            File.WriteAllText(index, "---\ndescription: Tech\n---\n");
            File.SetUnixFileMode(index, UnixFileMode.None);
            var terms = new Dictionary<string, TaxonomyTerm> { ["tech"] = new("Tech", "tech") };
            var logger = new WarningLogger();

            TaxonomyMetadataLoader.LoadFromIndexFiles(root, "tags", terms, logger);

            Assert.Null(terms["tech"].Description);
            var warning = Assert.Single(logger.Warnings);
            Assert.Contains("event=taxonomy.term.index_unreadable kind=tags slug=tech", warning, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(index))
            {
                File.SetUnixFileMode(index, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadAndEnrich_ReadsTermI18nKeys_AndDropsAKeyTwoTermsShare()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-term-keys-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var (slug, key) in new[] { ("malaysia", "my"), ("tea", "drink"), ("kopi", "drink") })
            {
                Directory.CreateDirectory(Path.Combine(root, "content", "_taxonomy", "tags", slug));
                File.WriteAllText(Path.Combine(root, "content", "_taxonomy", "tags", slug, "_index.md"), $"---\ni18nKey: {key}\ndescription: d\n---\n");
            }

            var logger = new WarningLogger();
            var context = new BuildContext
            {
                RootDir = root,
                OutputDir = root,
                BaseUrl = "/",
                LayoutsDir = root,
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                Logger = logger
            };
            var terms = new Dictionary<string, TaxonomyTerm>
            {
                ["malaysia"] = new("Malaysia", "malaysia"),
                ["tea"] = new("Tea", "tea"),
                ["kopi"] = new("Kopi", "kopi")
            };

            TaxonomyMetadataLoader.LoadAndEnrich(context, "tags", terms);

            Assert.Equal("my", terms["malaysia"].I18nKey);
            Assert.Null(terms["tea"].I18nKey);
            Assert.Null(terms["kopi"].I18nKey);
            Assert.Equal("d", terms["tea"].Description);
            var warning = Assert.Single(logger.Warnings);
            Assert.Contains("event=taxonomy.term.i18n_key_duplicate kind=tags key=drink slugs=kopi,tea", warning, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ParseSimpleFrontMatter_ValidYaml_ReturnsDictionary()
    {
        var input = "---\ndescription: A test description\nimage: /img/hero.png\nweight: 5\nparent: tech\n---\nBody content";
        var result = TaxonomyMetadataLoader.ParseSimpleFrontMatter(input);

        Assert.NotNull(result);
        Assert.Equal("A test description", result!["description"]);
        Assert.Equal("/img/hero.png", result["image"]);
        Assert.Equal("5", result["weight"]);
        Assert.Equal("tech", result["parent"]);
    }

    [Fact]
    public void ParseSimpleFrontMatter_NoFrontMatter_ReturnsNull()
    {
        var result = TaxonomyMetadataLoader.ParseSimpleFrontMatter("Just body content");

        Assert.Null(result);
    }

    [Fact]
    public void ParseSimpleFrontMatter_UnclosedFrontMatter_ReturnsNull()
    {
        var input = "---\ndescription: test\n";

        var result = TaxonomyMetadataLoader.ParseSimpleFrontMatter(input);

        Assert.Null(result);
    }

    [Fact]
    public void ParseSimpleFrontMatter_EmptyFrontMatter_ReturnsNull()
    {
        var input = "---\n---\nBody";

        var result = TaxonomyMetadataLoader.ParseSimpleFrontMatter(input);

        Assert.Null(result);
    }

    [Fact]
    public void LoadFromEnsureTerms_EnrichesExistingTerms()
    {
        var context = CreateContext();
        var ensure = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["tags"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["slug"] = "tech",
                    ["description"] = "Technology posts",
                    ["image"] = "/img/tech.png",
                    ["weight"] = 10,
                    ["parent"] = "root"
                }
            }
        };
        context.Set(BuildDataKeys.TaxonomyEnsureTerms, ensure);

        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
        };

        TaxonomyMetadataLoader.LoadFromEnsureTerms(context, "tags", terms);

        Assert.Equal("Technology posts", terms["tech"].Description);
        Assert.Equal("/img/tech.png", terms["tech"].Image);
        Assert.Equal(10, terms["tech"].Weight);
        Assert.Equal("root", terms["tech"].ParentSlug);
    }

    [Fact]
    public void LoadFromEnsureTerms_DoesNotOverwriteExistingMetadata()
    {
        var context = CreateContext();
        var ensure = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["tags"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["slug"] = "tech",
                    ["description"] = "New description"
                }
            }
        };
        context.Set(BuildDataKeys.TaxonomyEnsureTerms, ensure);

        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
            {
                Description = "Existing description",
                Weight = 99
            }
        };

        TaxonomyMetadataLoader.LoadFromEnsureTerms(context, "tags", terms);

        Assert.Equal("Existing description", terms["tech"].Description);
        Assert.Equal(99, terms["tech"].Weight);
    }

    [Fact]
    public void LoadFromEnsureTerms_MissingKind_DoesNotThrow()
    {
        var context = CreateContext();
        var ensure = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["tags"] = new List<Dictionary<string, object>>()
        };
        context.Set(BuildDataKeys.TaxonomyEnsureTerms, ensure);

        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
        };

        TaxonomyMetadataLoader.LoadFromEnsureTerms(context, "categories", terms);

        Assert.Null(terms["tech"].Description);
    }
}
