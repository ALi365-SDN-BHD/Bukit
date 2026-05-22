using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigLoaderTests : IDisposable
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
    public void Load_ValidMinimalConfig_ReturnsAppConfig()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              provider: markdown
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config);
        Assert.Equal("myblog", config.Site.Name);
        Assert.Equal("My Blog", config.Site.Title);
        Assert.Equal("markdown", config.Content.Provider);
        Assert.NotNull(config.Content.Markdown);
    }

    [Fact]
    public void Load_EmptyPath_ThrowsConfigException()
    {
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(""));
        Assert.Equal("Config path is required.", ex.Message);
    }

    [Fact]
    public void Load_WhitespacePath_ThrowsConfigException()
    {
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load("   "));
        Assert.Equal("Config path is required.", ex.Message);
    }

    [Fact]
    public void Load_NonExistentFile_ThrowsConfigException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-nonexistent-{Guid.NewGuid():N}.yaml");
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains("Config file not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(path, ex.Message);
    }

    [Fact]
    public void Load_YamlSyntaxError_ThrowsConfigException()
    {
        var yaml = """
            site:
              name: myblog
             title: invalid indent
            """;
        var path = WriteTempYaml(yaml);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains("Invalid YAML syntax", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(ex.InnerException);
        Assert.IsAssignableFrom<YamlDotNet.Core.YamlException>(ex.InnerException);
    }

    [Fact]
    public void Load_EmptyFile_ThrowsConfigException()
    {
        var path = WriteTempYaml("");
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Equal("Config file is empty.", ex.Message);
    }

    [Fact]
    public void Load_NonMappingRoot_ThrowsConfigException()
    {
        var yaml = """
            - item1
            - item2
            """;
        var path = WriteTempYaml(yaml);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Equal("Config root must be a mapping.", ex.Message);
    }

    [Fact]
    public void Load_MissingSiteSection_ThrowsConfigException()
    {
        var yaml = """
            content:
              provider: markdown
            """;
        var path = WriteTempYaml(yaml);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains("site section is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingSiteName_ThrowsConfigException()
    {
        var yaml = """
            site:
              title: My Blog
            content:
              provider: markdown
            """;
        var path = WriteTempYaml(yaml);

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));
        Assert.Contains("name is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_NotionProvider_PopulatesNotionConfig()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              provider: notion
              notion:
                databaseId: abc123def456
                pageSize: 100
                maxItems: 500
                renderContent: true
                maxRps: 3
                maxRetries: 5
                cacheMode: file
                cacheDir: /tmp/notion-cache
                sortProperty: Created
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.Equal("notion", config.Content.Provider);
        Assert.NotNull(config.Content.Notion);
        Assert.Equal("abc123def456", config.Content.Notion.DatabaseId);
        Assert.Equal(100, config.Content.Notion.PageSize);
        Assert.Equal(500, config.Content.Notion.MaxItems);
        Assert.True(config.Content.Notion.RenderContent);
        Assert.Equal(3, config.Content.Notion.MaxRps);
        Assert.Equal(5, config.Content.Notion.MaxRetries);
        Assert.Equal("file", config.Content.Notion.CacheMode);
        Assert.Equal("/tmp/notion-cache", config.Content.Notion.CacheDir);
        Assert.Equal("Created", config.Content.Notion.SortProperty);
        Assert.Null(config.Content.Markdown);
    }

    [Fact]
    public void Load_MarkdownProvider_PopulatesMarkdownConfig()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              provider: markdown
              markdown:
                dir: docs
                defaultType: post
                maxItems: 200
                includePaths:
                  - posts
                  - pages
                includeGlobs:
                  - "*.md"
                  - "*.markdown"
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.Equal("markdown", config.Content.Provider);
        Assert.NotNull(config.Content.Markdown);
        Assert.Equal("docs", config.Content.Markdown.Dir);
        Assert.Equal("post", config.Content.Markdown.DefaultType);
        Assert.Equal(200, config.Content.Markdown.MaxItems);
        Assert.NotNull(config.Content.Markdown.IncludePaths);
        Assert.Equal(2, config.Content.Markdown.IncludePaths.Count);
        Assert.Contains("posts", config.Content.Markdown.IncludePaths);
        Assert.Contains("pages", config.Content.Markdown.IncludePaths);
        Assert.NotNull(config.Content.Markdown.IncludeGlobs);
        Assert.Equal(2, config.Content.Markdown.IncludeGlobs.Count);
        Assert.Contains("*.md", config.Content.Markdown.IncludeGlobs);
        Assert.Contains("*.markdown", config.Content.Markdown.IncludeGlobs);
    }

    [Fact]
    public void Load_Collections_ParsesCollectionConfigs()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              collections:
                posts:
                  permalink: /blog/{year}/{month}/{slug}/
                  template: pages/post.html
                  listRoute: /blog/
                  listTemplate: pages/blog-list.html
                  pagination:
                    enabled: true
                    pageSize: 15
                  output:
                    rss: true
                    sitemap: true
                    archive: true
                pages:
                  permalink: /{slug}/
                  template: pages/page.html
            content:
              provider: markdown
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Site.Collections);
        Assert.Equal(2, config.Site.Collections.Count);

        var posts = config.Site.Collections["posts"];
        Assert.Equal("/blog/{year}/{month}/{slug}/", posts.Permalink);
        Assert.Equal("pages/post.html", posts.Template);
        Assert.Equal("/blog/", posts.ListRoute);
        Assert.Equal("pages/blog-list.html", posts.ListTemplate);
        Assert.True(posts.Pagination.Enabled);
        Assert.Equal(15, posts.Pagination.PageSize);
        Assert.True(posts.Output.Rss);
        Assert.True(posts.Output.Sitemap);
        Assert.True(posts.Output.Archive);

        var pages = config.Site.Collections["pages"];
        Assert.Equal("/{slug}/", pages.Permalink);
        Assert.Equal("pages/page.html", pages.Template);
        Assert.False(pages.Pagination.Enabled);
        Assert.Equal(10, pages.Pagination.PageSize);
    }

    [Fact]
    public void Load_ContentSources_ParsesCollectionMappings()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  name: companies-db
                  mode: content
                  collection: companies
                  addToCollections:
                    - china_companies
                    - malaysia_companies
                  notion:
                    databaseId: db-companies
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        var source = Assert.Single(config.Content.Sources!);
        Assert.Equal("companies", source.Collection);
        Assert.NotNull(source.AddToCollections);
        Assert.Equal(new[] { "china_companies", "malaysia_companies" }, source.AddToCollections);
    }

    [Fact]
    public void Load_Permalinks_ParsesPermalinksMap()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              permalinks:
                post: "/{year}/{month}/{slug}/"
                page: "/docs/{slug}/"
                custom: "/special/{slug}.html"
            content:
              provider: markdown
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Site.Permalinks);
        Assert.Equal(3, config.Site.Permalinks.Count);
        Assert.Equal("/{year}/{month}/{slug}/", config.Site.Permalinks["post"]);
        Assert.Equal("/docs/{slug}/", config.Site.Permalinks["page"]);
        Assert.Equal("/special/{slug}.html", config.Site.Permalinks["custom"]);
    }

    [Fact]
    public void Load_DeploymentConfig_ParsesDeployConfig()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              provider: markdown
            deploy:
              provider: github-pages
              branch: main
              message: Deploy via Bukit
              cname: example.com
              keepHistory: true
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Deploy);
        Assert.Equal("github-pages", config.Deploy.Provider);
        Assert.Equal("main", config.Deploy.Branch);
        Assert.Equal("Deploy via Bukit", config.Deploy.Message);
        Assert.Equal("example.com", config.Deploy.Cname);
        Assert.True(config.Deploy.KeepHistory);
    }

    [Fact]
    public void Load_SeoConfig_WithOrganization_ParsesSeoConfig()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              url: https://example.com
              seo:
                enabled: true
                renderMode: inject
                diagnostics: strict
                defaultImage: /assets/og.png
                twitterSite: "@myblog"
                organization:
                  name: Example Inc
                  url: https://example.com/about
                  logo: https://example.com/logo.png
                robotsTxt:
                  enabled: true
                schema:
                  webPage: true
                  collectionPage: false
                  searchAction: true
            content:
              provider: markdown
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.True(config.Site.Seo.Enabled);
        Assert.Equal("inject", config.Site.Seo.RenderMode);
        Assert.Equal("strict", config.Site.Seo.Diagnostics);
        Assert.Equal("/assets/og.png", config.Site.Seo.DefaultImage);
        Assert.Equal("@myblog", config.Site.Seo.TwitterSite);
        Assert.NotNull(config.Site.Seo.Organization);
        Assert.Equal("Example Inc", config.Site.Seo.Organization.Name);
        Assert.Equal("https://example.com/about", config.Site.Seo.Organization.Url);
        Assert.Equal("https://example.com/logo.png", config.Site.Seo.Organization.Logo);
        Assert.True(config.Site.Seo.RobotsTxt.Enabled);
        Assert.True(config.Site.Seo.Schema.WebPage);
        Assert.False(config.Site.Seo.Schema.CollectionPage);
        Assert.True(config.Site.Seo.Schema.SearchAction);
    }

    [Fact]
    public void Load_ExternalPlugins_ParsesPluginConfigs()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
              externalPlugins:
                sitemap-generator:
                  runtime: process
                  entry: plugins/sitemap.exe
                  hooks:
                    - after-build
                  enabled: true
                  timeoutMs: 10000
                  options:
                    mode: full
                    pretty: true
                image-optimizer:
                  runtime: wasm
                  entry: plugins/optimizer.wasm
                  hooks:
                    - after-build
                    - derive-pages
                  wasmProfile: wasi-preview1
                  maxMemoryMb: 128
                  wasmFsMode: output-only
                  wasmAllowNetwork: false
                  capabilities:
                    - emit-outputs
            content:
              provider: markdown
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.Site.ExternalPlugins);
        Assert.Equal(2, config.Site.ExternalPlugins.Count);

        var sitemap = config.Site.ExternalPlugins["sitemap-generator"];
        Assert.Equal("process", sitemap.Runtime);
        Assert.Equal("plugins/sitemap.exe", sitemap.Entry);
        Assert.NotNull(sitemap.Hooks);
        Assert.Contains("after-build", sitemap.Hooks);
        Assert.True(sitemap.Enabled);
        Assert.Equal(10000, sitemap.TimeoutMs);
        Assert.NotNull(sitemap.Options);
        Assert.Equal("full", sitemap.Options["mode"]);
        Assert.Equal("true", sitemap.Options["pretty"]);

        var optimizer = config.Site.ExternalPlugins["image-optimizer"];
        Assert.Equal("wasm", optimizer.Runtime);
        Assert.Equal("plugins/optimizer.wasm", optimizer.Entry);
        Assert.Equal(2, optimizer.Hooks.Count);
        Assert.Contains("after-build", optimizer.Hooks);
        Assert.Contains("derive-pages", optimizer.Hooks);
        // DESKTOP-REMOVED: wasm runtime fields disabled (AOT-only).
        // Assert.Equal("wasi-preview1", optimizer.WasmProfile);
        // Assert.Equal(128, optimizer.MaxMemoryMb);
        // Assert.Equal("output-only", optimizer.WasmFsMode);
        // Assert.False(optimizer.WasmAllowNetwork);
        // Assert.NotNull(optimizer.Capabilities);
        // Assert.Contains("emit-outputs", optimizer.Capabilities);
    }

    [Fact]
    public void Load_TaxonomyConfig_WithKinds_ParsesTaxonomyConfig()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              provider: markdown
            taxonomy:
              template: pages/taxonomy.html
              indexTemplate: pages/index.html
              termTemplate: pages/term.html
              outputMode: both
              indexEnabled: true
              pageSize: 20
              pinField: sticky
              pinOrderField: weight
              kinds:
                - key: tags
                  kind: tag
                  title: Tags
                  singularTitlePrefix: "Tag: "
                  template: pages/tag.html
                  indexTemplate: pages/tag-index.html
                  termTemplate: pages/tag-term.html
                  indexEnabled: true
                - key: categories
                  kind: category
                  title: Categories
                  indexEnabled: true
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        Assert.Equal("pages/taxonomy.html", config.Taxonomy.Template);
        Assert.Equal("pages/index.html", config.Taxonomy.IndexTemplate);
        Assert.Equal("pages/term.html", config.Taxonomy.TermTemplate);
        Assert.Equal("both", config.Taxonomy.OutputMode);
        Assert.True(config.Taxonomy.IndexEnabled);
        Assert.Equal(20, config.Taxonomy.PageSize);
        Assert.Equal("sticky", config.Taxonomy.PinField);
        Assert.Equal("weight", config.Taxonomy.PinOrderField);

        Assert.NotNull(config.Taxonomy.Kinds);
        Assert.Equal(2, config.Taxonomy.Kinds.Count);

        var tags = config.Taxonomy.Kinds[0];
        Assert.Equal("tags", tags.Key);
        Assert.Equal("tag", tags.Kind);
        Assert.Equal("Tags", tags.Title);
        Assert.Equal("Tag: ", tags.SingularTitlePrefix);
        Assert.Equal("pages/tag.html", tags.Template);
        Assert.Equal("pages/tag-index.html", tags.IndexTemplate);
        Assert.Equal("pages/tag-term.html", tags.TermTemplate);
        Assert.True(tags.IndexEnabled);

        var categories = config.Taxonomy.Kinds[1];
        Assert.Equal("categories", categories.Key);
        Assert.Equal("category", categories.Kind);
        Assert.Equal("Categories", categories.Title);
        Assert.True(categories.IndexEnabled);
    }

    [Fact]
    public void Load_MediaConfig_ParsesMediaConfig()
    {
        var yaml = """
            site:
              name: myblog
              title: My Blog
            content:
              provider: markdown
              media:
                downloadToLocal: true
                downloadDir: static/images
                urlBase: /images
                defaultImageUrl: /images/default.jpg
                fieldKeys:
                  - cover
                  - image
                  - thumbnail
                  - banner
                maxConcurrency: 8
                maxRetries: 5
                timeoutMs: 15000
                maxFileSizeBytes: 10485760
                blockPrivateNetworks: false
                retryBaseDelayMs: 1000
            """;
        var path = WriteTempYaml(yaml);

        var config = ConfigLoader.Load(path);

        var media = config.Content.Media;
        Assert.True(media.DownloadToLocal);
        Assert.Equal("static/images", media.DownloadDir);
        Assert.Equal("/images", media.UrlBase);
        Assert.Equal("/images/default.jpg", media.DefaultImageUrl);
        Assert.NotNull(media.FieldKeys);
        Assert.Equal(4, media.FieldKeys.Count);
        Assert.Contains("cover", media.FieldKeys);
        Assert.Contains("image", media.FieldKeys);
        Assert.Contains("thumbnail", media.FieldKeys);
        Assert.Contains("banner", media.FieldKeys);
        Assert.Equal(8, media.MaxConcurrency);
        Assert.Equal(5, media.MaxRetries);
        Assert.Equal(15000, media.TimeoutMs);
        Assert.Equal(10485760, media.MaxFileSizeBytes);
        Assert.False(media.BlockPrivateNetworks);
        Assert.Equal(1000, media.RetryBaseDelayMs);
    }
}
