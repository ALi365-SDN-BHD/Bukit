using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ConfigValidatorCoverageTests
{
    private static AppConfig ValidConfig(Func<AppConfig, AppConfig>? mutate = null)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = TestContent.Markdown()
        };
        return mutate != null ? mutate(config) : config;
    }

    private static AppConfig ConfigWithContent(Func<ContentConfig, ContentConfig> content) =>
        ValidConfig(c => c with { Content = NormalizeContentForSources(content(c.Content)) });

    private static ContentConfig NormalizeContentForSources(ContentConfig content)
    {
        if (content.Notion is not null ||
            content.Provider.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            return content with
            {
                Provider = "sources",
                Sources = new[]
                {
                    new ContentSourceConfig
                    {
                        Type = "notion",
                        Name = "page",
                        Collection = "page",
                        Notion = content.Notion ?? new NotionConfig { DatabaseId = "test-db" }
                    }
                }
            };
        }

        if (content.Markdown is not null)
        {
            return content with
            {
                Provider = "sources",
                Sources = new[]
                {
                    new ContentSourceConfig
                    {
                        Type = "markdown",
                        Name = "page",
                        Collection = "page",
                        Markdown = content.Markdown
                    }
                }
            };
        }

        return content;
    }

    private static void Validate(AppConfig config)
        => ConfigValidator.Validate(config with { Content = NormalizeContentForSources(config.Content) });

    private static AppConfig ConfigWithSite(Func<SiteConfig, SiteConfig> site) =>
        ValidConfig(c => c with { Site = site(c.Site) });

    private void WithNotionToken(Action action)
    {
        var previous = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, "test-token-for-validation");
            action();
        }
        finally
        {
            if (previous is null)
                Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);
            else
                Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, previous);
        }
    }

    private void WithoutNotionToken(Action action)
    {
        var previous = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);
            action();
        }
        finally
        {
            if (previous is not null)
                Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, previous);
        }
    }

    // ── Media validation ──────────────────────────────────────────

    [Fact]
    public void Validate_Media_DownloadDirPathTraversal_Throws()
    {
        var config = ValidConfig(c => c with
        {
            Content = c.Content with
            {
                Media = c.Content.Media with { DownloadDir = "../../outside" }
            }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.downloadDir", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_UrlBaseEmpty_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { UrlBase = "" } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.urlBase", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_DefaultImageUrlEmpty_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { DefaultImageUrl = "" } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.defaultImageUrl", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_FieldKeysNull_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { FieldKeys = null! } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.fieldKeys", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_MaxConcurrencyZero_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { MaxConcurrency = 0 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.maxConcurrency", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_MaxConcurrencyNegative_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { MaxConcurrency = -1 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.maxConcurrency", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_MaxRetriesNegative_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { MaxRetries = -1 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.maxRetries", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_TimeoutMsZero_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { TimeoutMs = 0 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.timeoutMs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_TimeoutMsNegative_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { TimeoutMs = -10 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.timeoutMs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_MaxFileSizeBytesZero_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { MaxFileSizeBytes = 0 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.maxFileSizeBytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_MaxFileSizeBytesNegative_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { MaxFileSizeBytes = -1024 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.maxFileSizeBytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_RetryBaseDelayMsNegative_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { RetryBaseDelayMs = -1 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.retryBaseDelayMs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Media_ValidDefaultValues_Passes()
    {
        var config = ValidConfig();

        var ex = Record.Exception(() => Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Media_DownloadDirAbsolute_Throws()
    {
        var config = ConfigWithContent(c => c with { Media = c.Media with { DownloadDir = "/etc/passwd" } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.media.downloadDir", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Markdown validation ───────────────────────────────────────

    [Fact]
    public void Validate_Markdown_DirEmpty_Throws()
    {
        var config = ConfigWithContent(c => c with { Markdown = new MarkdownConfig { Dir = "" } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.markdown.dir", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Markdown_DirPathTraversal_Throws()
    {
        var config = ConfigWithContent(c => c with { Markdown = new MarkdownConfig { Dir = "../escaped" } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.markdown.dir", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Markdown_IncludePathsWithTraversal_Throws()
    {
        var config = ConfigWithContent(c => c with
        {
            Markdown = new MarkdownConfig { IncludePaths = new[] { "posts/../../secret" } }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.markdown.includePaths", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Markdown_IncludeGlobsValid_Passes()
    {
        var config = ConfigWithContent(c => c with
        {
            Markdown = new MarkdownConfig { IncludeGlobs = new[] { "**/*.md", "posts/**/*.html" } }
        });

        var ex = Record.Exception(() => Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Markdown_IncludeGlobsEmptyElement_Throws()
    {
        var config = ConfigWithContent(c => c with
        {
            Markdown = new MarkdownConfig { IncludeGlobs = new[] { "**/*.md", "" } }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.markdown.includeGlobs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Markdown_IncludePathsEmptyElement_Throws()
    {
        var config = ConfigWithContent(c => c with
        {
            Markdown = new MarkdownConfig { IncludePaths = new[] { "  " } }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.markdown.includePaths", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Markdown_MaxItemsZero_Throws()
    {
        var config = ConfigWithContent(c => c with { Markdown = new MarkdownConfig { MaxItems = 0 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.markdown.maxItems", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Markdown_MaxItemsNegative_Throws()
    {
        var config = ConfigWithContent(c => c with { Markdown = new MarkdownConfig { MaxItems = -5 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("content.markdown.maxItems", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Notion validation ─────────────────────────────────────────

    [Fact]
    public void Validate_Notion_DatabaseIdEmpty_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "" }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.databaseId", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_DatabaseIdWhitespace_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "   " }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.databaseId", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_PageSizeZero_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", PageSize = 0 }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.pageSize", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_PageSizeNegative_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", PageSize = -1 }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.pageSize", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_PageSizeExceedsMax_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", PageSize = 101 }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.pageSize", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_FilterTypeInvalid_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", FilterType = "invalid" }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.filterType", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_SortDirectionInvalid_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig
                {
                    DatabaseId = "test-db",
                    SortProperty = "Created",
                    SortDirection = "random"
                }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.sortDirection", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_SortDirectionAscending_Passes()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig
                {
                    DatabaseId = "test-db",
                    SortProperty = "Created",
                    SortDirection = "ascending"
                }
            }
        };

        WithNotionToken(() =>
        {
            var ex = Record.Exception(() => Validate(config));
            Assert.Null(ex);
        });
    }

    [Fact]
    public void Validate_Notion_CacheModeInvalid_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", CacheMode = "memory" }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.cacheMode", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_CacheModeReadwrite_Passes()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", CacheMode = "readwrite" }
            }
        };

        WithNotionToken(() =>
        {
            var ex = Record.Exception(() => Validate(config));
            Assert.Null(ex);
        });
    }

    [Fact]
    public void Validate_Notion_CacheDirWhitespace_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", CacheDir = "  " }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.cacheDir", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_NotionTokenMissing_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db" }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("NOTION_TOKEN", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_WithToken_Passes()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db" }
            }
        };

        WithNotionToken(() =>
        {
            var ex = Record.Exception(() => Validate(config));
            Assert.Null(ex);
        });
    }

    [Fact]
    public void Validate_Notion_MaxItemsZero_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", MaxItems = 0 }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.maxItems", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_MaxRetriesNegative_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", MaxRetries = -1 }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.maxRetries", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_RenderConcurrencyZero_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", RenderConcurrency = 0 }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.renderConcurrency", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_MaxRpsZero_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig { DatabaseId = "test-db", MaxRps = 0 }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.maxRps", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_FieldPolicyModeInvalid_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig
                {
                    DatabaseId = "test-db",
                    FieldPolicy = new NotionFieldPolicyConfig { Mode = "blacklist" }
                }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.fieldPolicy.mode", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_IncludeSlugsWithoutSlugProperty_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig
                {
                    DatabaseId = "test-db",
                    IncludeSlugs = new[] { "my-slug" },
                    IncludeSlugProperty = ""
                }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.includeSlugProperty", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Validate_Notion_FilterTypeNoneWithoutFilterProperty_Passes()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig
                {
                    DatabaseId = "test-db",
                    FilterType = "none",
                    FilterProperty = ""
                }
            }
        };

        WithNotionToken(() =>
        {
            var ex = Record.Exception(() => Validate(config));
            Assert.Null(ex);
        });
    }

    [Fact]
    public void Validate_Notion_CheckboxTrueWithoutFilterProperty_Throws()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = new ContentConfig
            {
                Provider = "notion",
                Sources = TestContent.Notion().Sources,
                Notion = new NotionConfig
                {
                    DatabaseId = "test-db",
                    FilterType = "checkbox_true",
                    FilterProperty = ""
                }
            }
        };

        WithoutNotionToken(() =>
        {
            var ex = Assert.Throws<ConfigException>(() => Validate(config));
            Assert.Contains("content.notion.filterProperty", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── Taxonomy validation ───────────────────────────────────────

    [Fact]
    public void Validate_Taxonomy_TemplateEmpty_Throws()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { Template = "" } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.template", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_TemplateWhitespace_Throws()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { Template = "  " } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.template", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_OutputModeInvalid_Throws()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { OutputMode = "invalid" } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.outputMode", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("both")]
    [InlineData("pages")]
    [InlineData("data")]
    [InlineData("fields_only")]
    public void Validate_Taxonomy_OutputModeValid_Passes(string mode)
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { OutputMode = mode } });

        var ex = Record.Exception(() => Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Taxonomy_PageSizeZero_Throws()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { PageSize = 0 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.pageSize", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_PageSizeNegative_Throws()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { PageSize = -1 } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.pageSize", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_IndexTemplateWhitespace_Throws()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { IndexTemplate = "  " } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.indexTemplate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_IndexTemplateNull_Passes()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { IndexTemplate = null } });

        var ex = Record.Exception(() => Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Taxonomy_TermTemplateWhitespace_Throws()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { TermTemplate = "  " } });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.termTemplate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_TermTemplateNull_Passes()
    {
        var config = ValidConfig(c => c with { Taxonomy = new TaxonomyConfig { TermTemplate = null } });

        var ex = Record.Exception(() => Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Taxonomy_ItemFieldsEmptyElement_Throws()
    {
        var config = ValidConfig(c => c with
        {
            Taxonomy = new TaxonomyConfig { ItemFields = new[] { "tags", "", "categories" } }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.itemFields", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_ItemFieldsWhitespaceElement_Throws()
    {
        var config = ValidConfig(c => c with
        {
            Taxonomy = new TaxonomyConfig { ItemFields = new[] { "  " } }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.itemFields", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_ItemFieldsValid_Passes()
    {
        var config = ValidConfig(c => c with
        {
            Taxonomy = new TaxonomyConfig { ItemFields = new[] { "tags", "categories" } }
        });

        var ex = Record.Exception(() => Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Taxonomy_Kinds_KeyRequired_Throws()
    {
        var config = ValidConfig(c => c with
        {
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new[]
                {
                    new TaxonomyKindConfig { Key = "tags" },
                    new TaxonomyKindConfig { Key = "" }
                }
            }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.kinds[1].key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Taxonomy_Kinds_KeyWhitespace_Throws()
    {
        var config = ValidConfig(c => c with
        {
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new[] { new TaxonomyKindConfig { Key = "   " } }
            }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("taxonomy.kinds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("auto")]
    [InlineData("always")]
    [InlineData("never")]
    public void Validate_ListPageContentMode_ValidValues_Passes(string? value)
    {
        var config = ValidConfig(c => c with
        {
            Build = new BuildConfig { Output = "dist", ListPageContentMode = value! }
        });

        var ex = Record.Exception(() => Validate(config));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("all")]
    [InlineData("none")]
    public void Validate_ListPageContentMode_InvalidValues_Throws(string value)
    {
        var config = ValidConfig(c => c with
        {
            Build = new BuildConfig { Output = "dist", ListPageContentMode = value }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("listPageContentMode", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("debug")]
    [InlineData("info")]
    [InlineData("warn")]
    [InlineData("error")]
    public void Validate_LoggingLevel_ValidValues_Passes(string? value)
    {
        var config = ValidConfig(c => c with
        {
            Build = new BuildConfig { Output = "dist" },
            Logging = new LoggingConfig { Level = value! }
        });

        var ex = Record.Exception(() => Validate(config));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("fatal")]
    [InlineData("trace")]
    public void Validate_LoggingLevel_InvalidValues_Throws(string value)
    {
        var config = ValidConfig(c => c with
        {
            Build = new BuildConfig { Output = "dist" },
            Logging = new LoggingConfig { Level = value }
        });

        var ex = Assert.Throws<ConfigException>(() => Validate(config));
        Assert.Contains("logging.level", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
