using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigLoaderFullCoverageTests : IDisposable
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
        var path = Path.Combine(Path.GetTempPath(), $"bukit-config-test-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Load_SiteLanguages_ParsesStringList()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              languages:
                - en
                - zh-CN
                - ja
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Site.Languages);
        Assert.Equal(3, config.Site.Languages.Count);
        Assert.Equal("en", config.Site.Languages[0]);
        Assert.Equal("zh-CN", config.Site.Languages[1]);
        Assert.Equal("ja", config.Site.Languages[2]);
    }

    [Fact]
    public void Load_SiteLanguages_EmptyList_ReturnsNull()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              languages: []
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.Null(config.Site.Languages);
    }

    [Fact]
    public void Load_SiteDefaultLanguage_ParsesValue()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              defaultLanguage: en
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.Equal("en", config.Site.DefaultLanguage);
    }

    [Fact]
    public void Load_SiteAutoSummary_ParsesBoolAndMaxLength()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              autoSummary: true
              autoSummaryMaxLength: 150
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.True(config.Site.AutoSummary);
        Assert.Equal(150, config.Site.AutoSummaryMaxLength);
    }

    [Fact]
    public void Load_SiteAutoSummary_Defaults()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.False(config.Site.AutoSummary);
        Assert.Equal(200, config.Site.AutoSummaryMaxLength);
    }

    [Fact]
    public void Load_SiteSearchIncludeDerived_True()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              searchIncludeDerived: true
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.True(config.Site.SearchIncludeDerived);
    }

    [Fact]
    public void Load_ExternalProtocolIncludeRoutedPages_ThrowsConfigException()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              externalProtocolIncludeRoutedPages: true
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains("site.externalProtocolIncludeRoutedPages", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_SiteSitemapFeedSearchConfig_CustomValues()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              sitemapMode: full
              feed:
                formats: [rss, atom]
                limit: 7
                path: feeds
              search:
                mode: merged
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.Equal("full", config.Site.SitemapMode);
        Assert.Equal(["rss", "atom"], config.Site.Feed.Formats);
        Assert.Equal(7, config.Site.Feed.Limit);
        Assert.Equal("feeds", config.Site.Feed.Path);
        Assert.Equal("merged", config.Site.Search.Mode);
    }

    [Fact]
    public void Load_SitePluginFailModeAndDeriveConflictPolicy_CustomValues()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              pluginFailMode: warn
              deriveConflictPolicy: skip
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.Equal("warn", config.Site.PluginFailMode);
        Assert.Equal("skip", config.Site.DeriveConflictPolicy);
    }

    [Fact]
    public void Load_ThemeConfig_CustomValues()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            theme:
              name: my-custom-theme
              source: "https://example.com/themes.git@v1.2.3"
              layouts: _layouts
              assets: _assets
              static: public
              staticTemplate: pages/static.html
              componentValidation: strict
        """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.Equal("my-custom-theme", config.Theme.Name);
        Assert.Equal("https://example.com/themes.git@v1.2.3", config.Theme.Source);
        Assert.Equal("_layouts", config.Theme.Layouts);
        Assert.Equal("_assets", config.Theme.Assets);
        Assert.Equal("public", config.Theme.Static);
        Assert.Equal("pages/static.html", config.Theme.StaticTemplate);
        Assert.Equal("strict", config.Theme.ComponentValidation);
    }

    [Fact]
    public void Load_ThemeParams_WithNestedObjectTreeAndArray()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            theme:
              params:
                primaryColor: "#3498db"
                fontSize: 16
                navLinks:
                  - label: Home
                    url: /
                  - label: About
                    url: /about
                footer:
                  copyright: "2024 My Blog"
                  social:
                    twitter: "@myblog"
                    github: myblog
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Theme.Params);
        Assert.Equal("#3498db", config.Theme.Params["primaryColor"]);
        Assert.Equal("16", config.Theme.Params["fontSize"]);

        var navLinks = Assert.IsAssignableFrom<List<object>>(config.Theme.Params["navLinks"]);
        Assert.Equal(2, navLinks.Count);

        var footer = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(config.Theme.Params["footer"]);
        Assert.Equal("2024 My Blog", footer["copyright"]);
        var social = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(footer["social"]);
        Assert.Equal("@myblog", social["twitter"]);
        Assert.Equal("myblog", social["github"]);
    }

    [Fact]
    public void Load_LoggingLevel_CustomValues()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            logging:
              level: debug
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.Equal("debug", config.Logging.Level);
    }

    [Fact]
    public void Load_LoggingLevel_Warn()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            logging:
              level: warn
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.Equal("warn", config.Logging.Level);
    }

    [Fact]
    public void Load_LoggingLevel_Error()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            logging:
              level: error
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.Equal("error", config.Logging.Level);
    }

    [Fact]
    public void Load_Plugins_BooleanToggles()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              plugins:
                seo: true
                sitemap: false
                feed: true
                openGraph: false
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Site.Plugins);
        Assert.Equal(4, config.Site.Plugins.Count);
        Assert.True(config.Site.Plugins["seo"].Enabled);
        Assert.False(config.Site.Plugins["sitemap"].Enabled);
        Assert.True(config.Site.Plugins["feed"].Enabled);
        Assert.False(config.Site.Plugins["openGraph"].Enabled);
        Assert.Null(config.Site.Plugins["seo"].Options);
    }

    [Fact]
    public void Load_Plugins_MappingWithOptionsAndDisabled()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              plugins:
                search:
                  enabled: true
                  options:
                    indexMode: full
                    maxTokens: 500
                comments:
                  enabled: false
                  options:
                    provider: disqus
                    shortname: mysite
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Site.Plugins);
        Assert.Equal(2, config.Site.Plugins.Count);

        var search = config.Site.Plugins["search"];
        Assert.True(search.Enabled);
        Assert.NotNull(search.Options);
        Assert.Equal("full", search.Options["indexMode"]);
        Assert.Equal("500", search.Options["maxTokens"]);

        var comments = config.Site.Plugins["comments"];
        Assert.False(comments.Enabled);
        Assert.NotNull(comments.Options);
        Assert.Equal("disqus", comments.Options["provider"]);
        Assert.Equal("mysite", comments.Options["shortname"]);
    }

    [Fact]
    public void Load_TaxonomyTemplates_ThrowsUnknownField()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            taxonomy:
              unexpectedField: pages/tag.html
            """;
        var path = WriteTempYaml(yaml);
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("taxonomy.unexpectedField", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_TaxonomyItemFields_ThrowsUnknownField()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            taxonomy:
              itemFields:
                - tags
                - categories
                - series
            """;
        var path = WriteTempYaml(yaml);
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("taxonomy.itemFields", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_TaxonomyPinFieldAndPinOrderField_ThrowsUnknownField()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            taxonomy:
              pinField: featured
              pinOrderField: priority
            """;
        var path = WriteTempYaml(yaml);
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("taxonomy.pinField", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_TaxonomyPinFieldBySource_ThrowsUnknownField()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            taxonomy:
              pinFieldBySource:
                notion: NotionPinned
                markdown: frontmatter_pinned
            """;
        var path = WriteTempYaml(yaml);
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("taxonomy.pinFieldBySource", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_TaxonomyPinOrderFieldBySource_ThrowsUnknownField()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            taxonomy:
              pinOrderFieldBySource:
                notion: NotionOrder
                markdown: weight
            """;
        var path = WriteTempYaml(yaml);
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("taxonomy.pinOrderFieldBySource", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_NotionFieldPolicy_WhitelistModeWithAllowedList()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  notion:
                    databaseId: abc123
                    fieldPolicy:
                      mode: whitelist
                      allowed:
                        - title
                        - slug
                        - tags
                        - published
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);
        var source = config.Content.Sources![0].Notion;

        Assert.NotNull(source);
        Assert.Equal("whitelist", source.FieldPolicy.Mode);
        Assert.NotNull(source.FieldPolicy.Allowed);
        Assert.Equal(4, source.FieldPolicy.Allowed!.Count);
        Assert.Contains("title", source.FieldPolicy.Allowed);
        Assert.Contains("slug", source.FieldPolicy.Allowed);
        Assert.Contains("tags", source.FieldPolicy.Allowed);
        Assert.Contains("published", source.FieldPolicy.Allowed);
    }

    [Fact]
    public void Load_NotionFieldPolicy_AllMode()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  notion:
                    databaseId: abc123
                    fieldPolicy:
                      mode: all
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);
        var source = config.Content.Sources![0].Notion;

        Assert.NotNull(source);
        Assert.Equal("all", source.FieldPolicy.Mode);
        Assert.Null(source.FieldPolicy.Allowed);
    }

    [Fact]
    public void Load_ContentSources_MultipleSources()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  name: blog-db
                  mode: content
                  notion:
                    databaseId: db-blog
                    pageSize: 50
                - type: markdown
                  name: docs
                  mode: content
                  markdown:
                    dir: docs
                    defaultType: docs
                - type: notion
                  name: archive-db
                  mode: data
                  notion:
                    databaseId: db-archive
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Content.Sources);
        Assert.Equal(3, config.Content.Sources.Count);

        var s0 = config.Content.Sources[0];
        Assert.Equal("notion", s0.Type);
        Assert.Equal("blog-db", s0.Name);
        Assert.Equal("content", s0.Mode);
        Assert.NotNull(s0.Notion);
        Assert.Equal("db-blog", s0.Notion.DatabaseId);
        Assert.Equal(50, s0.Notion.PageSize);

        var s1 = config.Content.Sources[1];
        Assert.Equal("markdown", s1.Type);
        Assert.Equal("docs", s1.Name);
        Assert.Equal("content", s1.Mode);
        Assert.NotNull(s1.Markdown);
        Assert.Equal("docs", s1.Markdown.Dir);
        Assert.Equal("docs", s1.Markdown.DefaultType);

        var s2 = config.Content.Sources[2];
        Assert.Equal("notion", s2.Type);
        Assert.Equal("archive-db", s2.Name);
        Assert.Equal("data", s2.Mode);
        Assert.NotNull(s2.Notion);
        Assert.Equal("db-archive", s2.Notion.DatabaseId);
    }

    [Fact]
    public void Load_DeployOptions_RejectsUnsupportedProvider()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "myblog", Title = "My Blog" },
            Content = new ContentConfig
            {
                Sources =
                [
                    new ContentSourceConfig
                    {
                        Type = "markdown",
                        Markdown = new MarkdownConfig { Dir = "content" }
                    }
                ]
            },
            Deploy = new DeployConfig { Provider = "custom" }
        };
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("deploy.provider must be 'github-pages' in Bukit 1.0.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_NotionNumericFields_RenderConcurrencyMaxRpsMaxRetries()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  notion:
                    databaseId: abc123
                    renderConcurrency: 4
                    maxRps: 2
                    maxRetries: 8
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);
        var source = config.Content.Sources![0].Notion;

        Assert.NotNull(source);
        Assert.Equal(4, source.RenderConcurrency);
        Assert.Equal(2, source.MaxRps);
        Assert.Equal(8, source.MaxRetries);
    }

    [Fact]
    public void Load_BuildReportEnabled_ParsesTrue()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            build:
              report:
                enabled: true
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.True(config.Build.Report.Enabled);
    }

    [Fact]
    public void Load_BuildReportEnabled_DefaultsToTrue()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);

        Assert.True(config.Build.Report.Enabled);
    }

    [Fact]
    public void Load_NotionCacheModeAndCacheDir_CustomValues()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  notion:
                    databaseId: abc123
                    cacheMode: memory
                    cacheDir: /var/cache/bukit
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);
        var source = config.Content.Sources![0].Notion;

        Assert.NotNull(source);
        Assert.Equal("memory", source.CacheMode);
        Assert.Equal("/var/cache/bukit", source.CacheDir);
    }

    [Fact]
    public void Load_NotionIncludeSlugsAndSlugProperty()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  notion:
                    databaseId: abc123
                    includeSlugs:
                      - about
                      - contact
                      - privacy
                    includeSlugProperty: CustomSlug
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);
        var source = config.Content.Sources![0].Notion;

        Assert.NotNull(source);
        Assert.NotNull(source.IncludeSlugs);
        Assert.Equal(3, source.IncludeSlugs.Count);
        Assert.Contains("about", source.IncludeSlugs);
        Assert.Contains("contact", source.IncludeSlugs);
        Assert.Contains("privacy", source.IncludeSlugs);
        Assert.Equal("CustomSlug", source.IncludeSlugProperty);
    }

    [Fact]
    public void Load_NotionFilterAndSort_CustomValues()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  notion:
                    databaseId: abc123
                    filterProperty: Status
                    filterType: select_equals
                    filterValue: Published
                    sortProperty: Updated
                    sortDirection: descending
            """;
        var path = WriteTempYaml(yaml);
        var config = ConfigLoader.Load(path);
        var source = config.Content.Sources![0].Notion;

        Assert.NotNull(source);
        Assert.Equal("Status", source.FilterProperty);
        Assert.Equal("select_equals", source.FilterType);
        Assert.Equal("Published", source.FilterValue);
        Assert.Equal("Updated", source.SortProperty);
        Assert.Equal("descending", source.SortDirection);
    }
}
