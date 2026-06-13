using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SchemaFailModeTests : IDisposable
{
    private readonly string _rootDir;

    public SchemaFailModeTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-schema-failmode-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public void CollectionConfig_HasSchemaFailModeField()
    {
        var config = new CollectionConfig
        {
            Permalink = "/posts/{slug}/",
            Template = "post.html",
            SchemaFailMode = "strict"
        };

        Assert.Equal("strict", config.SchemaFailMode);
    }

    [Fact]
    public void CollectionConfig_SchemaFailMode_DefaultsToNull()
    {
        var config = new CollectionConfig
        {
            Permalink = "/posts/{slug}/",
            Template = "post.html"
        };

        Assert.Null(config.SchemaFailMode);
    }

    [Fact]
    public void ConfigLoader_ReadsSchemaFailModeFromYaml()
    {
        var yaml = """
            site:
              name: test-site
              title: Test
              language: en
              baseUrl: /
              collections:
                posts:
                  permalink: /posts/{slug}/
                  template: post.html
                  schemaFailMode: strict
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            """;

        var configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(configPath, yaml);

        var config = ConfigLoader.Load(configPath);

        Assert.NotNull(config.Site.Collections);
        Assert.True(config.Site.Collections.TryGetValue("posts", out var posts));
        Assert.Equal("strict", posts.SchemaFailMode);
    }

    [Fact]
    public void ConfigLoader_SiteCollections_ReadsSchemaFailModeFromYaml()
    {
        var siteYaml = """
            site:
              name: test-site
              title: Test
              language: en
              baseUrl: /
              collections:
                posts:
                  permalink: /posts/{slug}/
                  template: post.html
                  schemaFailMode: strict
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            """;

        File.WriteAllText(Path.Combine(_rootDir, "site.yaml"), siteYaml);

        var config = ConfigLoader.Load(Path.Combine(_rootDir, "site.yaml"));

        Assert.NotNull(config.Site.Collections);
        Assert.True(config.Site.Collections.TryGetValue("posts", out var posts));
        Assert.Equal("strict", posts.SchemaFailMode);
    }

    [Fact]
    public void ConfigValidator_AcceptsValidSchemaFailModeValues()
    {
        var config = CreateConfigWithCollectionMode("warn");
        ConfigValidator.Validate(config);
    }

    [Fact]
    public void ConfigValidator_RejectsInvalidSchemaFailModeValue()
    {
        var config = CreateConfigWithCollectionMode("panic");

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("schemaFailMode", ex.Message);
        Assert.Contains("off|warn|strict", ex.Message);
    }

    [Fact]
    public void ResolveCollectionSchemaFailMode_OverridesGlobal()
    {
        var collection = new CollectionConfig
        {
            Permalink = "/posts/{slug}/",
            Template = "post.html",
            SchemaFailMode = "strict"
        };

        var result = ContentSchemaValidator.ResolveSchemaFailMode(collection, "warn");
        Assert.Equal("strict", result);
    }

    [Fact]
    public void ResolveCollectionSchemaFailMode_FallsBackToGlobal()
    {
        var collection = new CollectionConfig
        {
            Permalink = "/posts/{slug}/",
            Template = "post.html",
            SchemaFailMode = null
        };

        var result = ContentSchemaValidator.ResolveSchemaFailMode(collection, "strict");
        Assert.Equal("strict", result);
    }

    private static AppConfig CreateConfigWithCollectionMode(string mode)
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Language = "en",
                BaseUrl = "/",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["posts"] = new()
                    {
                        Permalink = "/posts/{slug}/",
                        Template = "post.html",
                        SchemaFailMode = mode
                    }
                }
            },
            Content = TestContent.Markdown(),
            Build = new BuildConfig { Output = "dist" }
        };
    }
}
