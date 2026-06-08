using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyTermsInjectorTests
{
    private static ContentDocument CreateDocument(string id, string title, string slug, IReadOnlyDictionary<string, ContentField>? fields = null)
    {
        return ContentDocument.Create(id, title, slug, DateTimeOffset.UtcNow, null, fields);
    }

    [Fact]
    public void InjectFromDataDocuments_WithTaxonomyConfig_InjectsTerms()
    {
        var documents = new List<ContentDocument>
        {
            CreateDocument("1", "Tech Post", "tech-post"),
            CreateDocument("2", "Programming 101", "programming-101"),
        };

        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = TestContent.Markdown(),
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new List<TaxonomyKindConfig>
                {
                    new TaxonomyKindConfig { Key = "tags", Kind = "tags" },
                    new TaxonomyKindConfig { Key = "categories", Kind = "categories" }
                }
            }
        };

        var context = new BuildContext
        {
            Config = config,
            RootDir = "/tmp/test",
            OutputDir = "/tmp/test",
            BaseUrl = "/",
            LayoutsDir = "/tmp/test",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            BodyStore = NullContentBodyStore.Instance,
            Logger = new ConsoleLogger(LogLevel.Debug),
        };

        TaxonomyTermsInjector.InjectFromDataDocuments(context, documents);
    }

    [Fact]
    public void InjectFromDataDocuments_WithEmptyItems_DoesNotThrow()
    {
        var documents = Array.Empty<ContentDocument>();
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = TestContent.Markdown(),
            Taxonomy = new TaxonomyConfig()
        };

        var context = new BuildContext
        {
            Config = config,
            RootDir = "/tmp/test",
            OutputDir = "/tmp/test",
            BaseUrl = "/",
            LayoutsDir = "/tmp/test",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            BodyStore = NullContentBodyStore.Instance,
            Logger = new ConsoleLogger(LogLevel.Debug),
        };

        TaxonomyTermsInjector.InjectFromDataDocuments(context, documents);

        Assert.Empty(context.Data);
    }

    [Fact]
    public void SlugifyTerm_WithSimpleTerm_ReturnsSlugified()
    {
        var result = SlugHelper.Slugify("Hello World");

        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void SlugifyTerm_WithSpecialCharacters_ReturnsCleanSlug()
    {
        var result = SlugHelper.Slugify("C# & .NET Programming!");

        Assert.True(result.Length > 0);
        Assert.DoesNotContain("#", result, StringComparison.Ordinal);
        Assert.DoesNotContain("!", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SlugifyTerm_WithUnicodeCharacters_ReturnsValidSlug()
    {
        var result = SlugHelper.Slugify("\u673a\u5668\u5b66\u4e60");

        Assert.True(!string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void SlugifyTerm_WithEmptyString_ReturnsEmpty()
    {
        var result = SlugHelper.Slugify("");

        Assert.Equal("", result);
    }

    [Fact]
    public void NormalizeNotionFieldKey_WithSimpleName_ReturnsNormalized()
    {
        var result = TaxonomyTermsInjector.NormalizeNotionFieldKey("Tags");

        Assert.NotNull(result);
        Assert.Equal("tags", result);
    }

    [Fact]
    public void NormalizeNotionFieldKey_WithSpaces_ReturnsTrimmedLowercase()
    {
        var result = TaxonomyTermsInjector.NormalizeNotionFieldKey("  My Field  ");

        Assert.NotNull(result);
        Assert.Equal("my_field", result);
    }

    [Fact]
    public void GetOrCreateEnsureTermsMap_WithNewKind_CreatesMap()
    {
        var data = new Dictionary<string, object>();

        var result = TaxonomyTermsInjector.GetOrCreateEnsureTermsMap(data);

        Assert.NotNull(result);
        Assert.True(data.ContainsKey("taxonomy_ensure_terms"));
    }

    [Fact]
    public void GetOrCreateEnsureTermsMap_WithExistingKind_ReturnsExisting()
    {
        var existing = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["tags"] = new List<Dictionary<string, object>> { new() { ["title"] = "Tech", ["slug"] = "tech" } }
        };
        var data = new Dictionary<string, object>
        {
            ["taxonomy_ensure_terms"] = existing
        };

        var result = TaxonomyTermsInjector.GetOrCreateEnsureTermsMap(data);

        Assert.Same(existing, result);
    }
}
