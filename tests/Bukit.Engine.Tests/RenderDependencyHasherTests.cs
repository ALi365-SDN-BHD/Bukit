using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RenderDependencyHasherTests
{
    private static readonly SiteModel s_emptySiteModel = new()
    {
        Name = "test",
        Title = "test",
        BaseUrl = "/",
        Language = "en"
    };

    private static AppConfig CreateBaseConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test Site"
            },
            Content = new ContentConfig
            {
                Provider = "markdown",
                Markdown = new MarkdownConfig { Dir = "content" },
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = new BuildConfig { Output = "dist" },
            Theme = new ThemeConfig { Layouts = "layouts" }
        };
    }

    [Fact]
    public void Compute_SameConfig_ProducesSameHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig();

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentUrl_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Url = "https://other.com" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_UrlNull_DoesNotThrow()
    {
        var config = CreateBaseConfig();

        var hash = RenderDependencyHasher.Compute(config, s_emptySiteModel);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Compute_DifferentLanguages_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Languages = new[] { "en", "zh", "fr" } }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_LanguagesOrder_ProducesSameHash()
    {
        var config1 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Languages = new[] { "en", "zh" } }
        };
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Languages = new[] { "zh", "en" } }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_LanguagesNull_DoesNotThrow()
    {
        var config = CreateBaseConfig();

        var hash = RenderDependencyHasher.Compute(config, s_emptySiteModel);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Compute_LanguagesEmpty_DoesNotThrow()
    {
        var config = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Languages = Array.Empty<string>() }
        };

        var hash = RenderDependencyHasher.Compute(config, s_emptySiteModel);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Compute_DifferentDefaultLanguage_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { DefaultLanguage = "zh" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DefaultLanguageNull_DoesNotThrow()
    {
        var config = CreateBaseConfig();

        var hash = RenderDependencyHasher.Compute(config, s_emptySiteModel);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Compute_DifferentSitemapMode_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { SitemapMode = "merged" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentRssMode_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { RssMode = "merged" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentSearchMode_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { SearchMode = "merged" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_ExistingFieldsStillAffectHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Title = "Changed Title" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }
}
