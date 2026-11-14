using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class IntentValidatorTests : IDisposable
{
    private readonly string _rootDir;

    public IntentValidatorTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-intent-validator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public void Validate_InvalidIntent_ReturnsExpectedErrors()
    {
        var intent = new SiteIntent
        {
            Site = new SiteIntentSite
            {
                Name = "",
                Title = "",
                BaseUrl = "blog"
            },
            Content = new SiteIntentContent
            {
                Kind = "weird"
            },
            Theme = new SiteIntentTheme
            {
                Name = ""
            }
        };

        var result = IntentValidator.Validate(intent, _rootDir);

        Assert.Contains("site.name is required.", result.Errors);
        Assert.Contains("site.title is required.", result.Errors);
        Assert.Contains("site.base_url must start with '/'.", result.Errors);
        Assert.Contains("content.kind must be markdown|notion.", result.Errors);
        Assert.Contains("theme.name is required.", result.Errors);
        Assert.False(result.IsValid);
    }

    public static TheoryData<string?, string[], string?> LanguageCases => new()
    {
        { null, new[] { "en" }, null },
        { "en", new[] { "en" }, null },
        { "EN", new[] { "en" }, null },
        { "fr", new[] { "en" }, "languages.default must be included in languages.supported." },
        { null, new[] { "en", "zh-CN" }, "languages.default is required when languages.supported has more than one language." },
        { "zh-CN", new[] { "en", "zh-CN" }, null }
    };

    [Theory]
    [MemberData(nameof(LanguageCases))]
    public void Validate_Languages_DefaultIsOptionalOnlyForOneLanguage(string? defaultLanguage, string[] supported, string? expectedError)
    {
        var intent = new SiteIntent
        {
            Site = new SiteIntentSite { Name = "site", Title = "Site", BaseUrl = "/" },
            Languages = new SiteIntentLanguages { Default = defaultLanguage, Supported = supported },
            Content = new SiteIntentContent { Kind = "markdown" },
            Theme = new SiteIntentTheme { Name = "starter" }
        };

        var errors = IntentValidator.Validate(intent, _rootDir).Errors
            .Where(error => error.StartsWith("languages.", StringComparison.Ordinal))
            .ToList();

        if (expectedError is null)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.Equal([expectedError], errors);
        }
    }

    [Fact]
    public void Validate_NotionIntentWithoutDatabaseId_ReturnsErrorAndTokenWarning()
    {
        var intent = new SiteIntent
        {
            Site = new SiteIntentSite
            {
                Name = "site",
                Title = "Site",
                BaseUrl = "/"
            },
            Content = new SiteIntentContent
            {
                Kind = "notion",
                Notion = new SiteIntentNotionContent
                {
                    DatabaseId = "",
                    FieldPolicy = new SiteIntentNotionFieldPolicy
                    {
                        Mode = "invalid"
                    }
                }
            },
            Theme = new SiteIntentTheme
            {
                Name = "starter"
            }
        };

        var result = IntentValidator.Validate(intent, _rootDir);

        Assert.Contains("content.notion.database_id is required when content.kind is notion.", result.Errors);
        Assert.Contains("content.notion.field_policy.mode must be whitelist|all.", result.Errors);
        Assert.Contains(result.Warnings, warning => warning.Contains("NOTION_TOKEN is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MarkdownIntentWithMissingDirectory_ReturnsWarning()
    {
        var intent = new SiteIntent
        {
            Site = new SiteIntentSite
            {
                Name = "site",
                Title = "Site",
                BaseUrl = "/"
            },
            Content = new SiteIntentContent
            {
                Kind = "markdown",
                Markdown = new SiteIntentMarkdownContent
                {
                    Dir = "missing-content"
                }
            },
            Theme = new SiteIntentTheme
            {
                Name = "starter"
            }
        };

        var result = IntentValidator.Validate(intent, _rootDir);

        Assert.Contains(result.Warnings, warning => warning.Contains("content.markdown.dir not found: missing-content", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("theme not found under themes/: starter", StringComparison.Ordinal));
    }
}
