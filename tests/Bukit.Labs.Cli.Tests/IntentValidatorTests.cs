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
