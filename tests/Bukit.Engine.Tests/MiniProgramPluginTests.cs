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

    // The base path is part of public URLs but not of the variant output directory.
    private string _basePath = "/";

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
    public async Task ModuleMatchingNoRowInAnyLanguage_Fails()
    {
        // Other languages' rows exist, but none of them is a banner.
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [Faq("f1")], defaultBanner: false, otherLanguageData: [Faq("f9")]));

        Assert.Contains("field 'module' 'banner' matches no record of source 'modules' in any site language", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RowsWithLocaleOutsideSiteLanguages_DoNotCountAsMatch()
    {
        // locale: zh is no site language, so no variant ever lists these rows.
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(
            Config(languages: ["en", "zh-CN"]), [], [], defaultBanner: false, otherLanguageData: [Banner("b9", "Typo", 1, locale: "zh")]));

        Assert.Contains("field 'module' 'banner' matches no record of source 'modules' in any site language", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledScopeRowWithAmbiguousAliases_DoesNotFailTheBuild()
    {
        // The index skips disabled rows before reading fields, so their aliases are never resolved there.
        var config = Config(scopeField: "scope_name");
        var empty = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal) };
        var row = SettingRow("s1", "app", enabled: false, scopeKey: "scopeName", extra: ("scope__name", "app"));

        await RunAsync(config, [], [row], empty);

        Assert.Empty(ReadDataset("settings")["values"]!.AsObject());
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

        await Assert.ThrowsAsync<BukitException>(() => new MiniProgramPlugin(Config(), BuildExecutionMode.Production).AfterBuildAsync(context));

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

    [Fact]
    public async Task SharedRootAssets_KeepRootBaseInOtherLanguages()
    {
        var config = Config(languages: ["en", "zh-CN"]);
        config = config with { Site = config.Site with { SharedAssets = new SharedAssetsConfig { Enabled = true } } };
        Directory.CreateDirectory(_outputDir);
        _basePath = "/zh-CN/";
        var shared = SharedAssetScope.TryCreate(config, ["en", "zh-CN"], _outputDir, logger: null)!;
        const string body = """<p><img src="/assets/images/hero.png"><img src="/assets/uploads/photo.png"><a href="/zh-CN/assets/files/a.pdf">pdf</a></p>""";

        await RunAsync(
            config,
            [Article("a1", "First", body: body)],
            [Banner("b1", "One", 1, image: "/assets/images/{id}.png"), Banner("b2", "Two", 2, image: "/zh-CN/assets/images/{id}.png")],
            baseUrl: "/zh-CN/",
            sharedAssets: shared);

        // The zh-CN variant omits shared files: their URLs keep the root base, as the website's do.
        // Localized media is copied into every variant, so it keeps the variant base.
        var banners = ReadDataset("banners")["items"]!.AsArray();
        Assert.Equal("https://example.com/assets/images/b1.png", (string)banners[0]!["image"]!);
        Assert.Equal("https://example.com/assets/images/b2.png", (string)banners[1]!["image"]!);
        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details[0]!["url"]!)["body"]!;
        Assert.Contains("src=\"https://example.com/assets/images/hero.png\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"https://example.com/zh-CN/assets/uploads/photo.png\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.com/assets/files/a.pdf\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnwrappedBlockContainers_KeepParagraphBoundaries()
    {
        const string body = """<div>Line one</div><div>Line two</div><figure><img src="/a.png"><figcaption>Caption</figcaption></figure>""";

        await RunAsync(Config(), [Article("a1", "First", body: body)], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details[0]!["url"]!)["body"]!;
        Assert.Equal("<p>Line one</p><p>Line two</p><p><img src=\"https://example.com/a.png\"></p><p>Caption</p>", html);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task NonFiniteNumber_FailsWithRecordAndField(double order)
    {
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [Banner("b1", "One", order)]));

        Assert.Contains("record '", ex.Message, StringComparison.Ordinal);
        Assert.Contains("field 'order' is not a finite number", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LargeIntegers_KeepEveryDigit()
    {
        await RunAsync(Config(), [], [Banner("b1", "One", 9007199254740993L), Banner("b2", "Two", "9007199254740995")]);

        var items = ReadDataset("banners")["items"]!.AsArray();
        Assert.Equal("9007199254740993", items[0]!["order"]!.ToJsonString());
        Assert.Equal("9007199254740995", items[1]!["order"]!.ToJsonString());
    }

    [Fact]
    public async Task MixedDateForms_SortChronologically()
    {
        var config = Config();
        var datasets = config.MiniProgram.Datasets.ToList();
        datasets[1] = datasets[1] with { Types = new Dictionary<string, string> { ["image"] = "image" } };
        config = config with { MiniProgram = config.MiniProgram with { Datasets = datasets } };

        await RunAsync(config, [],
        [
            Banner("b1", "Second", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            Banner("b2", "Third", new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)),
            Banner("b3", "First", "2026-01-01T10:00:00+08:00")
        ]);

        var titles = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["title"]!);
        Assert.Equal(["First", "Second", "Third"], titles);
    }

    [Fact]
    public async Task DataIndexBodyThatIsNotText_Fails()
    {
        var dataIndex = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["settings"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["app"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["body"] = new List<string> { "a", "b" } }
            }
        };

        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [], [], dataIndex));

        Assert.Contains("field 'body' is not an HTML string", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedRootAssets_CoverRelativeFullAndRootPrefixedUrls()
    {
        var config = Config(languages: ["en", "zh-CN"]);
        config = config with { Site = config.Site with { BaseUrl = "/repo/", SharedAssets = new SharedAssetsConfig { Enabled = true } } };
        Directory.CreateDirectory(_outputDir);
        _basePath = "/repo/zh-CN/";
        var shared = SharedAssetScope.TryCreate(config, ["en", "zh-CN"], _outputDir, logger: null)!;

        await RunAsync(
            config,
            [],
            [
                Banner("b1", "One", 1, image: "assets/images/{id}.png"),
                Banner("b2", "Two", 2, image: "https://example.com/repo/zh-CN/assets/images/{id}.png"),
                Banner("b3", "Three", 3, image: "/repo/assets/images/{id}.png"),
                Banner("b4", "Four", 4, image: "media/{id}.png")
            ],
            baseUrl: "/repo/zh-CN/",
            sharedAssets: shared,
            rootBaseUrl: "/repo/");

        var images = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["image"]!).ToArray();
        Assert.Equal(
            [
                "https://example.com/repo/assets/images/b1.png",
                "https://example.com/repo/assets/images/b2.png",
                "https://example.com/repo/assets/images/b3.png",
                "https://example.com/repo/zh-CN/media/b4.png"
            ],
            images);
    }

    [Theory]
    [InlineData("1e-30")]
    [InlineData("0.12345678901234567890123456789012")]
    public async Task NumberText_IsWrittenWithEveryDigit(string order)
    {
        await RunAsync(Config(), [], [Banner("b1", "One", order)]);

        Assert.Equal(order, ReadDataset("banners")["items"]![0]!["order"]!.ToJsonString());
    }

    [Fact]
    public async Task LargeIntegerSortKeys_CompareExactly()
    {
        await RunAsync(Config(), [], [Banner("b1", "Larger", 9007199254740993L), Banner("b2", "Smaller", 9007199254740992L)]);

        var titles = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["title"]!);
        Assert.Equal(["Smaller", "Larger"], titles);
    }

    [Theory]
    [InlineData("May")]
    [InlineData("3/4/2026")]
    public async Task NonIsoDateText_Fails(string value)
    {
        var config = Config();
        var datasets = config.MiniProgram.Datasets.ToList();
        datasets[1] = datasets[1] with { Types = new Dictionary<string, string> { ["image"] = "image", ["order"] = "date" } };
        config = config with { MiniProgram = config.MiniProgram with { Datasets = datasets } };

        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(config, [], [Banner("b1", "One", value)]));

        Assert.Contains("field 'order' is not a date", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DescendingSort_KeepsTextAfterNumbers()
    {
        var config = Config();
        var datasets = config.MiniProgram.Datasets.ToList();
        datasets[1] = datasets[1] with { Sort = new MiniProgramSortConfig("order", Descending: true), Types = new Dictionary<string, string> { ["image"] = "image" } };
        config = config with { MiniProgram = config.MiniProgram with { Datasets = datasets } };

        await RunAsync(config, [], [Banner("b1", "Low", 1), Banner("b2", "Placeholder", "TBD"), Banner("b3", "High", 3)]);

        var titles = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["title"]!);
        Assert.Equal(["High", "Low", "Placeholder"], titles);
    }

    [Fact]
    public async Task CellContainersAndCaptions_KeepTableLayout()
    {
        const string body = """<table><caption>Prices</caption><tr><td><div>A</div><div>B</div></td></tr></table><ul><li><div>Item</div></li></ul>""";

        await RunAsync(Config(), [Article("a1", "First", body: body)], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details[0]!["url"]!)["body"]!;
        Assert.StartsWith("<p>Prices</p><table>", html, StringComparison.Ordinal);
        Assert.Contains("<td>A<br>B</td>", html, StringComparison.Ordinal);
        Assert.Contains("<li>Item</li>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("caption", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CellContainers_AreSeparatedFromTextOnBothSides()
    {
        const string body = """<table><tr><td>Label<div>Value</div></td><td><div>Only</div><span></span><style>x{}</style></td></tr></table>""";

        await RunAsync(Config(), [Article("a1", "First", body: body)], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details[0]!["url"]!)["body"]!;
        Assert.Contains("<td>Label<br>Value</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>Only<span></span></td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MixedIntegerAndDoubleSortKeys_UseOneTotalOrder()
    {
        await RunAsync(Config(), [],
        [
            Banner("b1", "Larger", 9007199254740993L),
            Banner("b2", "DoubleEqual", 9007199254740992.0),
            Banner("b3", "LongEqual", 9007199254740992L),
            Banner("b4", "Huge", "1e30")
        ]);

        var titles = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["title"]!);
        Assert.Equal(["DoubleEqual", "LongEqual", "Larger", "Huge"], titles);
    }

    [Fact]
    public async Task RootBasePrefixedLinks_AreKeptAsWritten()
    {
        _basePath = "/repo/zh-CN/";
        var body = "<p><a href=\"/repo/about/\">about</a></p>";

        await RunAsync(
            Config(languages: ["en", "zh-CN"]),
            [Article("a1", "First", body: body)],
            [Banner("b1", "One", 1, image: "/repo/assets/media/{id}.png"), Banner("b2", "Two", 2, image: "/repo/zh-CN/assets/media/{id}.png")],
            baseUrl: "/repo/zh-CN/",
            rootBaseUrl: "/repo/");

        var images = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["image"]!).ToArray();
        Assert.Equal(["https://example.com/repo/assets/media/b1.png", "https://example.com/repo/zh-CN/assets/media/b2.png"], images);
        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        Assert.Contains("href=\"https://example.com/repo/about/\"", (string)ReadByUrl((string)details[0]!["url"]!)["body"]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedAssetCheck_MatchesNormalizedSiteUrlAndEscapedPaths()
    {
        var config = Config(siteUrl: "https://example.com:443", languages: ["en", "zh-CN"]);
        config = config with { Site = config.Site with { SharedAssets = new SharedAssetsConfig { Enabled = true, Paths = ["assets/", "静态/"] } } };
        Directory.CreateDirectory(_outputDir);
        _basePath = "/zh-CN/";
        var shared = SharedAssetScope.TryCreate(config, ["en", "zh-CN"], _outputDir, logger: null)!;

        await RunAsync(
            config,
            [],
            [Banner("b1", "One", 1, image: "assets/images/{id}.png"), Banner("b2", "Two", 2, image: "静态/{id}.png")],
            baseUrl: "/zh-CN/",
            sharedAssets: shared,
            rootBaseUrl: "/");

        var images = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["image"]!).ToArray();
        Assert.DoesNotContain(images, image => image.Contains("/zh-CN/", StringComparison.Ordinal));
        Assert.EndsWith("/assets/images/b1.png", images[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task DateOnlyText_StaysACalendarDate()
    {
        var config = Config();
        var datasets = config.MiniProgram.Datasets.ToList();
        datasets[1] = datasets[1] with { Types = new Dictionary<string, string> { ["image"] = "image", ["order"] = "date" } };
        config = config with { MiniProgram = config.MiniProgram with { Datasets = datasets } };

        await RunAsync(config, [], [Banner("b1", "One", "2026-01-01"), Banner("b2", "Two", "2026-01-02T10:00:00+08:00")]);

        var orders = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["order"]!).ToArray();
        Assert.Equal(["2026-01-01", "2026-01-02T10:00:00+08:00"], orders);
    }

    [Fact]
    public async Task MidnightDateValues_AreCalendarDatesInAnyOffset()
    {
        var config = Config();
        var datasets = config.MiniProgram.Datasets.ToList();
        datasets[1] = datasets[1] with { Types = new Dictionary<string, string> { ["image"] = "image", ["order"] = "date" } };
        config = config with { MiniProgram = config.MiniProgram with { Datasets = datasets } };

        // Notion parses a date-only value at the build machine's local midnight, Markdown at UTC midnight.
        await RunAsync(config, [],
        [
            Banner("b1", "Local", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(8))),
            Banner("b2", "Utc", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            Banner("b3", "Timed", new DateTimeOffset(2026, 1, 3, 10, 0, 0, TimeSpan.FromHours(8)))
        ]);

        var orders = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["order"]!).ToArray();
        Assert.Equal(["2026-01-01", "2026-01-02", "2026-01-03T10:00:00+08:00"], orders);
    }

    [Fact]
    public async Task CellContainers_AddNoBreakNextToBlocksOrInvisibleContent()
    {
        const string body = """<table><tr><td><p>Intro</p><div>More</div></td></tr></table><ul><li><div>Item</div><span><script>track()</script></span></li></ul>""";

        await RunAsync(Config(), [Article("a1", "First", body: body)], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details[0]!["url"]!)["body"]!;
        Assert.Contains("<td><p>Intro</p>More</td>", html, StringComparison.Ordinal);
        Assert.Contains("<li>Item<span></span></li>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SitePathsWithSpaces_AreEscapedLikeRelativeOnes()
    {
        await RunAsync(Config(), [], [Banner("b1", "One", 1, image: "/assets/images/new banner.png"), Banner("b2", "Two", 2, image: "assets/images/new banner.png")]);

        var images = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["image"]!).ToArray();
        Assert.Equal(["https://example.com/assets/images/new%20banner.png", "https://example.com/assets/images/new%20banner.png"], images);
    }

    [Fact]
    public async Task NaNSortKey_SortsLastInDescendingOrder()
    {
        var config = Config();
        var datasets = config.MiniProgram.Datasets.ToList();
        datasets[1] = datasets[1] with
        {
            Fields = ["title", "image"],
            Sort = new MiniProgramSortConfig("order", Descending: true),
            Types = new Dictionary<string, string> { ["image"] = "image" }
        };
        config = config with { MiniProgram = config.MiniProgram with { Datasets = datasets } };

        await RunAsync(config, [], [Banner("b1", "Invalid", double.NaN), Banner("b2", "Low", 1), Banner("b3", "High", 3)]);

        var titles = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["title"]!);
        Assert.Equal(["High", "Low", "Invalid"], titles);
    }

    [Theory]
    [InlineData("""<p><a href="https://prod-files-secure.s3.us-west-2.amazonaws.com/a.pdf?X-Amz-Signature=abc">PDF</a></p>""")]
    [InlineData("""<video src="https://prod-files-secure.s3.us-west-2.amazonaws.com/v.mp4?X-Amz-Signature=abc"></video>""")]
    public async Task SignedBodyLinksAndEmbeds_Fail(string body)
    {
        var ex = await Assert.ThrowsAsync<BukitException>(() => RunAsync(Config(), [Article("a1", "First", body: body)], []));

        Assert.Equal(DiagnosticCode.SchemaValidationFailed, ex.Code);
        Assert.Contains("field 'body' signed URL was not localized", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("abc", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogHash_ChangesWhenOnlyFileUrlsChange()
    {
        var config = Config();
        await RunAsync(config, [Article("a1", "First")], []);
        var before = ReadCatalog();

        await RunAsync(config with { MiniProgram = config.MiniProgram with { OutputDir = "data" } }, [Article("a1", "First")], []);
        var after = JsonNode.Parse(File.ReadAllText(Path.Combine(_outputDir, "data", "catalog.json")))!;

        Assert.Equal(Hash(before, "articles"), Hash(after, "articles"));
        Assert.NotEqual((string)before["catalogHash"]!, (string)after["catalogHash"]!);
    }

    [Fact]
    public async Task InlineWrappersAndMixedContainers_WriteNoNestedOrMergedParagraphs()
    {
        const string body = """
            <div>Read more <a href="/x/"><div>Card</div></a> after</div><div><span>A</span><div>B<ul><li>x</li></ul></div></div><table><tr><td><div>C<ul><li>y</li></ul>D</div></td></tr></table>
            """;

        await RunAsync(Config(), [Article("a1", "First", body: body)], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        var html = (string)ReadByUrl((string)details[0]!["url"]!)["body"]!;
        Assert.StartsWith(
            "<p>Read more</p><a href=\"https://example.com/x/\"><p>Card</p></a><p>after</p><p><span>A</span></p><p>B</p><ul><li>x</li></ul>",
            html,
            StringComparison.Ordinal);
        Assert.Contains("<td>C<ul><li>y</li></ul>D</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PageUrlField_IsEscaped()
    {
        await RunAsync(Config(), [Article("a1", "文章")], []);

        var details = ReadByUrl((string)ReadCatalog()["datasets"]!["articles"]!["detailCatalog"]!["url"]!)["items"]!.AsArray();
        Assert.Equal("https://example.com/%E6%96%87%E7%AB%A0/", (string)ReadByUrl((string)details[0]!["url"]!)["url"]!);
    }

    [Fact]
    public async Task TinySortKeys_OrderByValue()
    {
        await RunAsync(Config(), [], [Banner("b1", "Larger", 3e-30), Banner("b2", "Smaller", 1e-30), Banner("b3", "Zero", 0.0)]);

        var titles = ReadDataset("banners")["items"]!.AsArray().Select(item => (string)item!["title"]!);
        Assert.Equal(["Zero", "Smaller", "Larger"], titles);
    }

    [Fact]
    public async Task FloatNumbers_AreWrittenAsTheirShortestText()
    {
        await RunAsync(Config(), [], [Banner("b1", "One", 0.1f)]);

        Assert.Equal("0.1", ReadDataset("banners")["items"]![0]!["order"]!.ToJsonString());
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
        IReadOnlyList<ContentDocument>? otherLanguageData = null,
        SharedAssetScope? sharedAssets = null,
        string? rootBaseUrl = null)
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
        if (sharedAssets is not null)
        {
            context.Set(BuildDataKeys.SharedAssets, new SharedAssetBuildData(sharedAssets, []));
        }

        context.Set(BuildDataKeys.RootBaseUrl, rootBaseUrl ?? baseUrl);

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

    private static ContentDocument Banner(string id, string title, object order, bool enabled = true, string? image = "/assets/media/{id}.png", string? url = null, string? locale = null)
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
        if (locale is not null) fields["locale"] = locale;
        return ContentDocument.Create($"modules:{id}", title, id, Day, null, ContentFieldReader.ToFieldMap(fields));
    }

    private static ContentDocument Faq(string id)
        => ContentDocument.Create($"modules:{id}", "Question", id, Day, null, ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "modules",
            ["sourceMode"] = "data",
            ["type"] = "faq"
        }));

    private static ContentDocument SettingRow(string id, string scope, bool enabled, string scopeKey = "scope", (string Key, string Value)? extra = null)
    {
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceKey"] = "settings",
            ["sourceMode"] = "data",
            [scopeKey] = scope,
            ["key"] = "title",
            ["value"] = "Shop",
            ["value_type"] = "text",
            ["enabled"] = enabled
        };
        if (extra is { } pair) fields[pair.Key] = pair.Value;
        return ContentDocument.Create($"settings:{id}", id, id, Day, null, ContentFieldReader.ToFieldMap(fields));
    }

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
        var relative = new Uri(url).AbsolutePath[_basePath.Length..];
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
