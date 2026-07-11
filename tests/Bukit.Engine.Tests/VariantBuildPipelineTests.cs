using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class VariantBuildPipelineTests : IDisposable
{
    private readonly string _rootDir;

    public VariantBuildPipelineTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-variant-pipeline-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    private static AppConfig CreateMinimalConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test Site", BaseUrl = "/", Language = "en" },
            Content = TestContent.Markdown(),
        };
    }

    [Fact]
    public void Pipeline_CanBeConstructed()
    {
        var pipeline = new VariantBuildPipeline();
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void PrepareDataModules_EmptyItems_ReturnsEmptyResult()
    {
        var pipeline = new VariantBuildPipeline();
        var documents = new List<ContentDocument>();
        var bodyStore = new NoOpBodyStore();

        var result = pipeline.PrepareDataModules(documents, "en", bodyStore);

        Assert.Empty(result.DataDocuments);
        Assert.Null(result.RouteMetadata);
    }

    [Fact]
    public void PrepareDataModules_AttachesRouteMetadataWithoutChangingExistingDataModels()
    {
        var pipeline = new VariantBuildPipeline();
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
        {
            ["sourceKey"] = "page_meta",
            ["sourceMode"] = "data",
            ["route"] = "/",
            ["title"] = "Home",
            ["summary"] = "Home summary"
        });
        var document = ContentDocument.Create("home", "Home", "home", DateTimeOffset.UtcNow, null, fields);
        var source = new ContentSourceConfig { Type = "notion", Name = "page_meta", Mode = "data" };
        var routeMetadata = new RouteMetadataConfig { Source = "page_meta", RequiredRoutes = ["/"] };

        var result = pipeline.PrepareDataModules(
            [document], "en", new NoOpBodyStore(), [source], routeMetadata);

        Assert.Equal("Home", result.RouteMetadata!["/"].Title);
        Assert.Null(result.DataIndex);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<ModuleInfo>>(result.SourceData!["page_meta"]);
        Assert.Single(rows);
        Assert.Equal("Home", rows[0].Title);
    }

    [Fact]
    public void BuildSiteModel_ConstructsFromConfigAndData()
    {
        var pipeline = new VariantBuildPipeline();
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "MySite",
                Title = "My Title",
                Description = "A test site description",
                Url = "https://example.com",
                Language = "zh",
                BaseUrl = "/"
            },
            Content = TestContent.Markdown(),
            Theme = new ThemeConfig
            {
                Params = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["showSidebar"] = true
                }
            }
        };

        Dictionary<string, IReadOnlyList<ModuleInfo>>? modules = null;
        Dictionary<string, object>? sourceData = null;

        var model = pipeline.BuildSiteModel(config, "/custom/", modules, sourceData);

        Assert.Equal("MySite", model.Name);
        Assert.Equal("My Title", model.Title);
        Assert.Equal("https://example.com", model.Url);
        Assert.Equal("/custom/", model.BaseUrl);
        Assert.Equal("zh", model.Language);
        Assert.Equal("A test site description", model.Description);
        Assert.NotNull(model.Params);
        Assert.True((bool)model.Params!["showSidebar"]);
    }

    [Fact]
    public void BuildStaticHtmlData_WithNullStaticTemplate_ReturnsNullTemplateAndNoRoutes()
    {
        var pipeline = new VariantBuildPipeline();

        var (routes, template) = pipeline.BuildStaticHtmlData(
            null, null, _ => { }, false);

        Assert.NotNull(routes);
        Assert.Empty(routes);
        Assert.Null(template);
    }

    [Fact]
    public void BuildStaticHtmlData_WithStaticDirButNoHtml_DoesNotWarn()
    {
        var pipeline = new VariantBuildPipeline();
        var staticDir = Path.Combine(_rootDir, "static");
        Directory.CreateDirectory(staticDir);
        File.WriteAllText(Path.Combine(staticDir, "style.css"), "body{}");
        var warnings = new List<string>();

        var (routes, template) = pipeline.BuildStaticHtmlData(
            staticDir, null, warnings.Add, false);

        Assert.Empty(routes);
        Assert.Null(template);
        Assert.Empty(warnings);
    }

    [Fact]
    public void BuildStaticHtmlData_WithHtmlAndNoStaticTemplate_Warns()
    {
        var pipeline = new VariantBuildPipeline();
        var staticDir = Path.Combine(_rootDir, "static");
        Directory.CreateDirectory(staticDir);
        File.WriteAllText(Path.Combine(staticDir, "legacy.html"), "<h1>Legacy</h1>");
        var warnings = new List<string>();

        var (routes, template) = pipeline.BuildStaticHtmlData(
            staticDir, null, warnings.Add, false);

        Assert.Empty(routes);
        Assert.Null(template);
        Assert.Single(warnings);
        Assert.Contains("Static HTML files", warnings[0]);
    }

    [Fact]
    public void BuildStaticHtmlData_WithCustomTemplate_ReturnsCorrectTemplate()
    {
        var pipeline = new VariantBuildPipeline();
        var staticDir = Path.Combine(_rootDir, "static");
        Directory.CreateDirectory(staticDir);

        var (routes, template) = pipeline.BuildStaticHtmlData(
            staticDir, "custom-static", _ => { }, false);

        Assert.NotNull(routes);
        Assert.Equal("custom-static", template);
    }

    [Fact]
    public void GetThemeRootForTokens_WithRegistry_ReturnsThemeRoot()
    {
        var pipeline = new VariantBuildPipeline();

        var (themeRoot, parentRoot) = pipeline.GetThemeRootForTokens(
            "/path/to/theme", true, null, false);

        Assert.Equal("/path/to/theme", themeRoot);
        Assert.Null(parentRoot);
    }

    [Fact]
    public void GetThemeRootForTokens_WithRegistryAndExtends_ReturnsBothRoots()
    {
        var pipeline = new VariantBuildPipeline();

        var (themeRoot, parentRoot) = pipeline.GetThemeRootForTokens(
            "/path/to/theme", true, "/path/to/parent", true);

        Assert.Equal("/path/to/theme", themeRoot);
        Assert.Equal("/path/to/parent", parentRoot);
    }

    [Fact]
    public void GetThemeRootForTokens_WithNullRegistry_ReturnsNulls()
    {
        var pipeline = new VariantBuildPipeline();

        var (themeRoot, parentRoot) = pipeline.GetThemeRootForTokens(
            "/path/to/theme", false, null, false);

        Assert.Null(themeRoot);
        Assert.Null(parentRoot);
    }

    private sealed class NoOpBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(string.Empty));
    }
}
