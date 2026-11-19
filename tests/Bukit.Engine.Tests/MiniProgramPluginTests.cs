using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.BuiltIn.MiniProgram;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MiniProgramPluginTests : IDisposable
{
    private static readonly DateTimeOffset Day = new(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(8));
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), $"bukit-mp-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Disabled_WritesNothing()
    {
        await RunAsync(Config(enabled: false), [Article("a1", "First")], []);

        Assert.False(Directory.Exists(Path.Combine(_outputDir, "mp")));
    }

    [Theory]
    [InlineData(null, DiagnosticCode.ConfigRequiredFieldMissing)]
    [InlineData("http://localhost:4000", DiagnosticCode.ConfigInvalidValue)]
    public async Task RequiresHttpsSiteUrl(string? siteUrl, DiagnosticCode code)
    {
        var ex = await Assert.ThrowsAsync<ConfigException>(() => RunAsync(Config(siteUrl: siteUrl), [], []));

        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public async Task DevelopmentBuildWithoutSiteUrl_SkipsOutput()
    {
        var logger = new RecordingLogger();

        await RunAsync(Config(siteUrl: null), [Article("a1", "First")], [], mode: BuildExecutionMode.Development, logger: logger);

        Assert.False(Directory.Exists(Path.Combine(_outputDir, "mp")));
        Assert.Contains(logger.Infos, message => message.Contains("event=miniprogram.skipped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DevelopmentBuildWithInvalidSiteUrl_StillFails()
    {
        var ex = await Assert.ThrowsAsync<ConfigException>(() => RunAsync(Config(siteUrl: "http://localhost:4000"), [], [], mode: BuildExecutionMode.Development));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
    }

    [Fact]
    public async Task Collection_WritesListDetailsAndCatalog()
    {
        var context = await RunAsync(Config(), [Article("a1", "First", internalNote: "secret"), Article("a2", "Second", day: 2)], []);

        var catalog = ReadCatalog();
        Assert.Equal("1.0", (string)catalog["schemaVersion"]!);
        Assert.Equal("zh-CN", (string)catalog["language"]!);
        Assert.StartsWith("sha256:", (string)catalog["catalogHash"]!);
        var articles = catalog["datasets"]!["articles"]!;
        Assert.Equal("collection", (string)articles["kind"]!);
        Assert.StartsWith("https://example.com/mp/articles.list.", (string)articles["url"]!);

        var list = ReadByUrl((string)articles["url"]!)["items"]!.AsArray();
        Assert.Equal(["second", "first"], list.Select(item => (string)item!["id"]!));
        Assert.Equal("Second", (string)list[0]!["title"]!);
        Assert.Null(list[0]!["internalNote"]);
        Assert.Null(list[0]!["body"]);

        var details = ReadByUrl((string)articles["detailCatalog"]!["url"]!)["items"]!.AsArray();
        Assert.Equal(["first", "second"], details.Select(item => (string)item!["id"]!));
        var first = ReadByUrl((string)details[0]!["url"]!);
        Assert.Equal("First", (string)first["title"]!);
        Assert.Equal("https://example.com/first/", (string)first["url"]!);
        Assert.Contains("<p>First body</p>", (string)first["body"]!, StringComparison.Ordinal);

        var outputs = context.Get(BuildDataKeys.PluginOutputs);
        Assert.Contains(outputs, output => output.Path == "mp/catalog.json" && output.Plugin == "miniprogram");
        Assert.Equal(Directory.GetFiles(_outputDir, "*", SearchOption.AllDirectories).Length, outputs.Count);
    }

    [Fact]
    public async Task Files_AreDeterministicLfWithoutBom()
    {
        await RunAsync(Config(), [Article("a1", "First"), Article("a2", "Second")], [Banner("b1", "One", 1), Banner("b2", "Two", 2)]);
        var first = Snapshot();
        Directory.Delete(_outputDir, recursive: true);
        await RunAsync(Config(), [Article("a2", "Second"), Article("a1", "First")], [Banner("b2", "Two", 2), Banner("b1", "One", 1)]);

        Assert.Equal(first, Snapshot());
        foreach (var bytes in first.Values)
        {
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.Equal((byte)'\n', bytes[^1]);
            Assert.NotEqual((byte)'\n', bytes[^2]);
        }
    }

    [Fact]
    public async Task ChangingOneBanner_ChangesOnlyThatDataset()
    {
        await RunAsync(Config(), [Article("a1", "First")], [Banner("b1", "One", 1)]);
        var before = ReadCatalog();
        Directory.Delete(_outputDir, recursive: true);
        await RunAsync(Config(), [Article("a1", "First")], [Banner("b1", "Uno", 1)]);
        var after = ReadCatalog();

        Assert.NotEqual(Hash(before, "banners"), Hash(after, "banners"));
        Assert.NotEqual((string)before["catalogHash"]!, (string)after["catalogHash"]!);
        Assert.Equal(Hash(before, "articles"), Hash(after, "articles"));
        Assert.Equal(Hash(before, "site"), Hash(after, "site"));
    }

    [Fact]
    public async Task ChangingArticleBody_ChangesDetailButNotList()
    {
        await RunAsync(Config(), [Article("a1", "First")], []);
        var before = ReadCatalog();
        Directory.Delete(_outputDir, recursive: true);
        await RunAsync(Config(), [Article("a1", "First", body: "<p>Edited</p>")], []);
        var after = ReadCatalog();

        Assert.Equal(Hash(before, "articles"), Hash(after, "articles"));
        Assert.NotEqual((string)before["datasets"]!["articles"]!["detailCatalog"]!["hash"]!, (string)after["datasets"]!["articles"]!["detailCatalog"]!["hash"]!);
        Assert.NotEqual((string)before["catalogHash"]!, (string)after["catalogHash"]!);
    }

    [Fact]
    public async Task Module_SortsByDeclaredFieldAndSkipsDisabledRecords()
    {
        await RunAsync(Config(), [], [Banner("b3", "Third", 3), Banner("b1", "First", 1), Banner("b2", "Hidden", 2, enabled: false), Faq("f1")]);

        var items = ReadDataset("banners")["items"]!.AsArray();
        Assert.Equal(["First", "Third"], items.Select(item => (string)item!["title"]!));
        Assert.Equal("https://example.com/assets/media/b1.png", (string)items[0]!["image"]!);
        Assert.Equal(1, items[0]!["order"]!.GetValue<double>());
        Assert.DoesNotContain("b1", (string)items[0]!["id"]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedImageUrl_FailsWithRecordAndField()
    {
        var banner = Banner("b1", "One", 1, image: "https://prod-files-secure.s3.us-west-2.amazonaws.com/x.png?X-Amz-Signature=abc");

        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [banner]));

        Assert.Equal(DiagnosticCode.SchemaValidationFailed, ex.Code);
        Assert.Contains("dataset 'banners'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("field 'image'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongType_FailsWithRecordAndField()
    {
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [Banner("b1", "One", "abc")]));

        Assert.Equal(DiagnosticCode.SchemaValidationFailed, ex.Code);
        Assert.Contains("field 'order' is not a number", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingRequiredField_Fails()
    {
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [Banner("b1", "One", 1, image: null)]));

        Assert.Contains("field 'image' is required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Body_IsReducedToSafeSubset()
    {
        const string body = """
            <div class="notion-callout"><p onclick="x()">Hi <a href="/second/#top">next</a></p></div>
            <script>alert(1)</script><iframe src="https://video.example/v1"></iframe>
            <details><summary>More</summary><p>Hidden</p></details>
            <img src="/assets/media/a.png" alt="A" class="lazy" loading="lazy"><a href="javascript:alert(1)">bad</a>
            """;
        await RunAsync(Config(), [Article("a1", "First", body: body), Article("a2", "Second")], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details.First(item => (string)item!["id"]! == "first")!["url"]!)["body"]!;
        Assert.Contains("<p>Hi <a href=\"https://example.com/second/#top\" data-bukit-id=\"second\">next</a></p>", html, StringComparison.Ordinal);
        Assert.Contains("<p><a href=\"https://video.example/v1\">https://video.example/v1</a></p>", html, StringComparison.Ordinal);
        Assert.Contains("<p><strong>More</strong></p><p>Hidden</p>", html, StringComparison.Ordinal);
        Assert.Contains("<img src=\"https://example.com/assets/media/a.png\" alt=\"A\">", html, StringComparison.Ordinal);
        Assert.Contains("<a>bad</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("script", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("iframe", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_KeepsOrderNestingAndAbsoluteUrls()
    {
        await RunAsync(Config(), [], []);

        var items = ReadDataset("nav")["items"]!.AsArray();
        Assert.Equal(["Home", "Docs"], items.Select(item => (string)item!["title"]!));
        Assert.Equal("https://example.com/", (string)items[0]!["url"]!);
        var child = items[1]!["children"]!.AsArray().Single()!;
        Assert.Equal("Guide", (string)child["title"]!);
        Assert.Equal("https://example.com/docs/guide/", (string)child["url"]!);
    }

    [Fact]
    public async Task DataIndexAndTaxonomy_AreWritten()
    {
        var pages = new[]
        {
            Article("a1", "First", tags: ["x", "y"]),
            Article("a2", "Second", tags: ["x"]),
            Article("a3", "Third", tags: ["X"]),
            Article("p1", "About", tags: ["x"], source: "pages")
        };
        await RunAsync(Config(), pages, [], Settings(("title", "Shop"), ("phone", "123")));

        var values = ReadDataset("settings")["values"]!.AsObject();
        Assert.Equal(["phone", "title"], values.Select(pair => pair.Key));
        var tags = ReadDataset("tags")["items"]!.AsArray();
        // Only collection pages count, and case variants are one term.
        Assert.Equal([("X", 3), ("y", 1)], tags.Select(item => ((string)item!["name"]!, (int)item["count"]!)));
    }

    [Fact]
    public async Task UnknownModuleOrScope_Fails()
    {
        var module = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [Faq("f1")], defaultBanner: false));
        Assert.Contains("field 'module' 'banner' matches no record of source 'modules'", module.Message, StringComparison.Ordinal);

        var empty = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal) };
        var scope = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [SettingRow("s1", "shop", enabled: true)], empty));
        Assert.Contains("field 'scope' 'app' matches no record of source 'settings'", scope.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllScopeRowsDisabled_WritesEmptyValues()
    {
        // The data index drops disabled rows, so the scope is absent from it but still matched by its rows.
        var empty = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal) };

        await RunAsync(Config(), [], [SettingRow("s1", "app", enabled: false)], empty);

        Assert.Empty(ReadDataset("settings")["values"]!.AsObject());
    }

    [Fact]
    public async Task VariantWithoutRowsOfItsLanguage_WarnsAndWritesEmpty()
    {
        // Locale-specific data rows exist only in their own language variant; another language has them.
        var logger = new RecordingLogger();
        var empty = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal) };

        await RunAsync(Config(), [], [], empty, defaultBanner: false, logger: logger, otherLanguageData: [Banner("b9", "Other", 1), SettingRow("s9", "app", enabled: true)]);

        Assert.Empty(ReadDataset("banners")["items"]!.AsArray());
        Assert.Empty(ReadDataset("settings")["values"]!.AsObject());
        Assert.Equal(2, logger.Warnings.Count(message => message.Contains("event=miniprogram.dataset_empty", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ModuleMatchingNoRowInAnyLanguage_FailsOnMultilingualSite()
    {
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(languages: ["en", "zh-CN"]), [], [Faq("f1")], defaultBanner: false, otherLanguageData: [Faq("f9")]));

        Assert.Contains("field 'module' 'banner' matches no record of source 'modules'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledScopeRows_MatchThroughFieldAlias()
    {
        // DataModuleBuilder reads scope_name as scopeName too; the empty-scope check must agree with it.
        var config = Config(scopeField: "scope_name");
        var empty = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal) };

        await RunAsync(config, [], [SettingRow("s1", "app", enabled: false, scopeKey: "scopeName")], empty);

        Assert.Empty(ReadDataset("settings")["values"]!.AsObject());
    }

    [Fact]
    public async Task DataRecordUrlField_IsWritten()
    {
        var config = Config();
        var datasets = config.MiniProgram.Datasets.ToList();
        datasets[1] = datasets[1] with { Fields = ["title", "image", "order", "url"], Required = ["title", "url"], Types = new Dictionary<string, string> { ["url"] = "url", ["order"] = "number" } };
        config = config with { MiniProgram = config.MiniProgram with { Datasets = datasets } };

        await RunAsync(config, [], [Banner("b1", "One", 1, url: "/sale/")]);

        Assert.Equal("https://example.com/sale/", (string)ReadDataset("banners")["items"]![0]!["url"]!);
    }

    [Fact]
    public async Task Menu_IsOrderedByWeight()
    {
        var config = Config();
        config = config with
        {
            Site = config.Site with
            {
                Menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["main"] =
                    [
                        new MenuConfig { Identifier = "docs", Name = "Docs", Url = "/docs/", Weight = 2 },
                        new MenuConfig { Identifier = "home", Name = "Home", Url = "/", Weight = 1 }
                    ]
                }
            }
        };

        await RunAsync(config, [], []);

        Assert.Equal(["Home", "Docs"], ReadDataset("nav")["items"]!.AsArray().Select(item => (string)item!["title"]!));
    }

    [Fact]
    public async Task FailedBuild_TracksFilesAlreadyWritten()
    {
        var context = NewContext([Article("a1", "First")], [Banner("b1", "One", 1, image: "https://cdn.example/x.png?X-Amz-Signature=s")], Settings(("title", "Shop")), null, "/", new RecordingLogger());

        await Assert.ThrowsAsync<BukitException>(() => new MiniProgramPlugin(Config()).AfterBuildAsync(context));

        // The article files were written before the banner failed; the manifest must still own them.
        var tracked = context.Get(BuildDataKeys.PluginOutputs)!.Select(output => output.Path).ToList();
        Assert.NotEmpty(tracked);
        var written = Directory.GetFiles(Path.Combine(_outputDir, "mp"), "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_outputDir, path).Replace('\\', '/'));
        Assert.Equal(written.Order(StringComparer.Ordinal), tracked.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DataIndexBody_IsSanitized()
    {
        var config = Config();
        var datasets = config.MiniProgram.Datasets.ToList();
        datasets[2] = datasets[2] with { Types = null };
        config = config with { MiniProgram = config.MiniProgram with { Datasets = datasets } };

        await RunAsync(config, [], [], Settings(("body", "<p onclick=\"x()\">Hi</p><script>alert(1)</script>")));

        Assert.Equal("<p>Hi</p>", (string)ReadDataset("settings")["values"]!["body"]!);
    }

    [Fact]
    public async Task VersionStyleInternalHref_GetsBukitId()
    {
        await RunAsync(Config(), [Article("a1", "First", body: "<p><a href=\"/release-v1.2\">r</a></p>"), Article("a2", "Release-v1.2")], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details.First(item => (string)item!["id"]! == "first")!["url"]!)["body"]!;
        Assert.Contains("data-bukit-id=\"release-v1.2\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllRowsDisabled_WritesEmptyModule()
    {
        await RunAsync(Config(), [], [Banner("b1", "Hidden", 1, enabled: false)]);

        Assert.Empty(ReadDataset("banners")["items"]!.AsArray());
    }

    [Fact]
    public async Task RelativeDataUrl_ResolvesUnderBasePath()
    {
        await RunAsync(Config(), [], [Banner("b1", "One", 1, image: "media/{id}.png")], baseUrl: "/repo/");

        // The base path is part of the public URL, not of the output directory.
        var file = Directory.GetFiles(Path.Combine(_outputDir, "mp"), "banners.*.json").Single();
        var banners = JsonNode.Parse(File.ReadAllText(file))!;
        Assert.Equal("https://example.com/repo/media/b1.png", (string)banners["items"]![0]!["image"]!);
    }

    [Theory]
    [InlineData("https://storage.googleapis.com/b/x.png?X-Goog-Algorithm=GOOG4&X-Goog-Signature=abc")]
    [InlineData("https://d1.cloudfront.net/x.png?Policy=p&Signature=s&Key-Pair-Id=k")]
    [InlineData("https://file.notion.so/f/x.png?table=block&expirationTimestamp=1&signature=s")]
    public async Task OtherSignedUrlSchemes_Fail(string url)
    {
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [Banner("b1", "One", 1, image: url)]));

        Assert.Contains("field 'image' signed URL was not localized", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Signature=", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EquivalentInternalHrefs_GetBukitId()
    {
        const string body = "<p><a href=\"/second\">a</a><a href=\"/second/index.html\">b</a><a href=\"https://EXAMPLE.com/second/?x=1\">c</a></p>";
        await RunAsync(Config(), [Article("a1", "First", body: body), Article("a2", "Second")], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details.First(item => (string)item!["id"]! == "first")!["url"]!)["body"]!;
        Assert.Equal(3, html.Split("data-bukit-id=\"second\"").Length - 1);
    }

    [Fact]
    public async Task LinkToPageOutsideCollections_HasNoBukitId()
    {
        await RunAsync(Config(), [Article("a1", "First", body: "<p><a href=\"/about/\">about</a></p>"), Article("p1", "About", source: "pages")], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        Assert.Equal(["first"], details.Select(item => (string)item!["id"]!));
        var html = (string)ReadByUrl((string)details[0]!["url"]!)["body"]!;
        Assert.Contains("<a href=\"https://example.com/about/\">about</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StringTypeOnList_Fails()
    {
        var config = Config(articleListFields: ["title", "tags"], articleTypes: new Dictionary<string, string> { ["tags"] = "string" });

        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(config, [Article("a1", "First", tags: ["x", "y"])], []));

        Assert.Equal(DiagnosticCode.SchemaValidationFailed, ex.Code);
        Assert.Contains("field 'tags' is not a string", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UntypedMapField_Fails()
    {
        var config = Config(articleListFields: ["title", "extra"]);
        var map = new Dictionary<string, object> { ["lat"] = 1.5 };

        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(config, [Article("a1", "First", extra: map)], []));

        Assert.Contains("field 'extra' has a value that is not a scalar", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyOptionalList_IsWrittenAsMissing()
    {
        await RunAsync(Config(articleListFields: ["title", "extra"]), [Article("a1", "First", extra: new List<object>())], []);

        var item = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["url"]!)["items"]![0]!;
        Assert.Null(item["extra"]);
    }

    [Fact]
    public async Task NonIndexablePages_AreExcluded()
    {
        await RunAsync(Config(), [Article("a1", "First"), Article("a2", "Second")], [], nonIndexable: "/second/");

        var list = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["url"]!)["items"]!.AsArray();
        Assert.Equal(["first"], list.Select(item => (string)item!["id"]!));
    }

    private async Task<BuildContext> RunAsync(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> pages,
        IReadOnlyList<ContentDocument> data,
        IReadOnlyDictionary<string, object>? dataIndex = null,
        string? nonIndexable = null,
        string baseUrl = "/",
        bool defaultBanner = true,
        bool defaultSettings = true,
        BuildExecutionMode mode = BuildExecutionMode.Production,
        ILogger? logger = null,
        IReadOnlyList<ContentDocument>? otherLanguageData = null)
    {
        // The default config declares a banner module and a settings scope; an unmatched one fails the build.
        if (defaultBanner && !data.Any(document => ContentFieldReader.GetContentType(document) == "banner"))
        {
            data = [.. data, Banner("b0", "Default", 0, enabled: false)];
        }

        if (defaultSettings)
        {
            dataIndex ??= Settings(("title", "Shop"));
        }

        var context = NewContext(pages, data, dataIndex, nonIndexable, baseUrl, logger ?? new ConsoleLogger(LogLevel.Error), otherLanguageData);
        await new MiniProgramPlugin(config, mode).AfterBuildAsync(context);
        return context;
    }

    private BuildContext NewContext(
        IReadOnlyList<RoutedContentDocument> pages,
        IReadOnlyList<ContentDocument> data,
        IReadOnlyDictionary<string, object>? dataIndex,
        string? nonIndexable,
        string baseUrl,
        ILogger logger,
        IReadOnlyList<ContentDocument>? otherLanguageData = null)
    {
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        if (nonIndexable is not null)
        {
            var page = pages.Single(p => p.Route.Url == nonIndexable);
            seoIndex[page.Route.OutputPath] = new SeoIndexEntry(page.Route, "https://example.com" + page.Route.Url, "noindex", Indexable: false, Day, null, null);
        }

        var context = new BuildContext
        {
            RootDir = _outputDir,
            OutputDir = _outputDir,
            BaseUrl = baseUrl,
            LayoutsDir = _outputDir,
            RoutedDocuments = pages,
            SeoIndex = seoIndex,
            Logger = logger,
            // The content graph holds the documents of every language; the variant sees only its own.
            ContentGraph = new CanonicalContentGraph([], [], [], [.. data, .. otherLanguageData ?? []])
        };
        context.Set(BuildDataKeys.DataModules, new DataModuleResult(data, null, null, dataIndex, null));
        return context;
    }

    private static AppConfig Config(
        bool enabled = true,
        string? siteUrl = "https://example.com",
        string[]? articleListFields = null,
        Dictionary<string, string>? articleTypes = null,
        IReadOnlyList<string>? languages = null,
        string scopeField = "scope") => new()
    {
        Site = new SiteConfig
        {
            Name = "shop",
            Title = "Shop",
            Url = siteUrl,
            Languages = languages,
            Menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(StringComparer.OrdinalIgnoreCase)
            {
                ["main"] =
                [
                    new MenuConfig { Identifier = "home", Name = "Home", Url = "/" },
                    new MenuConfig
                    {
                        Identifier = "docs",
                        Name = "Docs",
                        Url = "/docs/",
                        Children = [new MenuConfig { Identifier = "guide", Name = "Guide", Url = "/docs/guide/" }]
                    }
                ]
            }
        },
        Content = ContentConfigFactory.FromSources(
        [
            TestContent.MarkdownSource(),
            new ContentSourceConfig { Type = "markdown", Name = "settings", Mode = "data", Markdown = new MarkdownConfig { Dir = "data/settings" }, DataIndex = new DataIndexConfig { ScopeField = scopeField } }
        ]),
        MiniProgram = new MiniProgramConfig
        {
            Enabled = enabled,
            Datasets =
            [
                new MiniProgramDatasetConfig
                {
                    Name = "articles",
                    Kind = "collection",
                    Source = "blog",
                    ListFields = articleListFields ?? ["title", "summary"],
                    DetailFields = ["title", "url", "body", "publishedAt"],
                    Types = articleTypes
                },
                new MiniProgramDatasetConfig
                {
                    Name = "banners",
                    Kind = "module",
                    Source = "modules",
                    Module = "banner",
                    Fields = ["title", "image", "order"],
                    Sort = new MiniProgramSortConfig("order", Descending: false),
                    Required = ["title", "image"],
                    Types = new Dictionary<string, string> { ["image"] = "image", ["order"] = "number" }
                },
                new MiniProgramDatasetConfig { Name = "settings", Kind = "dataIndex", Source = "settings", Scope = "app" },
                new MiniProgramDatasetConfig { Name = "tags", Kind = "taxonomy", Taxonomy = "tags" },
                new MiniProgramDatasetConfig { Name = "nav", Kind = "menu", Menu = "main" }
            ]
        }
    };

    private static RoutedContentDocument Article(
        string id,
        string title,
        int day = 1,
        string? body = null,
        string? internalNote = null,
        string[]? tags = null,
        string source = "blog",
        object? extra = null)
    {
        var slug = title.ToLowerInvariant();
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = source,
            ["sourceMode"] = "content",
            ["summary"] = title + " summary"
        };
        if (internalNote is not null) fields["internalNote"] = internalNote;
        if (tags is not null) fields["tags"] = tags;
        if (extra is not null) fields["extra"] = extra;
        var document = ContentDocument.Create($"blog:{id}", title, slug, Day.AddDays(day), body ?? $"<p>{title} body</p>", ContentFieldReader.ToFieldMap(fields));
        return new RoutedContentDocument(document, new RouteInfo($"/{slug}/", $"{slug}/index.html", "pages/post.html"));
    }

    private static ContentDocument Banner(string id, string title, object order, bool enabled = true, string? image = "/assets/media/{id}.png", string? url = null)
    {
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "modules",
            ["sourceMode"] = "data",
            ["type"] = "banner",
            ["order"] = order,
            ["enabled"] = enabled
        };
        if (image is not null) fields["image"] = image.Replace("{id}", id, StringComparison.Ordinal);
        if (url is not null) fields["url"] = url;
        return ContentDocument.Create($"modules:{id}", title, id, Day, null, ContentFieldReader.ToFieldMap(fields));
    }

    private static ContentDocument Faq(string id)
        => ContentDocument.Create($"modules:{id}", "Question", id, Day, null, ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "modules",
            ["sourceMode"] = "data",
            ["type"] = "faq"
        }));

    private static ContentDocument SettingRow(string id, string scope, bool enabled, string scopeKey = "scope")
        => ContentDocument.Create($"settings:{id}", id, id, Day, null, ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "settings",
            ["sourceMode"] = "data",
            [scopeKey] = scope,
            ["key"] = "title",
            ["value"] = "Shop",
            ["value_type"] = "text",
            ["enabled"] = enabled
        }));

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = [];
        public List<string> Warnings { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) => Warnings.Add(message);
    }

    private JsonNode ReadCatalog() => JsonNode.Parse(File.ReadAllText(Path.Combine(_outputDir, "mp", "catalog.json")))!;

    private JsonNode ReadDataset(string name) => ReadByUrl((string)ReadCatalog()["datasets"]![name]!["url"]!);

    private JsonNode ReadByUrl(string url)
    {
        var relative = new Uri(url).AbsolutePath.TrimStart('/');
        return JsonNode.Parse(File.ReadAllText(Path.Combine(_outputDir, relative)))!;
    }

    private static Dictionary<string, object> Settings(params (string Key, string Value)[] values)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["app"] = values.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal)
            }
        };

    private static string Hash(JsonNode catalog, string dataset) => (string)catalog["datasets"]![dataset]!["hash"]!;

    private SortedDictionary<string, byte[]> Snapshot()
        => new(Directory.GetFiles(_outputDir, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(_outputDir, path).Replace('\\', '/'), File.ReadAllBytes), StringComparer.Ordinal);
}
