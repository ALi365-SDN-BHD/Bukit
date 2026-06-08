using Bukit.Cli.Intent;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class IntentValidatorTests : IDisposable
{
    private readonly string _tempDir;

    public IntentValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-validator-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    private static SiteIntent CreateValidMarkdownIntent()
    {
        return new SiteIntent
        {
            Site = new SiteIntentSite
            {
                Name = "test-site",
                Title = "Test Site",
                BaseUrl = "/"
            },
            Content = new SiteIntentContent
            {
                Kind = "markdown",
                Markdown = new SiteIntentMarkdownContent { Dir = "content" }
            },
            Theme = new SiteIntentTheme { Name = "starter" }
        };
    }

    [Fact]
    public void Validate_ValidMarkdownIntent_PassesValidation()
    {
        var intent = CreateValidMarkdownIntent();

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_MissingSiteName_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Site = new SiteIntentSite { Name = "", Title = "Test", BaseUrl = "/" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("site.name"));
    }

    [Fact]
    public void Validate_MissingSiteTitle_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Site = new SiteIntentSite { Name = "test", Title = "", BaseUrl = "/" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("site.title"));
    }

    [Fact]
    public void Validate_MissingBaseUrl_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Site = new SiteIntentSite { Name = "test", Title = "Test", BaseUrl = "" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("site.base_url"));
    }

    [Fact]
    public void Validate_BaseUrlNotStartingWithSlash_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Site = new SiteIntentSite { Name = "test", Title = "Test", BaseUrl = "abc" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("must start with '/'"));
    }

    [Fact]
    public void Validate_InvalidContentKindValue_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Content = new SiteIntentContent { Kind = "wordpress" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("content.kind"));
    }

    [Fact]
    public void Validate_MissingThemeName_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Theme = new SiteIntentTheme { Name = "" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("theme.name"));
    }

    [Fact]
    public void Validate_NotionContentKind_MissingDatabaseId_Fails()
    {
        var intent = new SiteIntent
        {
            Site = new SiteIntentSite { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = new SiteIntentContent
            {
                Kind = "notion",
                Notion = new SiteIntentNotionContent { DatabaseId = "" }
            },
            Theme = new SiteIntentTheme { Name = "starter" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("database_id"));
    }

    [Fact]
    public void Validate_ContentDirNotFound_ProducesWarning()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Content = new SiteIntentContent
            {
                Kind = "markdown",
                Markdown = new SiteIntentMarkdownContent { Dir = "nonexistent" }
            }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("not found"));
    }

    [Fact]
    public void Validate_UrlWithoutProtocol_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Site = new SiteIntentSite { Name = "test", Title = "Test", BaseUrl = "/", Url = "example.com" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("http:// or https://"));
    }

    [Fact]
    public void Validate_UrlWithHttps_Passes()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Site = new SiteIntentSite { Name = "test", Title = "Test", BaseUrl = "/", Url = "https://example.com" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidFieldPolicyMode_Fails()
    {
        var intent = new SiteIntent
        {
            Site = new SiteIntentSite { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = new SiteIntentContent
            {
                Kind = "notion",
                Notion = new SiteIntentNotionContent
                {
                    DatabaseId = "abc123",
                    FieldPolicy = new SiteIntentNotionFieldPolicy { Mode = "blocklist" }
                }
            },
            Theme = new SiteIntentTheme { Name = "starter" }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("field_policy.mode"));
    }

    [Fact]
    public void Validate_LanguagesMissingDefault_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Languages = new SiteIntentLanguages { Default = "", Supported = new[] { "zh-CN" } }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("languages.default"));
    }

    [Fact]
    public void Validate_LanguagesMissingSupported_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Languages = new SiteIntentLanguages { Default = "zh-CN", Supported = Array.Empty<string>() }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("languages.supported"));
    }

    [Fact]
    public void Validate_LanguagesDuplicates_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Languages = new SiteIntentLanguages { Default = "zh-CN", Supported = new[] { "zh-CN", "zh-CN" } }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicate language"));
    }

    [Fact]
    public void Validate_LanguagesDefaultNotInSupported_Fails()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Languages = new SiteIntentLanguages { Default = "en-US", Supported = new[] { "zh-CN" } }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("languages.default must be included"));
    }

    [Fact]
    public void Validate_BlogType_RssFalse_ProducesWarning()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Site = new SiteIntentSite { Name = "test", Title = "Test", BaseUrl = "/", Type = "blog" },
            Features = new SiteIntentFeatures { Rss = false }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("rss=false"));
    }

    [Fact]
    public void Validate_BlogType_SearchFalse_ProducesWarning()
    {
        var intent = CreateValidMarkdownIntent() with
        {
            Site = new SiteIntentSite { Name = "test", Title = "Test", BaseUrl = "/", Type = "blog" },
            Features = new SiteIntentFeatures { Search = false }
        };

        var result = IntentValidator.Validate(intent, _tempDir);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("search=false"));
    }
}
