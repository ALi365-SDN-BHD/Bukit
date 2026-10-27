using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class StrictYamlContractTests : IDisposable
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
        var path = Path.Combine(Path.GetTempPath(), $"bukit-config-strict-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return path;
    }

    private const string MinimalSite = """
        site:
          name: myblog
          title: My Blog
        """;

    private const string MinimalContent = """
        content:
          sources:
            - type: markdown
              markdown:
                dir: content
        """;

    [Fact]
    public void Load_DuplicateKeyInRoot_ShouldFailWithConfigYamlSyntaxError_WhenYamlDeclaresKeyTwice()
    {
        var path = WriteTempYaml("""
            site:
              name: myblog
              title: My Blog
            site:
              name: other
              title: Other
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigYamlSyntaxError, ex.Code);
    }

    [Fact]
    public void Load_DuplicateKeyInNestedMapping_ShouldFailWithConfigYamlSyntaxError_WhenYamlDeclaresKeyTwice()
    {
        var path = WriteTempYaml(MinimalSite + "\n" + """
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
                    dir: other
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigYamlSyntaxError, ex.Code);
    }

    [Fact]
    public void Load_UnknownRootSection_ShouldFail_WhenSectionIsNotDeclared()
    {
        var path = WriteTempYaml(MinimalSite + "\n" + MinimalContent + "\n" + """
            pluginMarket:
              enabled: true
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("Unknown config field 'root.pluginMarket'.", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build:\n  clean: maybe\n", "build.clean")]
    [InlineData("build:\n  languageJobs: many\n", "languageJobs")]
    [InlineData("site:\n  name: myblog\n  title: My Blog\n  autoSummaryMaxLength: many\n", "autoSummaryMaxLength")]
    [InlineData("site:\n  name: myblog\n  title: My Blog\n  pagination:\n    pageSize: many\n", "pageSize")]
    [InlineData("site:\n  name: myblog\n  title: My Blog\n  sitemapDetail:\n    defaultPriority: high\n", "defaultPriority")]
    public void Load_InvalidScalarType_ShouldFailWithInvalidValueDiagnostic_WhenValueCannotBeParsed(string fragment, string expectedPath)
    {
        var baseYaml = fragment.StartsWith("build:", StringComparison.Ordinal) || fragment.StartsWith("taxonomy:", StringComparison.Ordinal)
            ? MinimalSite + "\n" + MinimalContent + "\n" + fragment
            : fragment + MinimalContent;
        var path = WriteTempYaml(baseYaml);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains(expectedPath, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("site:\n  name: [a]\n  title: My Blog\n", "name", "sequence")]
    [InlineData("site:\n  name: myblog\n  title: {a: b}\n", "title", "mapping")]
    [InlineData("site:\n  name: myblog\n  title: My Blog\n  languages: nope\n", "languages", "scalar")]
    [InlineData("site:\n  name: myblog\n  title: My Blog\n  permalinks: nope\n", "permalinks", "scalar")]
    public void Load_WrongNodeKind_ShouldFailWithNodeKindMismatch_WhenNodeIsNotExpectedKind(
        string fragment,
        string expectedPath,
        string expectedInMessage)
    {
        var path = WriteTempYaml(fragment + MinimalContent);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains(expectedPath, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedInMessage, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("site:\n  title: My Blog\n", "name is required")]
    [InlineData("site:\n  name: '   '\n  title: My Blog\n", "name is required")]
    [InlineData("site:\n  name: myblog\n", "title is required")]
    public void Load_MissingRequiredSiteField_ShouldFail_WhenValueAbsentOrBlank(string siteYaml, string expectedMessage)
    {
        var path = WriteTempYaml(siteYaml + MinimalContent);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains(expectedMessage, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MissingContentSection_ShouldFail_WhenSectionAbsent()
    {
        var path = WriteTempYaml(MinimalSite);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains("content section is required.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_EmptySourcesSequence_ShouldFail_WhenNoSourceDeclared()
    {
        var path = WriteTempYaml(MinimalSite + "\n" + """
            content:
              sources: []
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains("content.sources is required in Bukit 1.0", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_SourceWithoutType_ShouldFail_WhenTypeAbsent()
    {
        var path = WriteTempYaml(MinimalSite + "\n" + """
            content:
              sources:
                - markdown:
                    dir: content
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains("type is required.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_CollectionWithoutPermalink_ShouldFail_WhenCollectionDeclared()
    {
        var path = WriteTempYaml("""
            site:
              name: myblog
              title: My Blog
              collections:
                posts:
                  template: pages/post.html
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains("permalink is required.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_CollectionSchemaField_ShouldFailStrictUnknownField()
    {
        var path = WriteTempYaml("""
            site:
              name: myblog
              title: My Blog
              collections:
                posts:
                  permalink: /posts/{slug}/
                  schema:
                    title: string
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("Unknown config field 'site.collections.posts.schema'.", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("logging:\n  level: loud\n", "logging.level", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("build:\n  output: ''\n", "build.output", DiagnosticCode.ConfigRequiredFieldMissing)]
    public void Validate_InvalidLoadedValue_ShouldFailWithDiagnostic_WhenSemanticRulesViolated(
        string fragment,
        string expectedPath,
        DiagnosticCode expectedCode)
    {
        var path = WriteTempYaml(MinimalSite + "\n" + MinimalContent + "\n" + fragment);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(ConfigLoader.Load(path)));

        Assert.Equal(expectedCode, ex.Code);
        Assert.Contains(expectedPath, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("    canonicalMappings:\n      - rawKey: abstract", "canonicalMappings", "canonicalField")]
    [InlineData("    customFields:\n      - fieldType: string", "customFields", "name")]
    [InlineData("    fieldScopes:\n      posts:\n        - fieldType: string", "fieldScopes", "name")]
    [InlineData("    entityMappings:\n      - entityType: company", "entityMappings", "rawKey")]
    [InlineData("    relationMappings:\n      - relationType: related", "relationMappings", "rawKey")]
    public void Load_ModelSchemaMappingMissingRequiredKey_ShouldFailWithRequiredFieldDiagnostic(
        string modelSchema,
        string mapping,
        string requiredKey)
    {
        var yaml = MinimalSite + "\n" +
            "content:\n" +
            "  sources:\n" +
            "    - type: markdown\n" +
            "      markdown:\n" +
            "        dir: content\n" +
            "  modelSchema:\n" +
            modelSchema + "\n";
        var path = WriteTempYaml(yaml);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains($"content.modelSchema.{mapping}", ex.Message, StringComparison.Ordinal);
        Assert.Contains(requiredKey, ex.Message, StringComparison.Ordinal);
    }
}
