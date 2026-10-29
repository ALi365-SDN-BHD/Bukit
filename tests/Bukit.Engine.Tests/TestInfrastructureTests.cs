using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Engine.Tests.Infrastructure;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TestInfrastructureTests
{
    [Fact, Trait("Category", "Integration")]
    public void Sandbox_IsUnique_AndTeardownPreservesExternalFileAndDirectoryLinkTargets()
    {
        using var target = new SiteSandbox();
        var targetFile = target.Write("target/data.bin", "external");
        var sandbox = new SiteSandbox();
        Assert.NotEqual(target.Root, sandbox.Root);
        var fileLink = sandbox.Link("file-link", targetFile, directory: false);
        var directoryLink = sandbox.Link("directory-link", Path.GetDirectoryName(targetFile)!, directory: true);
        Assert.Equal("external", File.ReadAllText(fileLink));
        Assert.Equal("external", File.ReadAllText(Path.Combine(directoryLink, "data.bin")));
        sandbox.Dispose();
        Assert.False(Directory.Exists(sandbox.Root));
        Assert.Equal("external", File.ReadAllText(targetFile));
    }

    [Fact, Trait("Category", "Unit")]
    public void PublicSnapshot_OnlyExcludesExplicitMetadataRoots_AndDetectsEveryRetainedByteAndPathMutation()
    {
        using var sandbox = new SiteSandbox();
        string[] excluded = [".bukit/report.json", ".bukit-output-marker", ".bukit-build-state.json", "en/.bukit/report.json",
            "en/.bukit-output-marker", "zh-CN/.bukit-build-state.json", "zh-CN/.bukit/report.json"];
        string[] retained = ["assets/.bukit/public.bin", "en/content/.bukit/public.bin", "fr/.bukit/report.json", "docs/.bukit-output-marker",
            "en/nested/.bukit-build-state.json", ".bukit-extra/file", ".env", ".nojekyll", "_headers", "agent-manifest.json", "search.index.json"];
        foreach (var path in excluded.Concat(retained)) sandbox.Write(path, "before");
        byte[] binary = [0, 255, 254, 128, 13, 10];
        File.WriteAllBytes(sandbox.PathFor(retained[0]), binary);
        var before = OutputSnapshot.Public(sandbox.Root, "en", "zh-CN");
        Assert.Equal(retained.Order(StringComparer.Ordinal), before.Entries.Keys);
        foreach (var path in excluded) sandbox.Write(path, "metadata can change");
        before.AssertEqual(OutputSnapshot.Public(sandbox.Root, "en", "zh-CN"));
        foreach (var path in retained)
        {
            var bytes = File.ReadAllBytes(sandbox.PathFor(path));
            File.WriteAllBytes(sandbox.PathFor(path), [1, 0, 255]);
            Assert.NotNull(Record.Exception(() => before.AssertEqual(OutputSnapshot.Public(sandbox.Root, "en", "zh-CN"))));
            File.WriteAllBytes(sandbox.PathFor(path), bytes);
        }
        sandbox.Write("new", "added");
        Assert.NotNull(Record.Exception(() => before.AssertEqual(OutputSnapshot.Public(sandbox.Root, "en", "zh-CN"))));
        File.Delete(sandbox.PathFor("new"));
        File.Delete(sandbox.PathFor(".env"));
        Assert.NotNull(Record.Exception(() => before.AssertEqual(OutputSnapshot.Public(sandbox.Root, "en", "zh-CN"))));
        using var regularFile = new SiteSandbox();
        regularFile.Write(".bukit", "not a metadata directory");
        regularFile.Write(".bukit-output-marker/public.bin", "not a metadata file");
        regularFile.Write("en/.bukit-build-state.json/public.bin", "not a metadata file");
        Assert.Equal(new[] { ".bukit", ".bukit-output-marker/public.bin", "en/.bukit-build-state.json/public.bin" },
            OutputSnapshot.Public(regularFile.Root, "en").Entries.Keys);
    }

    [Fact, Trait("Category", "Integration")]
    public void Snapshots_DoNotFollowLinks_AndManagedSnapshotsRetainEveryExplicitResourceAndIdentity()
    {
        using var sandbox = new SiteSandbox();
        using var external = new SiteSandbox();
        var outside = external.Write("target/value", "outside");
        string[] sentinels = ["dist/.bukit/report.json", "dist/.bukit-output-marker", "dist/.bukit-build-state.json",
            ".cache/build-manifest.json", ".cache/media/value", ".cache/notion/value", "custom-media/value", "custom-notion/value", "custom-index.json"];
        foreach (var path in sentinels) sandbox.Write(path, "keep");
        var roots = new[] { ("output", sandbox.PathFor("dist")), ("cache", sandbox.PathFor(".cache")),
            ("media", sandbox.PathFor("custom-media")), ("notion", sandbox.PathFor("custom-notion")), ("index", sandbox.PathFor("custom-index.json")),
            ("missing", sandbox.PathFor("missing")) };
        var link = sandbox.Link("dist/link", outside, directory: false);
        sandbox.Link("dist/dir-link", Path.GetDirectoryName(outside)!, directory: true);
        Assert.Throws<IOException>(() => OutputSnapshot.Public(sandbox.PathFor("dist")));
        var before = OutputSnapshot.Managed(roots);
        Assert.Equal("file-link", before.Entries["output/link"].Kind);
        Assert.Equal("directory-link", before.Entries["output/dir-link"].Kind);
        Assert.DoesNotContain(before.Entries.Keys, key => key.Contains("/dir-link/", StringComparison.Ordinal));
        File.WriteAllText(outside, "target changed without changing link");
        before.AssertEqual(OutputSnapshot.Managed(roots));
        foreach (var path in sentinels)
        {
            File.WriteAllBytes(sandbox.PathFor(path), [0, 255, 3]);
            Assert.NotNull(Record.Exception(() => before.AssertEqual(OutputSnapshot.Managed(roots))));
            sandbox.Write(path, "keep");
        }
        Directory.CreateDirectory(sandbox.PathFor("missing"));
        Assert.NotNull(Record.Exception(() => before.AssertEqual(OutputSnapshot.Managed(roots))));
        Directory.Delete(sandbox.PathFor("missing"));
        File.Delete(sandbox.PathFor("custom-index.json"));
        Directory.CreateDirectory(sandbox.PathFor("custom-index.json"));
        Assert.NotNull(Record.Exception(() => before.AssertEqual(OutputSnapshot.Managed(roots))));
        Directory.Delete(sandbox.PathFor("custom-index.json"));
        sandbox.Write("custom-index.json", "keep");
        File.Delete(link);
        sandbox.Link("dist/link", external.PathFor("dangling"), directory: false);
        Assert.NotNull(Record.Exception(() => before.AssertEqual(OutputSnapshot.Managed(roots))));
    }

    [Theory, Trait("Category", "Contract")]
    [InlineData("{\"type\":\"integer\",\"minimum\":0,\"maximum\":9223372036854775807}", "9223372036854775807", "9223372036854775808")]
    [InlineData("{\"type\":\"number\",\"maximum\":1.7976931348623157e308}", "1.7976931348623157e308", "1e309")]
    [InlineData("{\"type\":\"integer\"}", "1.0", "1.5")]
    [InlineData("{\"type\":\"integer\"}", "1e4000", "1e-1000")]
    [InlineData("{\"type\":\"integer\"}", "100000000000000000000000000000000", "100000000000000000000000000000000.1")]
    [InlineData("{\"type\":\"number\",\"minimum\":0}", "1e-1000", "-1e-1000")]
    [InlineData("{\"type\":\"number\",\"maximum\":1e4000}", "1e4000", "2e4000")]
    [InlineData("{\"uniqueItems\":true}", "[1e4000,2e4000]", "[1e4000,10e3999]")]
    [InlineData("{\"type\":[\"integer\",\"null\"],\"minimum\":0}", "null", "-1")]
    [InlineData("{\"const\":{\"a\":1,\"b\":2}}", "{\"b\":2,\"a\":1.0}", "{\"a\":1,\"b\":3}")]
    [InlineData("{\"enum\":[\"a\",true]}", "true", "false")]
    [InlineData("{\"type\":\"string\",\"minLength\":2,\"pattern\":\"^a\"}", "\"abc\"", "\"xbc\"")]
    [InlineData("{\"type\":\"string\",\"minLength\":2}", "\"ab\"", "\"😀\"")]
    [InlineData("{\"type\":\"string\",\"format\":\"date\"}", "\"2024-02-29\"", "\"2023-02-29\"")]
    [InlineData("{\"type\":\"string\",\"format\":\"date-time\"}", "\"2024-06-01T00:00:00.123Z\"", "\"2024-06-01T00:00:00\"")]
    [InlineData("{\"type\":\"string\",\"format\":\"uri\"}", "\"https://example.test/a\"", "\"/relative\"")]
    [InlineData("{\"type\":\"array\",\"minItems\":1,\"maxItems\":2,\"items\":{\"type\":\"integer\"}}", "[1,2]", "[1,2,3]")]
    [InlineData("{\"type\":\"array\",\"minItems\":1}", "[1]", "[]")]
    [InlineData("{\"type\":\"array\",\"items\":{\"type\":\"integer\"}}", "[1]", "[\"1\"]")]
    [InlineData("{\"uniqueItems\":true}", "[{\"a\":1},{\"a\":2}]", "[{\"a\":1,\"b\":2},{\"b\":2,\"a\":1.0}]")]
    [InlineData("{\"type\":\"object\",\"required\":[\"a\"],\"properties\":{\"a\":{\"type\":\"integer\"}},\"additionalProperties\":false}", "{\"a\":1}", "{\"a\":1,\"extra\":2}")]
    [InlineData("{\"required\":[\"a\"],\"properties\":{\"a\":{\"required\":[\"b\"]}}}", "{\"a\":{\"b\":1}}", "{\"a\":{}}")]
    [InlineData("{\"additionalProperties\":{\"type\":\"integer\",\"minimum\":0}}", "{\"a\":1}", "{\"a\":-1}")]
    [InlineData("{\"$defs\":{\"a/b~c\":{\"type\":\"integer\"}},\"$ref\":\"#/$defs/a~1b~0c\"}", "1", "\"1\"")]
    [InlineData("{\"oneOf\":[{\"type\":\"number\",\"minimum\":0,\"maximum\":1},{\"type\":\"null\"}]}", "0.5", "2")]
    [InlineData("{\"oneOf\":[{\"type\":\"number\"},{\"type\":\"integer\"}]}", "0.5", "1")]
    public void LocalSchema_EnforcesActiveConstraints(string schemaText, string valid, string invalid)
    {
        using var schema = JsonDocument.Parse(schemaText);
        using var good = JsonDocument.Parse(valid);
        using var bad = JsonDocument.Parse(invalid);
        LocalJsonSchema.AssertValid(good.RootElement, schema.RootElement);
        Assert.Throws<InvalidDataException>(() => LocalJsonSchema.AssertValid(bad.RootElement, schema.RootElement));
    }

    [Theory, Trait("Category", "Contract")]
    [InlineData("{\"properties\":{\"unused\":{\"maxLength\":2}}}", "Unsupported schema keyword")]
    [InlineData("{\"$ref\":\"#/$defs/missing\"}", "Unresolved reference")]
    [InlineData("{\"$ref\":\"https://example.test/schema\"}", "Unsupported reference")]
    [InlineData("{\"title\":\"not a schema\",\"properties\":{\"unused\":{\"$ref\":\"#/title\"}}}", "Reference is not a schema")]
    [InlineData("{\"properties\":{\"unused\":{\"format\":\"email\"}}}", "Unsupported schema format")]
    [InlineData("{\"type\":\"imaginary\"}", "Unsupported schema type")]
    public void LocalSchema_FailsClosedEvenForUnusedSchemaBranches(string text, string expected)
    {
        using var schema = JsonDocument.Parse(text);
        using var value = JsonDocument.Parse("{}");
        Assert.Contains(expected, Assert.Throws<InvalidDataException>(() => LocalJsonSchema.AssertValid(value.RootElement, schema.RootElement)).Message);
    }

    [Theory, Trait("Category", "Contract")]
    [InlineData("seo")]
    [InlineData("publish")]
    [InlineData("semantic")]
    public void ReportLookups_MatchWindowsAuditPathsToSlashSnapshotKeys(string lookup)
    {
        var summary = new
        {
            routeCount = 1, documentCount = 1, indexableCount = 1, nonIndexableCount = 0, errorCount = 0, warningCount = 0,
            publishIssueCount = 0, machineReadabilityIssueCount = 0, trustIssueCount = 0, representationGapCount = 0,
            geoEnhancedCount = 0, geoScore = 0, llmsTxtGenerated = false, llmsFullTxtGenerated = false
        };
        var seo = JsonSerializer.SerializeToElement(new
        {
            summary, issues = Array.Empty<object>(), routes = new[] { new
            {
                url = "/en/", outputPath = lookup is "seo" or "semantic" ? "en\\index.html" : "en/index.html",
                indexable = true, canonical = "https://example.test/en/", title = "Home", description = "", robots = "", author = ""
            } }
        });
        var publish = JsonSerializer.SerializeToElement(new
        {
            summary, issues = Array.Empty<object>(), documents = new[] { new
            {
                routeUrl = "/en/", outputPath = lookup == "publish" ? "en\\index.html" : "en/index.html",
                indexable = true, representations = Array.Empty<object>()
            } }
        });
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["en/index.html"] = "<main>Home</main>"u8.ToArray(),
            [".bukit/seo-report.json"] = JsonSerializer.SerializeToUtf8Bytes(seo),
            [".bukit/publish-audit-report.json"] = JsonSerializer.SerializeToUtf8Bytes(publish),
            [".bukit/seo-route-map.json"] = """{"routes":[{"route":"/en/"}]}"""u8.ToArray(),
            [".bukit/geo-report.json"] = """{"geoEnhancedRoutes":[],"geoEnhancedCount":0,"geoScore":0,"llmsTxtGenerated":false,"llmsFullTxtGenerated":false}"""u8.ToArray()
        };
        var hash = ReportAssertions.Hash("""{"body":"","title":"Home","description":"","canonical":"https://example.test/en/","robots":"","author":{"name":"","type":"","url":"","sameAs":[]},"jsonLd":[]}"""u8.ToArray());
        var snapshot = JsonSerializer.SerializeToElement(new
        {
            siteUrl = "https://example.test/", routes = new[] { new { url = "https://example.test/en/", indexable = true, semanticHash = hash } }
        });
        void Check()
        {
            if (lookup == "semantic") ReportAssertions.PublishSnapshot(snapshot, seo, publish, files, "https://example.test/");
            else ReportAssertions.AuditBundle(files, "");
        }
        Check();
        if (lookup == "semantic")
        {
            files["en/index.html"] = """<script type="application/ld+json">{}</script>"""u8.ToArray();
            Assert.NotNull(Record.Exception(Check));
        }
        files.Remove("en/index.html");
        Assert.NotNull(Record.Exception(Check));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task GeneratedReport_DefaultFullExpectation_RejectsActualIncrementalBuild()
    {
        using var site = new SiteBuilder();
        site.Markdown("alpha", "Alpha", "Body");
        var started = DateTimeOffset.UtcNow;
        var result = await site.BuildAsync(incremental: true);
        var ended = DateTimeOffset.UtcNow;
        Assert.True(result.Incremental.Enabled);

        var error = Assert.ThrowsAny<Exception>(() => ReportAssertions.Generated(site, result, started, ended));
        Assert.Contains("build/cache/routes semantics", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task GeneratedReport_FullBuild_RejectsMatchingFalseCacheCounts()
    {
        using var site = new SiteBuilder();
        site.Markdown("alpha", "Alpha", "Body");
        site.Markdown("beta", "Beta", "Body");
        var started = DateTimeOffset.UtcNow;
        var result = await site.BuildAsync();
        var ended = DateTimeOffset.UtcNow;
        var variant = Assert.Single(result.Variants);
        Assert.True(variant.RenderedCount > 0);
        var changedVariant = variant with { RenderedCount = variant.RenderedCount - 1, SkippedCount = variant.SkippedCount + 1 };
        var changedResult = result with
        {
            Incremental = result.Incremental with { CacheHitCount = 1, CacheMissCount = result.Incremental.CacheMissCount - 1 },
            Variants = [changedVariant]
        };
        var buildPath = Path.Combine(site.Output, ".bukit/build-report.json");
        var manifestPath = Path.Combine(site.Output, ".bukit/incremental-manifest.json");
        var build = JsonNode.Parse(File.ReadAllText(buildPath))!;
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        foreach (var counters in new[] { build["incremental"]!, manifest })
        {
            counters["cacheHitCount"] = changedResult.Incremental.CacheHitCount;
            counters["cacheMissCount"] = changedResult.Incremental.CacheMissCount;
        }
        manifest["variants"]![0]!["renderedCount"] = changedVariant.RenderedCount;
        manifest["variants"]![0]!["skippedCount"] = changedVariant.SkippedCount;
        File.WriteAllText(buildPath, build.ToJsonString());
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var error = Assert.ThrowsAny<Exception>(() => ReportAssertions.Generated(site, changedResult, started, ended));
        Assert.Contains("build/cache/routes semantics", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Category", "Integration")]
    [InlineData(null)]
    [InlineData("basic-markdown-site")]
    [InlineData("i18n-site")]
    [InlineData("taxonomy-site")]
    public async Task RealSites_BuildTwice_WithExactPublicBytesAndIndependentReports(string? fixture)
    {
        using var site = new SiteBuilder(fixture);
        OutputSnapshot? first = null;
        var errors = new List<Exception>();
        for (var pass = 0; pass < 2; pass++)
        {
            var start = DateTimeOffset.UtcNow;
            var result = await site.BuildAsync();
            var end = DateTimeOffset.UtcNow;
            AssertScenario(site, fixture, result);
            var snapshot = OutputSnapshot.Public(site.Output, site.Config.Site.Languages?.ToArray() ?? []);
            Assert.NotEmpty(snapshot.Entries);
            if (first is null) first = snapshot;
            else first.AssertEqual(snapshot);
            if (fixture == "basic-markdown-site" && pass == 0) AssertReportMutations(site);
            var error = Record.Exception(() => ReportAssertions.Generated(site, result, start, end));
            if (error is not null) errors.Add(error);
        }
        Assert.True(errors.Count == 0, string.Join("\n\n", errors.Select(error => error.ToString())));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task FailedRealBuild_PreservesRawOutputAndCacheBytes()
    {
        using var site = new SiteBuilder();
        site.Markdown("fixed", "Fixed", "fixed body");
        await site.BuildAsync();
        var obstruction = Path.Combine(site.Output, ".bukit/build-report.json");
        File.Delete(obstruction);
        Directory.CreateDirectory(obstruction);
        File.WriteAllBytes(Path.Combine(obstruction, "sentinel"), [0, 255, 128]);
        var before = OutputSnapshot.Managed(("output", site.Output), ("cache", site.Cache));
        await Assert.ThrowsAnyAsync<Exception>(() => site.BuildAsync(clean: false));
        before.AssertEqual(OutputSnapshot.Managed(("output", site.Output), ("cache", site.Cache)));
    }

    private static void AssertScenario(SiteBuilder site, string? fixture, BuildResult result)
    {
        string Text(string path) => File.ReadAllText(Path.Combine(site.Output, path));
        switch (fixture)
        {
            case null:
                Assert.Contains("Phase 0", Text("index.html"));
                Assert.Equal(0, result.Summary.RouteCount);
                Assert.Single(result.Variants);
                break;
            case "basic-markdown-site":
                Assert.Contains("Basic Test", Text("index.html"));
                Assert.Contains("Hello Bukit", Text("index/index.html"));
                Assert.Contains("This is a basic test site.", Text("index/index.html"));
                Assert.Equal("en", Assert.Single(result.Variants).Language);
                Assert.True(File.Exists(Path.Combine(site.Output, "content/index/index.html.json")));
                Assert.True(File.Exists(Path.Combine(site.Output, "content/index/index.html.md")));
                break;
            case "i18n-site":
                Assert.Equal(new[] { "en", "zh-CN" }, result.Variants.Select(variant => variant.Language).Order(StringComparer.Ordinal));
                var english = Text("en/index/index.html");
                var chinese = Text("zh-CN/index/index.html");
                Assert.Contains("I18N Test", Text("en/index.html"));
                Assert.Contains("I18N Test", Text("zh-CN/index.html"));
                Assert.Contains("English Page", english);
                Assert.Contains("This is the English version of the page.", english);
                Assert.DoesNotContain("这是中文页面", english);
                Assert.Contains("lang=\"en\"", english);
                Assert.Contains("Chinese Page", chinese);
                Assert.Contains("这是中文页面。使用中文进行测试。", chinese);
                Assert.DoesNotContain("This is the English version", chinese);
                Assert.Contains("lang=\"zh-CN\"", chinese);
                using (var search = JsonDocument.Parse(Text("search.json")))
                {
                    var rows = search.RootElement.EnumerateArray().ToArray();
                    Assert.Equal(new[] { "/en/", "/en/index/", "/zh-CN/", "/zh-CN/index/" }, rows.Select(row => row.GetProperty("url").GetString()).Order(StringComparer.Ordinal));
                    var en = Assert.Single(rows, row => row.GetProperty("url").GetString() == "/en/index/");
                    var zh = Assert.Single(rows, row => row.GetProperty("url").GetString() == "/zh-CN/index/");
                    Assert.Equal("English Page", en.GetProperty("title").GetString());
                    Assert.Contains("This is the English version", en.GetProperty("content").GetString());
                    Assert.Equal("Chinese Page", zh.GetProperty("title").GetString());
                    Assert.Contains("这是中文页面", zh.GetProperty("content").GetString());
                }
                break;
            case "taxonomy-site":
                Assert.Contains("Taxonomy Test", Text("index/index.html"));
                Assert.Contains("Another Post", Text("another-post/index.html"));
                var index = Text("categories/index.html");
                foreach (var term in new[] { "blog", "news", "tech" }) Assert.Contains($"href=\"/categories/{term}/\"", index);
                Assert.Contains("[tech:2]", index);
                var page1 = Text("categories/tech/index.html");
                var page2 = Text("categories/tech/page/2/index.html");
                Assert.Contains("page=1 total=2", page1);
                Assert.Contains("[Another Post]", page1);
                Assert.DoesNotContain("[Taxonomy Test]", page1);
                Assert.Contains("next=/categories/tech/page/2/", page1);
                Assert.Contains("page=2 total=2", page2);
                Assert.Contains("[Taxonomy Test]", page2);
                Assert.DoesNotContain("[Another Post]", page2);
                Assert.Contains("prev=/categories/tech/", page2);
                Assert.False(File.Exists(Path.Combine(site.Output, "categories/tech/page/3/index.html")));
                break;
        }
    }

    private static void AssertReportMutations(SiteBuilder site)
    {
        foreach (var path in new[] { Path.Combine(site.Output, ".bukit/build-report.json") })
        {
            var original = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            ReportAssertions.Schema(Path.GetFileName(path), JsonSerializer.SerializeToElement(original));
            var mutant = original.DeepClone().AsObject();
            mutant["schema"] = "https://example.test/wrong-schema";
            Assert.NotNull(Record.Exception(() => ReportAssertions.Schema(Path.GetFileName(path), JsonSerializer.SerializeToElement(mutant))));
            mutant = original.DeepClone().AsObject();
            mutant["unexpected"] = true;
            Assert.Throws<InvalidDataException>(() => ReportAssertions.Schema(Path.GetFileName(path), JsonSerializer.SerializeToElement(mutant)));
            mutant = original.DeepClone().AsObject();
            mutant.Remove("summary");
            Assert.Throws<InvalidDataException>(() => ReportAssertions.Schema(Path.GetFileName(path), JsonSerializer.SerializeToElement(mutant)));
            mutant = original.DeepClone().AsObject();
            mutant["durationMs"] = "not a number";
            Assert.Throws<InvalidDataException>(() => ReportAssertions.Schema(Path.GetFileName(path), JsonSerializer.SerializeToElement(mutant)));
        }
        foreach (var (fileName, field) in new[]
        {
            ("build-report.json", "durationMs"),
            ("routes.json", "routes"),
            ("assets.json", "assets"),
            ("incremental-manifest.json", "enabled"),
            ("security-report.json", "checks"),
            ("release-bundle-checksums.json", "fileCount"),
            ("artifact-manifest.json", "artifactCount"),
            ("build-manifest-digest.json", "reportCount"),
            ("publish-url-snapshot.json", "routes"),
            ("seo-report.json", "routes"),
            ("publish-audit-report.json", "documents"),
            ("seo-route-map.json", "routes"),
            ("geo-report.json", "geoScore"),
            ("analytics-report.json", "processedHtml")
        })
        {
            var generated = JsonNode.Parse(File.ReadAllText(Path.Combine(site.Output, ".bukit", fileName)))!.AsObject();
            ReportAssertions.Schema(fileName, JsonSerializer.SerializeToElement(generated));
            generated[field] = "wrong type";
            var error = Assert.Throws<InvalidDataException>(() => ReportAssertions.Schema(fileName, JsonSerializer.SerializeToElement(generated)));
            Assert.Contains(field, error.Message);
        }
        var bytes = new byte[] { 0, 255, 2 };
        var hash = ReportAssertions.Hash(bytes);
        var aggregate = ReportAssertions.Hash(System.Text.Encoding.UTF8.GetBytes($"data.bin|{hash}|3\n"));
        var report = JsonNode.Parse($$"""{"files":[{"path":"data.bin","hash":"{{hash}}","size":3}],"fileCount":1,"bundleHash":"{{aggregate}}"}""")!.AsObject();
        void Check(JsonObject value) => ReportAssertions.Files(JsonSerializer.SerializeToElement(value), "files", ["data.bin"], _ => bytes, "fileCount", "bundleHash");
        Check(report);
        foreach (var field in new[] { "path", "hash", "size", "fileCount", "bundleHash", "files" })
        {
            var mutant = report.DeepClone().AsObject();
            if (field == "files") mutant[field] = new JsonArray();
            else if (field == "fileCount") mutant[field] = 2;
            else if (field == "bundleHash") mutant[field] = "sha256:" + new string('0', 64);
            else if (field == "size") mutant["files"]![0]![field] = 4;
            else mutant["files"]![0]![field] = "wrong";
            Assert.NotNull(Record.Exception(() => Check(mutant)));
        }
        var files = OutputSnapshot.Managed(("output", site.Output)).Entries.Where(pair => pair.Value.Kind == "file")
            .ToDictionary(pair => pair.Key[7..], pair => pair.Value.Bytes, StringComparer.Ordinal);
        var snapshot = JsonNode.Parse(files[".bukit/publish-url-snapshot.json"])!.AsObject();
        using var seo = JsonDocument.Parse(files[".bukit/seo-report.json"]);
        using var publish = JsonDocument.Parse(files[".bukit/publish-audit-report.json"]);
        void CheckSnapshot(JsonObject value) => ReportAssertions.PublishSnapshot(JsonSerializer.SerializeToElement(value),
            seo.RootElement, publish.RootElement, files, site.Config.Site.Url!);
        CheckSnapshot(snapshot);
        foreach (var field in new[] { "siteUrl", "url", "indexable", "semanticHash" })
        {
            var mutant = snapshot.DeepClone().AsObject();
            if (field == "siteUrl") mutant[field] = "https://wrong.test/";
            else if (field == "indexable") mutant["routes"]![0]![field] = false;
            else mutant["routes"]![0]![field] = field == "url" ? "https://wrong.test/" : "sha256:" + new string('0', 64);
            ReportAssertions.Schema("publish-url-snapshot.json", JsonSerializer.SerializeToElement(mutant));
            Assert.NotNull(Record.Exception(() => CheckSnapshot(mutant)));
        }
    }
}
