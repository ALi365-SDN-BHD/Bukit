using Bukit.Cli.Intent;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class IntentApplierExtendedTests : IDisposable
{
    private readonly string _rootDir;

    public IntentApplierExtendedTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-intent-ext-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void Apply_ValidationFails_ReturnsValidationWithErrors()
    {
        var intentPath = Path.Combine(_rootDir, "intent.yaml");
        var outPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(intentPath, """
                                      site:
                                        name: Test
                                        title: Test Site
                                        base_url: no-leading-slash
                                      content:
                                        provider: unknown-provider
                                      theme:
                                        name: starter
                                      """);

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
    }

    [Fact]
    public void Apply_NotionProviderIntent_WritesNotionSection()
    {
        var intentPath = Path.Combine(_rootDir, "intent.yaml");
        var outPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(intentPath, """
                                      site:
                                        name: Test
                                        title: Test Site
                                        base_url: /
                                      content:
                                        provider: notion
                                        notion:
                                          database_id: abc-123
                                          field_policy:
                                            mode: whitelist
                                            allowed:
                                              - title
                                              - tags
                                      theme:
                                        name: starter
                                      """);

        var originalToken = Environment.GetEnvironmentVariable("NOTION_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", "ntn_fake123");

            var (validation, _) = IntentApplier.Apply(intentPath, outPath);

            Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
            var yaml = File.ReadAllText(outPath);
            Assert.Contains("provider: notion", yaml, StringComparison.Ordinal);
            Assert.Contains("databaseId: abc-123", yaml, StringComparison.Ordinal);
            Assert.Contains("mode: whitelist", yaml, StringComparison.Ordinal);
            Assert.Contains("title", yaml, StringComparison.Ordinal);
            Assert.Contains("tags", yaml, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", originalToken);
        }
    }

    [Fact]
    public void Apply_MultiLanguageIntent_WritesLanguagesAndDefaultLanguage()
    {
        var intentPath = Path.Combine(_rootDir, "intent.yaml");
        var outPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(intentPath, """
                                      site:
                                        name: Test
                                        title: Test Site
                                        base_url: /
                                      languages:
                                        default: en
                                        supported:
                                          - en
                                          - zh-CN
                                      content:
                                        provider: markdown
                                      theme:
                                        name: starter
                                      """);

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        var yaml = File.ReadAllText(outPath);
        Assert.Contains("defaultLanguage: en", yaml, StringComparison.Ordinal);
        Assert.Contains("en", yaml, StringComparison.Ordinal);
        Assert.Contains("zh-CN", yaml, StringComparison.Ordinal);
        var config = ConfigLoader.Load(outPath);
        Assert.Equal("en", config.Site.DefaultLanguage);
        Assert.Contains("en", config.Site.Languages!);
        Assert.Contains("zh-CN", config.Site.Languages!);
    }

    [Fact]
    public void Apply_CustomMarkdownDir_WritesMarkdownDir()
    {
        var intentPath = Path.Combine(_rootDir, "intent.yaml");
        var outPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(intentPath, """
                                      site:
                                        name: Test
                                        title: Test Site
                                        base_url: /
                                      content:
                                        provider: markdown
                                        markdown:
                                          dir: posts
                                      theme:
                                        name: starter
                                      """);

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        var yaml = File.ReadAllText(outPath);
        Assert.Contains("dir: posts", yaml, StringComparison.Ordinal);
        var config = ConfigLoader.Load(outPath);
        Assert.Equal("posts", config.Content.Markdown?.Dir);
    }

    [Fact]
    public void Apply_WithUrl_WritesSiteUrl()
    {
        var intentPath = Path.Combine(_rootDir, "intent.yaml");
        var outPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(intentPath, """
                                      site:
                                        name: Test
                                        title: Test Site
                                        base_url: /
                                        url: https://example.com
                                      content:
                                        provider: markdown
                                      theme:
                                        name: starter
                                      """);

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        var yaml = File.ReadAllText(outPath);
        Assert.Contains("url: https://example.com", yaml, StringComparison.Ordinal);
        var config = ConfigLoader.Load(outPath);
        Assert.Equal("https://example.com", config.Site.Url);
    }

    [Fact]
    public void Apply_WithThemeParams_WritesThemeParams()
    {
        var intentPath = Path.Combine(_rootDir, "intent.yaml");
        var outPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(intentPath, """
                                      site:
                                        name: Test
                                        title: Test Site
                                        base_url: /
                                      content:
                                        provider: markdown
                                      theme:
                                        name: starter
                                        params:
                                          brand: My Brand
                                          primary_color: '#ff0000'
                                          accent_color: '#00ff00'
                                      """);

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        var yaml = File.ReadAllText(outPath);
        Assert.Contains("brand: My Brand", yaml, StringComparison.Ordinal);
        Assert.Contains("primary_color: '#ff0000'", yaml, StringComparison.Ordinal);
        Assert.Contains("accent_color: '#00ff00'", yaml, StringComparison.Ordinal);
        var config = ConfigLoader.Load(outPath);
        Assert.NotNull(config.Theme.Params);
        Assert.Equal("My Brand", config.Theme.Params!["brand"]);
    }

    [Fact]
    public void Apply_ConfigValidatorThrowsConfigException_AddsErrorToValidation()
    {
        var intentPath = Path.Combine(_rootDir, "intent.yaml");
        var outPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(intentPath, """
                                      site:
                                        name: Test
                                        title: Test Site
                                        base_url: /
                                      content:
                                        provider: notion
                                        notion:
                                          database_id: db-456
                                          field_policy:
                                            mode: whitelist
                                      theme:
                                        name: starter
                                      """);

        var originalToken = Environment.GetEnvironmentVariable("NOTION_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", null);

            var (validation, _) = IntentApplier.Apply(intentPath, outPath);

            Assert.False(validation.IsValid);
            Assert.Contains(validation.Errors, e => e.Contains("NOTION_TOKEN"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", originalToken);
        }
    }
}
