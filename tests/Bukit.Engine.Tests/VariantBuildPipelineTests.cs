using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine;
using Bukit.Engine.Incremental;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
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
    public async Task PrepareDataModules_EmptyItems_ReturnsEmptyResult()
    {
        var pipeline = new VariantBuildPipeline();
        var documents = new List<ContentDocument>();
        var bodyStore = new NoOpBodyStore();

        var result = await pipeline.PrepareDataModulesAsync(documents, "en", bodyStore);

        Assert.Empty(result.DataDocuments);
        Assert.Null(result.RouteMetadata);
    }

    [Fact]
    public async Task PrepareDataModules_AttachesRouteMetadataWithoutExposingReservedRowsAsModules()
    {
        var pipeline = new VariantBuildPipeline();
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
        {
            ["sourceKey"] = "page_meta",
            ["sourceMode"] = "data",
            ["type"] = "route_metadata_record",
            ["route"] = "/",
            ["title"] = "Home",
            ["summary"] = "Home summary"
        });
        var document = ContentDocument.Create("home", "Home", "home", DateTimeOffset.UtcNow, null, fields);
        var source = new ContentSourceConfig { Type = "notion", Name = "page_meta", Mode = "data" };
        var routeMetadata = new RouteMetadataConfig { Source = "page_meta", RequiredRoutes = ["/"] };

        var result = await pipeline.PrepareDataModulesAsync(
            [document], "en", new NoOpBodyStore(), [source], routeMetadata);

        Assert.Equal("Home", result.RouteMetadata!["/"].Title);
        Assert.Null(result.Modules);
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
        Assert.Null(model.GetType().GetProperty("Analytics"));
    }

    [Fact]
    public void BuildSiteModel_DerivesBuildYearFromConfiguredTimezone()
    {
        var pipeline = new VariantBuildPipeline();
        var config = CreateMinimalConfig() with
        {
            Site = CreateMinimalConfig().Site with { Timezone = "Asia/Kuala_Lumpur" }
        };
        var instant = new DateTimeOffset(2025, 12, 31, 16, 30, 0, TimeSpan.Zero);

        var model = pipeline.BuildSiteModel(config, "/", null, null, buildStartedAt: instant);

        Assert.Equal(2026, model.BuildYear);
    }

    [Fact]
    public void BuildSiteModel_WhitespaceTimezoneUsesUtc()
    {
        var pipeline = new VariantBuildPipeline();
        var config = CreateMinimalConfig() with
        {
            Site = CreateMinimalConfig().Site with { Timezone = " " }
        };
        var instant = new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.Zero);

        var model = pipeline.BuildSiteModel(config, "/", null, null, buildStartedAt: instant);

        Assert.Equal(2025, model.BuildYear);
    }

    [Fact]
    public void BuildSiteModel_ReservesRouteMetadataSourceFromTemplateDataBindings()
    {
        var pipeline = new VariantBuildPipeline();
        var config = CreateMinimalConfig() with
        {
            Content = CreateMinimalConfig().Content with
            {
                RouteMetadata = new RouteMetadataConfig { Source = "page_meta" }
            }
        };
        var routeRows = new[] { new ModuleInfo { Id = "home", Title = "Home", Slug = "home", Content = string.Empty } };
        var settingsRows = new[] { new ModuleInfo { Id = "email", Title = "Email", Slug = "email", Content = string.Empty } };
        var modules = new Dictionary<string, IReadOnlyList<ModuleInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["page_meta"] = routeRows,
            ["settings"] = settingsRows
        };
        var sourceData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["page_meta"] = routeRows,
            ["settings"] = settingsRows
        };
        var dataIndex = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["page_meta"] = new Dictionary<string, object> { ["routes"] = "reserved" },
            ["settings"] = new Dictionary<string, object> { ["contact"] = "public" }
        };

        var model = pipeline.BuildSiteModel(config, "/", modules, sourceData, dataIndex: dataIndex);

        Assert.False(model.Modules!.ContainsKey("page_meta"));
        Assert.False(model.Data!.ContainsKey("page_meta"));
        Assert.False(model.DataIndex!.ContainsKey("page_meta"));
        Assert.True(model.Modules.ContainsKey("settings"));
        Assert.True(model.Data.ContainsKey("settings"));
        Assert.True(model.DataIndex.ContainsKey("settings"));
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

    [Theory]
    [InlineData(false, "inject")]
    [InlineData(true, "inject")]
    [InlineData(true, "theme")]
    [InlineData(true, "off")]
    public void CreateHtmlTransformPipeline_AnalyticsDoesNotDependOnSeoMode(
        bool seoEnabled,
        string renderMode)
    {
        var config = CreateMinimalConfig() with
        {
            Site = CreateMinimalConfig().Site with
            {
                Seo = new SeoConfig { Enabled = seoEnabled, RenderMode = renderMode, Diagnostics = "off" },
                Analytics = new AnalyticsConfig
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "google-analytics",
                            MeasurementId = "G-TEST"
                        }
                    ]
                }
            }
        };
        var buildContext = CreateBuildContext(config);
        var pluginTransforms = PluginRunner.CollectHtmlTransforms(
            buildContext,
            BuildExecutionMode.Production,
            PluginExecutionPolicy.From(config.Site),
            [new AnalyticsPlugin(config)]);
        var seoResult = new SeoPipeline().Execute(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            buildContext.Logger);
        var pipeline = VariantBuildPipeline.CreateHtmlTransformPipeline(
            seoResult,
            pluginTransforms,
            BuildExecutionMode.Production);

        var html = pipeline.Transform(
            new HtmlTransformContext(
                "/", "index.html", HtmlDocumentKind.Content,
                BuildExecutionMode.Production, buildContext.Logger,
                new PageInfo
                {
                    Title = "Home",
                    Url = "/",
                    Content = string.Empty,
                    Seo = new SeoModel { Title = "SEO Home", Canonical = "https://example.com/" }
                }),
            "<html><head><meta name='theme'></head><body></body></html>");

        Assert.True(HtmlHeadScanner.TryFindHead(html, out var head));
        var analytics = html.IndexOf("<!-- bukit:analytics:google-analytics:G-TEST:head:start", StringComparison.Ordinal);
        var theme = html.IndexOf("<meta name='theme'>", StringComparison.Ordinal);
        Assert.Equal(head.ContentStart, analytics);
        Assert.True(analytics < theme);
        if (seoEnabled && renderMode == "inject")
        {
            var canonical = html.IndexOf("rel=\"canonical\"", StringComparison.Ordinal);
            Assert.True(theme < canonical);
        }
    }

    [Fact]
    public async Task ExecuteRenderWithHtmlTransformRecordingAsync_StrictFailureStillRecordsExecution()
    {
        var config = CreateMinimalConfig() with
        {
            Site = CreateMinimalConfig().Site with { PluginFailMode = "strict" }
        };
        var context = CreateBuildContext(config);
        var transforms = PluginRunner.CollectHtmlTransforms(
            context,
            BuildExecutionMode.Production,
            PluginExecutionPolicy.From(config.Site),
            [new ThrowingHtmlTransformPlugin()]);
        var htmlContext = new HtmlTransformContext(
            "/", "index.html", HtmlDocumentKind.Content,
            BuildExecutionMode.Production, context.Logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VariantBuildPipeline.ExecuteRenderWithHtmlTransformRecordingAsync(
                transforms,
                () => Task.FromResult(transforms[0].Transform(htmlContext, "html"))));

        var execution = Assert.Single(context.PluginExecutions);
        Assert.Equal("html-transform", execution.Hook);
        Assert.False(execution.Success);
        Assert.Equal("strict transform failure", execution.Error);
    }

    [Fact]
    public async Task RenderAssetPlan_PassesRenderOwnershipToAssetPreflightBeforeWrites()
    {
        var staticDir = Path.Combine(_rootDir, "static");
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(staticDir);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(staticDir, "index.html"), "static");
        var document = ContentDocument.Create(
            "home", "Home", "home", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField>());
        var routedDocument = new RoutedContentDocument(
            document,
            new RouteInfo("/", "index.html", "detail"));
        var routeResult = new RoutePipelineResult([document], [routedDocument], [])
        {
            ListRouteGraph = ListRouteGraph.Empty
        };
        var config = CreateMinimalConfig();
        var context = new BuildVariantContext(
            Config: config,
            RootDir: _rootDir,
            Overrides: new ConfigOverrides { Incremental = false },
            Documents: [document],
            ContentGraph: CanonicalContentGraph.Empty,
            BodyStore: new NoOpBodyStore(),
            OutputDir: outputDir,
            BaseUrl: "/",
            LayoutsDir: Path.Combine(_rootDir, "layouts"),
            AssetsDir: Path.Combine(_rootDir, "assets"),
            StaticDir: staticDir,
            MediaDownloadDir: Path.Combine(_rootDir, "media"),
            SeoAlternates: new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            RootBaseUrl: null,
            ManifestSuffix: null,
            DefaultLanguage: "en",
            BuildStartedAt: DateTimeOffset.UtcNow);
        var manifestSetup = new ManifestSetupResult(
            new BuildManifest(), string.Empty, Path.Combine(_rootDir, "manifest.json"), null, false);

        var plan = VariantRenderAssetPlanner.Create(
            context,
            routeResult,
            derivedDocuments: [],
            staticEntries: null,
            new SiteModel { Name = "test", Title = "Test", BaseUrl = "/", Language = "en" },
            manifestSetup,
            themeRootForTokens: null,
            parentThemeRootForTokens: null,
            new ConsoleLogger(LogLevel.Error));

        var exception = await Assert.ThrowsAsync<BukitException>(() =>
            AssetPipeline.PrepareAsync(plan.AssetPipelineContext));

        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
        Assert.Contains("index.html", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outputDir));
    }

    private BuildContext CreateBuildContext(AppConfig config)
        => new()
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            LayoutsDir = Path.Combine(_rootDir, "layouts"),
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

    private sealed class ThrowingHtmlTransformPlugin :
        IBukitPlugin,
        IHookFilterPlugin,
        IHtmlTransformPlugin
    {
        public string Name => "throwing";
        public string Version => "1.0.0";
        public bool SupportsHook(string hook) => hook == HtmlTransformHooks.HtmlTransform;
        public IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context)
            => new ThrowingHtmlTransform();
    }

    private sealed class ThrowingHtmlTransform : IHtmlTransform
    {
        public string Name => "throwing";
        public string Transform(HtmlTransformContext context, string html)
            => throw new InvalidOperationException("strict transform failure");
    }

    private sealed class NoOpBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(string.Empty));
    }
}
