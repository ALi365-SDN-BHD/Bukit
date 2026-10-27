using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class CollectionsValidatorTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }

    private string WriteTempYaml(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-config-collections-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return path;
    }

    private static string CollectionsYaml(string collectionBody, string sourcesBody = """
        content:
          sources:
            - type: markdown
              collection: posts
              markdown:
                dir: content
        """)
        => "site:\n  name: myblog\n  title: My Blog\n  collections:\n    posts:\n" +
           collectionBody + "\n" + sourcesBody + "\n";

    private AppConfig LoadAndValidate(string yaml)
    {
        var config = ConfigLoader.Load(WriteTempYaml(yaml));
        ConfigValidator.Validate(config);
        return config;
    }

    private ConfigException ValidateFails(string yaml)
    {
        var config = ConfigLoader.Load(WriteTempYaml(yaml));
        return Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPermalinkOmitsSlugPlaceholder()
    {
        var ex = ValidateFails(CollectionsYaml("      permalink: /posts/"));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains("site.collections.posts.permalink must include {slug}.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTemplateIsBlank()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  template: '   '
            """));

        Assert.Contains("site.collections.posts.template must be a non-empty string", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public void Validate_ShouldFail_WhenPaginationPageSizeIsNotPositive(string pageSize)
    {
        var ex = ValidateFails(CollectionsYaml($$"""
                  permalink: /posts/{slug}/
                  pagination:
                    pageSize: {{pageSize}}
            """));

        Assert.Contains("site.collections.posts.pagination.pageSize must be a positive integer.", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/page/:num/", "must be relative", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("page/:num/?a=1", "must not contain query strings or fragments", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("page/:num/#a", "must not contain query strings or fragments", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("page\\:num", "must not contain backslashes", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("page/../:num/", "must not contain '..' path traversal segments", DiagnosticCode.ConfigPathTraversal)]
    [InlineData("page/%2E%2E/:num/", "must not contain '..' path traversal segments", DiagnosticCode.ConfigPathTraversal)]
    [InlineData("page/%2F:num/", "must not contain encoded slashes", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("page/{num", "contains an unclosed placeholder", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("page/num}/", "contains an unopened placeholder", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("page/{unknown}/", "contains unsupported placeholder {unknown}", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("page/section/", "must include :num, {num}, or {page}", DiagnosticCode.ConfigInvalidValue)]
    public void Validate_ShouldFail_WhenPaginationUrlPatternIsUnsafeOrIncomplete(
        string urlPattern,
        string expectedMessage,
        DiagnosticCode expectedCode)
    {
        var ex = ValidateFails(CollectionsYaml($$"""
                  permalink: /posts/{slug}/
                  pagination:
                    urlPattern: '{{urlPattern}}'
            """));

        Assert.Equal(expectedCode, ex.Code);
        Assert.Contains("site.collections.posts.pagination.urlPattern", ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedMessage, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPaginationUrlPatternContainsControlCharacters()
    {
        var ex = ValidateFails(CollectionsYaml(
            "      permalink: /posts/{slug}/\n" +
            "      pagination:\n" +
            "        urlPattern: \"page/\u0001:num/\"\n"));

        Assert.Contains("must not contain control characters", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("page/:num/")]
    [InlineData("page/{page}/")]
    [InlineData("page/{collection}/{num}/")]
    public void Validate_ShouldAccept_WhenPaginationUrlPatternIsRelativeWithPagePlaceholder(string urlPattern)
    {
        var config = LoadAndValidate(CollectionsYaml($$"""
                  permalink: /posts/{slug}/
                  pagination:
                    urlPattern: '{{urlPattern}}'
            """));

        Assert.Equal(urlPattern, config.Site.Collections!["posts"].Pagination.UrlPattern);
    }

    [Fact]
    public void Validate_ShouldFail_WhenListRouteIsNotRootRelative()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  listRoute: posts/
            """));

        Assert.Contains("site.collections.posts.listRoute must start with '/'.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSchemaFailModeIsUnsupported()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  schemaFailMode: lenient
            """));

        Assert.Contains("site.collections.posts.schemaFailMode must be off|warn|strict.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldAccept_WhenSchemaFailModeIsCaseInsensitive()
    {
        var config = LoadAndValidate(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  schemaFailMode: ' WARN '
            """));

        Assert.Equal(" WARN ", config.Site.Collections!["posts"].SchemaFailMode);
    }

    [Fact]
    public void Load_ShouldFail_WhenFilteredListOmitsField()
    {
        var path = WriteTempYaml(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - listRoute: /posts/tagged/
                      value: dotnet
            """));

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains("field is required.", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("operator: matches\nvalue: dotnet", "operator must be equals|contains|in")]
    [InlineData("operator: equals\nvalue: ''", "value is required for operator equals")]
    [InlineData("operator: contains\nvalue: ''", "value is required for operator contains")]
    [InlineData("operator: equals\nvalue: dotnet\nvalues: [a]", "values is only supported for operator in")]
    [InlineData("operator: in", "values must include at least one value for operator in")]
    [InlineData("operator: in\nvalue: dotnet\nvalues: [a]", "value must not be set when operator is in")]
    public void Validate_ShouldFail_WhenFilteredListOperatorContractIsViolated(string operatorAndValues, string expectedMessage)
    {
        var indented = operatorAndValues.Replace("\n", "\n          ", StringComparison.Ordinal);
        var ex = ValidateFails(CollectionsYaml($$"""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - field: tags
                      listRoute: /posts/tagged/
                      {{indented}}
            """));

        Assert.Contains("site.collections.posts.filteredLists[0]", ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedMessage, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFilteredListRouteIsNotRootRelative()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - field: tags
                      value: dotnet
                      listRoute: posts/tagged/
            """));

        Assert.Contains("listRoute must start with '/'.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTwoFilteredListsShareRoute()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - field: tags
                      value: dotnet
                      listRoute: /posts/tagged/
                    - field: tags
                      value: csharp
                      listRoute: /POSTS/TAGGED/
            """));

        Assert.Contains("duplicates another filtered list route", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFilteredListTemplateIsBlank()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - field: tags
                      value: dotnet
                      listRoute: /posts/tagged/
                      listTemplate: ' '
            """));

        Assert.Contains("listTemplate must be a non-empty string when set.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFilteredListPageSizeIsNotPositive()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - field: tags
                      value: dotnet
                      listRoute: /posts/tagged/
                      pageSize: 0
            """));

        Assert.Contains("pageSize must be a positive integer.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFilteredListUrlPatternIsUnsafe()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - field: tags
                      value: dotnet
                      listRoute: /posts/tagged/
                      urlPattern: ../:num/
            """));

        Assert.Equal(DiagnosticCode.ConfigPathTraversal, ex.Code);
        Assert.Contains("site.collections.posts.filteredLists[0].urlPattern", ex.Message, StringComparison.Ordinal);
        Assert.Contains("must not contain '..' path traversal segments", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFilteredListEmptyBehaviorIsUnsupported()
    {
        var ex = ValidateFails(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - field: tags
                      value: dotnet
                      listRoute: /posts/tagged/
                      emptyBehavior: ignore
            """));

        Assert.Contains("emptyBehavior must be render|skip.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldAccept_WhenFilteredListUsesInOperatorWithValues()
    {
        var config = LoadAndValidate(CollectionsYaml("""
                  permalink: /posts/{slug}/
                  filteredLists:
                    - field: tags
                      operator: in
                      values: [dotnet, csharp]
                      listRoute: /posts/tagged/
            """));

        var filter = Assert.Single(config.Site.Collections!["posts"].FilteredLists!);
        Assert.Equal("in", filter.Operator);
        Assert.Equal(new[] { "dotnet", "csharp" }, filter.Values);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNoContentSourceDeclaresCollection()
    {
        var ex = ValidateFails("""
            site:
              name: myblog
              title: My Blog
              collections:
                posts:
                  permalink: /posts/{slug}/
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

        Assert.Contains("no content source has a 'collection' field", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldFail_WhenOneOfTwoCollectionsIsUnreferenced()
    {
        var ex = ValidateFails("""
            site:
              name: myblog
              title: My Blog
              collections:
                posts:
                  permalink: /posts/{slug}/
                notes:
                  permalink: /notes/{slug}/
            content:
              sources:
                - type: markdown
                  collection: posts
                  markdown:
                    dir: content
                - type: markdown
                  name: pages
                  markdown:
                    dir: pages
            """);

        Assert.Contains("the following site.collections have no matching content source with a 'collection' field: notes", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldAccept_WhenAllSourcesAssignCollectionsEvenIfAnotherCollectionIsUnused()
    {
        var config = LoadAndValidate("""
            site:
              name: myblog
              title: My Blog
              collections:
                posts:
                  permalink: /posts/{slug}/
                notes:
                  permalink: /notes/{slug}/
            content:
              sources:
                - type: markdown
                  collection: posts
                  markdown:
                    dir: content
            """);

        Assert.True(config.Site.Collections!.ContainsKey("notes"));
    }

    [Fact]
    public void Validate_ShouldMatchCollectionReferencesIgnoringCaseWhenComparisonPathIsReached()
    {
        var config = LoadAndValidate("""
            site:
              name: myblog
              title: My Blog
              collections:
                Posts:
                  permalink: /posts/{slug}/
            content:
              sources:
                - type: markdown
                  collection: posts
                  markdown:
                    dir: content
                - type: markdown
                  name: pages
                  markdown:
                    dir: pages
            """);

        Assert.True(config.Site.Collections!.ContainsKey("posts"));
    }

    [Fact]
    public void Validate_ShouldAccept_WhenCollectionIsReferencedThroughAddToCollections()
    {
        var config = LoadAndValidate("""
            site:
              name: myblog
              title: My Blog
              collections:
                companies:
                  permalink: /companies/{slug}/
                china_companies:
                  permalink: /china-companies/{slug}/
            content:
              sources:
                - type: markdown
                  collection: companies
                  addToCollections: [china_companies]
                  markdown:
                    dir: content
                - type: markdown
                  name: pages
                  markdown:
                    dir: pages
            """);

        Assert.True(config.Site.Collections!.ContainsKey("china_companies"));
    }

    [Fact]
    public void Validate_ShouldIgnoreDataModeSources_WhenCheckingCollectionReferences()
    {
        LoadAndValidate("""
            site:
              name: myblog
              title: My Blog
              collections:
                posts:
                  permalink: /posts/{slug}/
            content:
              sources:
                - type: markdown
                  name: nav
                  mode: data
                  markdown:
                    dir: data
            """);
    }
}
