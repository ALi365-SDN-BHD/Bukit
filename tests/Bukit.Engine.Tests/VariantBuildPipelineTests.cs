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
    public async Task PrepareDataModules_EmptyItems_ReturnsEmptyResult()
    {
        var documents = new List<ContentDocument>();
        var bodyStore = new NoOpBodyStore();

        var result = await VariantDataSitePlanner.PrepareDataModulesAsync(documents, "en", bodyStore);

        Assert.Empty(result.DataDocuments);
        Assert.Null(result.RouteMetadata);
    }

    [Fact]
    public async Task PrepareDataModules_AttachesRouteMetadataWithoutExposingReservedRowsAsModules()
    {
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

        var result = await VariantDataSitePlanner.PrepareDataModulesAsync(
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

        var model = VariantDataSitePlanner.BuildSiteModel(config, "/custom/", modules, sourceData);

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
        var config = CreateMinimalConfig() with
        {
            Site = CreateMinimalConfig().Site with { Timezone = "Asia/Kuala_Lumpur" }
        };
        var instant = new DateTimeOffset(2025, 12, 31, 16, 30, 0, TimeSpan.Zero);

        var model = VariantDataSitePlanner.BuildSiteModel(config, "/", null, null, buildStartedAt: instant);

        Assert.Equal(2026, model.BuildYear);
    }

    [Fact]
    public void BuildSiteModel_WhitespaceTimezoneUsesUtc()
    {
        var config = CreateMinimalConfig() with
        {
            Site = CreateMinimalConfig().Site with { Timezone = " " }
        };
        var instant = new DateTimeOffset(2025, 12, 31, 23, 30, 0, TimeSpan.Zero);

        var model = VariantDataSitePlanner.BuildSiteModel(config, "/", null, null, buildStartedAt: instant);

        Assert.Equal(2025, model.BuildYear);
    }

    [Fact]
    public void BuildSiteModel_ReservesRouteMetadataSourceFromTemplateDataBindings()
    {
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

        var model = VariantDataSitePlanner.BuildSiteModel(config, "/", modules, sourceData, dataIndex: dataIndex);

        Assert.False(model.Modules!.ContainsKey("page_meta"));
        Assert.False(model.Data!.ContainsKey("page_meta"));
        Assert.False(model.DataIndex!.ContainsKey("page_meta"));
        Assert.True(model.Modules.ContainsKey("settings"));
        Assert.True(model.Data.ContainsKey("settings"));
        Assert.True(model.DataIndex.ContainsKey("settings"));
    }

    [Theory]
    [InlineData(false, false, null, false, 0)]
    [InlineData(true, false, null, false, 0)]
    [InlineData(true, true, null, true, 0)]
    [InlineData(true, true, "custom-static", false, 1)]
    public async Task VariantRouteStage_StaticHtmlUsesProductionRoutesAndWarnings(
        bool hasDirectory, bool hasHtml, string? template, bool warns, int routeCount)
    {
        var staticDir = Path.Combine(_rootDir, "static");
        if (hasDirectory)
        {
            Directory.CreateDirectory(staticDir);
            File.WriteAllText(Path.Combine(staticDir, hasHtml ? "legacy.html" : "style.css"), "content");
        }
        var config = CreateMinimalConfig();
        config = config with { Theme = config.Theme with { StaticTemplate = template } };
        var context = new BuildVariantContext(
            config, _rootDir, new ConfigOverrides(), [], CanonicalContentGraph.Empty,
            new NoOpBodyStore(), Path.Combine(_rootDir, "dist"), "/",
            Path.Combine(_rootDir, "layouts"), Path.Combine(_rootDir, "assets"), staticDir,
            Path.Combine(_rootDir, "media"), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            null, null, "en", DateTimeOffset.UnixEpoch);
        var logger = new WarningLogger();
        var result = await VariantRouteStage.ExecuteAsync(
            context, [], new ThemeTemplateResolver(null), logger,
            new BuildStageMetricsCollector(), CancellationToken.None);

        Assert.Equal(routeCount, result.StaticHtmlRoutes.Count);
        Assert.Equal(result.StaticHtmlRoutes, result.PluginContext.StaticHtmlRoutes);
        if (routeCount > 0)
        {
            var route = Assert.Single(result.StaticHtmlRoutes);
            Assert.Equal(template, route.Template);
            Assert.Equal("legacy/index.html", route.OutputPath);
            Assert.Equal(route, Assert.Single(result.StaticEntries!).Route);
        }
        else
        {
            Assert.Null(result.StaticEntries);
        }
        if (warns)
            Assert.Contains("Static HTML files", Assert.Single(logger.Warnings));
        else
            Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void GetThemeRootForTokens_WithRegistry_ReturnsThemeRoot()
    {

        var (themeRoot, parentRoot) = VariantRendererThemePlanner.GetThemeRootForTokens(
            "/path/to/theme", true, null, false);

        Assert.Equal("/path/to/theme", themeRoot);
        Assert.Null(parentRoot);
    }

    [Fact]
    public void GetThemeRootForTokens_WithRegistryAndExtends_ReturnsBothRoots()
    {

        var (themeRoot, parentRoot) = VariantRendererThemePlanner.GetThemeRootForTokens(
            "/path/to/theme", true, "/path/to/parent", true);

        Assert.Equal("/path/to/theme", themeRoot);
        Assert.Equal("/path/to/parent", parentRoot);
    }

    [Fact]
    public void GetThemeRootForTokens_WithNullRegistry_ReturnsNulls()
    {

        var (themeRoot, parentRoot) = VariantRendererThemePlanner.GetThemeRootForTokens(
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
        var pipeline = VariantAnalyticsTransformStage.CreateHtmlTransformPipeline(
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

    private sealed class WarningLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) => throw new InvalidOperationException(message);
    }

    private sealed class NoOpBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(string.Empty));
    }
}
