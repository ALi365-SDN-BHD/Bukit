using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Engine.Output;
using Bukit.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nWorktreeProtocolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-i18n-protocol-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void WorkerConfig_KeepsMiniProgramSection()
    {
        // Workers rebuild their config from this JSON; a dropped section would silently disable the output.
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "s", Title = "S" },
            Content = new ContentConfig(),
            MiniProgram = new MiniProgramConfig
            {
                Enabled = true,
                OutputDir = "app",
                Datasets = [new MiniProgramDatasetConfig { Name = "nav", Kind = "menu", Menu = "main", Sort = new MiniProgramSortConfig("order", true), Types = new Dictionary<string, string> { ["order"] = "number" } }]
            }
        };

        var json = JsonSerializer.Serialize(config, I18nWorktreeJsonContext.Default.AppConfig);
        var restored = JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.AppConfig)!;

        Assert.True(restored.MiniProgram.Enabled);
        Assert.Equal("app", restored.MiniProgram.OutputDir);
        var dataset = Assert.Single(restored.MiniProgram.Datasets);
        Assert.Equal(("nav", "menu", "main"), (dataset.Name, dataset.Kind, dataset.Menu));
        Assert.Equal(new MiniProgramSortConfig("order", true), dataset.Sort);
        Assert.Equal("number", dataset.Types!["order"]);
        Assert.NotEqual(
            I18nWorktreeProtocol.ComputeConfigHash(config, new ConfigOverrides()),
            I18nWorktreeProtocol.ComputeConfigHash(config with { MiniProgram = config.MiniProgram with { OutputDir = "other" } }, new ConfigOverrides()));
    }

    [Fact]
    public void WorkerConfig_KeepsContentLinks()
    {
        // Workers rebuild their config from this JSON; dropped links would leave every page without them.
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "s", Title = "S" },
            Content = new ContentConfig
            {
                Links =
                [
                    new ContentLinkConfig
                    {
                        Collection = "route",
                        Field = "days[].attractions",
                        Target = new ContentLinkTargetConfig { Collection = "attraction", Key = "code" },
                        Select = ["image"],
                        Backref = new ContentLinkBackrefConfig { Name = "routes", Limit = 6, Select = ["region"] }
                    }
                ]
            }
        };

        var json = JsonSerializer.Serialize(config, I18nWorktreeJsonContext.Default.AppConfig);
        var restored = JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.AppConfig)!;

        var link = Assert.Single(restored.Content.Links);
        Assert.Equal(("route", "days[].attractions", "attraction", "code"), (link.Collection, link.Field, link.Target.Collection, link.Target.Key));
        Assert.Equal(["image"], link.Select);
        Assert.Equal(("routes", 6, "region"), (link.Backref!.Name, link.Backref.Limit, Assert.Single(link.Backref.Select)));
        Assert.NotEqual(
            I18nWorktreeProtocol.ComputeConfigHash(config, new ConfigOverrides()),
            I18nWorktreeProtocol.ComputeConfigHash(config with { Content = config.Content with { Links = [] } }, new ConfigOverrides()));
    }

    [Fact]
    public void WorkerConfig_KeepsDerivedFields()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "s", Title = "S" },
            Content = new ContentConfig
            {
                DerivedFields = [new ContentDerivedFieldConfig { Collection = "city", Field = "seo_title", Value = "{{ name }}", Mode = "override" }]
            }
        };

        var json = JsonSerializer.Serialize(config, I18nWorktreeJsonContext.Default.AppConfig);
        var restored = JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.AppConfig)!;

        var rule = Assert.Single(restored.Content.DerivedFields);
        Assert.Equal(("city", "seo_title", "{{ name }}", "override"), (rule.Collection, rule.Field, rule.Value, rule.Mode));
        Assert.NotEqual(
            I18nWorktreeProtocol.ComputeConfigHash(config, new ConfigOverrides()),
            I18nWorktreeProtocol.ComputeConfigHash(config with { Content = config.Content with { DerivedFields = [] } }, new ConfigOverrides()));
    }

    [Fact]
    public void WorkerConfig_KeepsSiteQueries()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "s",
                Title = "S",
                Queries =
                [
                    new SiteQueryConfig
                    {
                        Name = "top",
                        From = "route",
                        Where = [new SiteQueryFilterConfig { Field = "region", Values = ["north"] }, new SiteQueryFilterConfig { HasBackref = "routes" }],
                        Sort = [new SiteQuerySortConfig { Field = "featured", Direction = "desc", Values = ["a"] }],
                        Select = ["image"],
                        Group = new SiteQueryGroupConfig { Field = "months", Values = ["1"] },
                        Limit = 6
                    }
                ]
            },
            Content = new ContentConfig()
        };

        var json = JsonSerializer.Serialize(config, I18nWorktreeJsonContext.Default.AppConfig);
        var restored = JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.AppConfig)!;

        var query = Assert.Single(restored.Site.Queries);
        Assert.Equal(("top", "route", 6, "image"), (query.Name, query.From, query.Limit, Assert.Single(query.Select)));
        Assert.Equal(("region", "north", "routes"), (query.Where[0].Field, Assert.Single(query.Where[0].Values), query.Where[1].HasBackref));
        Assert.Equal(("featured", "desc", "a"), (query.Sort[0].Field, query.Sort[0].Direction, Assert.Single(query.Sort[0].Values)));
        Assert.Equal(("months", "1"), (query.Group!.Field, Assert.Single(query.Group.Values)));
        Assert.NotEqual(
            I18nWorktreeProtocol.ComputeConfigHash(config, new ConfigOverrides()),
            I18nWorktreeProtocol.ComputeConfigHash(config with { Site = config.Site with { Queries = [] } }, new ConfigOverrides()));
    }

    [Fact]
    public void WorkerConfig_KeepsRelatedFieldIndicesAndCollectionRules()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "s",
                Title = "S",
                Related = new RelatedConfig
                {
                    Indices = [new RelatedIndexConfig { Name = "field", Field = "region" }],
                    Collections = [new RelatedCollectionConfig { Collection = "route", Threshold = 0, Limit = 4, Indices = [new RelatedIndexConfig { Name = "field", Field = "cities", Weight = 2 }] }]
                }
            },
            Content = new ContentConfig()
        };

        var json = JsonSerializer.Serialize(config, I18nWorktreeJsonContext.Default.AppConfig);
        var restored = JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.AppConfig)!.Site.Related;

        Assert.Equal("region", Assert.Single(restored.Indices).Field);
        var route = Assert.Single(restored.Collections);
        Assert.Equal(("route", 0, 4, "cities", 2), (route.Collection, route.Threshold, route.Limit, Assert.Single(route.Indices).Field, route.Indices[0].Weight));
    }

    [Fact]
    public void WorkerConfig_KeepsEnumLabels()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "s", Title = "S" },
            Content = new ContentConfig
            {
                ModelSchema = new ContentModelSchemaConfig
                {
                    CustomFields = [new CustomFieldDefinitionConfig { Name = "audience", Enum = ["family"], EnumLabels = new Dictionary<string, string> { ["family"] = "亲子" } }]
                }
            }
        };

        var json = JsonSerializer.Serialize(config, I18nWorktreeJsonContext.Default.AppConfig);
        var restored = JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.AppConfig)!;

        Assert.Equal("亲子", restored.Content.ModelSchema!.CustomFields![0].EnumLabels!["family"]);
    }

    [Fact]
    public void WorkerConfig_KeepsCloudBaseSourceSettings()
    {
        // Workers validate the config rebuilt from this JSON; a cloudbase source without its settings fails validation.
        var source = new ContentSourceConfig
        {
            Type = "cloudbase",
            CloudBase = new CloudBaseConfig
            {
                EnvId = "env-1",
                Table = "posts",
                Filter = new CloudBaseFilterConfig { Field = "status", Value = "published" },
                Sort = new CloudBaseSortConfig { Field = "publish_at", Direction = "desc" },
                FileFields = ["cover"]
            }
        };
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "s", Title = "S" },
            Content = new ContentConfig { Sources = [source] }
        };

        var json = JsonSerializer.Serialize(config, I18nWorktreeJsonContext.Default.AppConfig);
        var restored = JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.AppConfig)!;

        var cloudBase = Assert.Single(restored.Content.Sources!).CloudBase;
        Assert.NotNull(cloudBase);
        Assert.Equal(("env-1", "posts"), (cloudBase.EnvId, cloudBase.Table));
        Assert.Equal(source.CloudBase.Filter, cloudBase.Filter);
        Assert.Equal(source.CloudBase.Sort, cloudBase.Sort);
        Assert.Equal(["cover"], cloudBase.FileFields);
        var changedSource = source with { CloudBase = source.CloudBase with { Table = "other" } };
        Assert.NotEqual(
            I18nWorktreeProtocol.ComputeConfigHash(config, new ConfigOverrides()),
            I18nWorktreeProtocol.ComputeConfigHash(config with { Content = config.Content with { Sources = [changedSource] } }, new ConfigOverrides()));
    }

    [Fact]
    public void WorkerConfig_KeepsInternalSiteSeoAndImageSettings()
    {
        // These settings are internal, so the worker JSON keeps them only through JsonInclude.
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "s",
                Title = "S",
                I18n = new Dictionary<string, SiteLanguageIdentityConfig> { ["zh-CN"] = new() { Title = "站点" } },
                Seo = new SeoConfig
                {
                    Authors = new Dictionary<string, SeoAuthorProfileConfig> { ["aisha"] = new() { Name = "Aisha", Url = "https://example.test/aisha/" } },
                    Schema = new SeoSchemaConfig { ArticleType = "Article" }
                },
                Collections = new Dictionary<string, CollectionConfig> { ["news"] = new() { Permalink = "/news/{slug}/", ArticleType = "NewsArticle" } }
            },
            Content = new ContentConfig(),
            Theme = new ThemeConfig { Images = new ImageOptimizationConfig { SizesAttribute = "100vw", FallbackVariants = ImageFallbackVariants.None } }
        };

        var json = JsonSerializer.Serialize(config, I18nWorktreeJsonContext.Default.AppConfig);
        var restored = JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.AppConfig)!;

        Assert.Equal("站点", restored.Site.I18n!["zh-CN"].Title);
        Assert.Equal(("Aisha", "https://example.test/aisha/"), (restored.Site.Seo.Authors!["aisha"].Name, restored.Site.Seo.Authors["aisha"].Url));
        // The restored map is case-sensitive, so a byline in another case must still find its profile.
        var byline = SeoAuthorResolver.Resolve(null, null, null, restored.Site.Seo with { DefaultAuthor = "AISHA" });
        Assert.Equal("https://example.test/aisha/", byline.Primary!.Url);
        Assert.Equal("Article", restored.Site.Seo.Schema.ArticleType);
        Assert.Equal("NewsArticle", restored.Site.Collections!["news"].ArticleType);
        Assert.Equal(("100vw", ImageFallbackVariants.None), (restored.Theme.Images!.SizesAttribute, restored.Theme.Images.FallbackVariants));
        Assert.NotEqual(
            I18nWorktreeProtocol.ComputeConfigHash(config, new ConfigOverrides()),
            I18nWorktreeProtocol.ComputeConfigHash(config with { Theme = new ThemeConfig { Images = config.Theme.Images with { SizesAttribute = "50vw" } } }, new ConfigOverrides()));
    }

    private static readonly SiteConfig PrefixedSite = new()
    {
        Name = "prefixed",
        Title = "Prefixed",
        Languages = ["en", "zh-CN"],
        DefaultLanguage = "en",
        DefaultLanguageInSubdir = true
    };

    public I18nWorktreeProtocolTests() => Directory.CreateDirectory(_root);

    public void Dispose() => TestCleanup.DeleteDirectory(_root, recursive: true);

    [Fact]
    public async Task ContentSnapshot_UsesStableProjectPaths_AndRoundTripsObjectFields()
    {
        var worktreeRoot = Path.Combine(_root, "random-worktree");
        var projectRoot = Path.Combine(_root, "project");
        var sourcePath = Path.Combine(worktreeRoot, "content", "page.md");
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourcePath"] = new("text", sourcePath),
            ["nested"] = new("object", new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["items"] = new List<object> { "one", 2L }
            }),
            ["related"] = new("relation", new List<string> { "rel-a", "rel-b" })
        };
        var document = ContentDocument.Create(
            "page",
            "Page",
            "page",
            DateTimeOffset.UnixEpoch,
            contentHtml: null,
            fields,
            bodyKey: sourcePath) with
        {
            Source = new ContentSourceInfo("markdown", SourcePath: sourcePath)
        };
        var content = new ContentPipelineResult(
            [document],
            new OneBodyStore(new("<p>body</p>")),
            []);

        var snapshot = await I18nWorktreeProtocol.CaptureContentAsync(
            "head",
            DateTimeOffset.UnixEpoch,
            content,
            worktreeRoot,
            projectRoot,
            CancellationToken.None);
        var path = Path.Combine(_root, "snapshot.json");
        await I18nWorktreeProtocolStore.WriteContentAsync(path, snapshot, CancellationToken.None);
        Assert.DoesNotContain("<p>body</p>", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
        var restored = await I18nWorktreeProtocolStore.ReadContentAsync(path, CancellationToken.None);

        var expectedPath = Path.Combine(projectRoot, "content", "page.md");
        var restoredDocument = Assert.Single(restored.Documents);
        Assert.Equal(expectedPath, restoredDocument.Body.BodyKey);
        Assert.Equal(expectedPath, restoredDocument.Source.SourcePath);
        Assert.Equal(expectedPath, restoredDocument.CustomFields!["sourcePath"].Value);
        Assert.True(restored.Bodies.ContainsKey(expectedPath));
        Assert.Single(restored.BodyFiles);
        var nested = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(restoredDocument.CustomFields["nested"].Value);
        Assert.Equal(true, nested["enabled"]);
        Assert.Equal(new object[] { "one", 2L }, Assert.IsAssignableFrom<IReadOnlyList<object>>(nested["items"]));
        // Workers check string lists by type (pages_index relation ids, media URLs): a list of
        // strings must come back as one, and still read as a list of objects.
        var related = restoredDocument.CustomFields["related"].Value;
        Assert.Equal(["rel-a", "rel-b"], Assert.IsAssignableFrom<IReadOnlyList<string>>(related));
        Assert.IsAssignableFrom<IEnumerable<object>>(related);
    }

    [Fact]
    public async Task ContentSnapshot_RejectsChangedBodyFile()
    {
        var key = Path.Combine(_root, "content", "page.md");
        var document = ContentDocument.Create("page", "Page", "page", DateTimeOffset.UnixEpoch,
            contentHtml: null, bodyKey: key);
        var content = new ContentPipelineResult([document], new OneBodyStore(new("<p>original</p>")), []);
        var snapshot = await I18nWorktreeProtocol.CaptureContentAsync(
            "head", DateTimeOffset.UnixEpoch, content, _root, _root, CancellationToken.None);
        var path = Path.Combine(_root, "snapshot.json");
        await I18nWorktreeProtocolStore.WriteContentAsync(path, snapshot, CancellationToken.None);
        var restored = await I18nWorktreeProtocolStore.ReadContentAsync(path, CancellationToken.None);
        Assert.Equal("<p>original</p>", restored.Bodies[key].Html);
        var bodyFile = Assert.Single(restored.BodyFiles).Value;
        await File.WriteAllTextAsync(Path.Combine(_root, bodyFile.Path), "<p>changed</p>");

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => I18nWorktreeProtocolStore.ReadContentAsync(path, CancellationToken.None));

        Assert.Contains("body hash is invalid", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Schema")]
    [InlineData("Completed")]
    [InlineData("Head")]
    [InlineData("Language")]
    [InlineData("OutputDir")]
    public void VariantResult_RejectsOtherIdentityMismatches(string field)
    {
        var output = Path.Combine(_root, "output");
        var snapshot = CreateVariantSnapshot(output);
        snapshot = field switch
        {
            "Schema" => snapshot with { Schema = "wrong" },
            "Completed" => snapshot with { Completed = false },
            "Head" => snapshot with { Head = "wrong" },
            "Language" => snapshot with { Language = "zh-CN" },
            "OutputDir" => snapshot with { OutputDir = Path.Combine(_root, "other-output") },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        Assert.Throws<InvalidDataException>(() => I18nWorktreeProtocol.RestoreVariant(
            snapshot, CanonicalContentGraph.Empty, "head", "config-hash", "hash", "en", output,
            Path.Combine(_root, "manifest.json"), PrefixedSite));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Finalize_RejectsMissingOrDuplicateLanguage_AfterSingleSharedContentFetch(bool duplicate)
    {
        var root = Path.Combine(_root, "project");
        var configPath = CreateSite(root);
        var config = ConfigLoader.Load(configPath);
        var finalOutput = Path.Combine(root, "dist");
        var finalCache = Path.Combine(root, ".cache");
        Directory.CreateDirectory(finalOutput);
        Directory.CreateDirectory(finalCache);
        var outputSentinel = Path.Combine(finalOutput, "sentinel.bin");
        var cacheSentinel = Path.Combine(finalCache, "sentinel.bin");
        byte[] oldOutput = [0, 1, 255];
        byte[] oldCache = [255, 2, 0];
        await File.WriteAllBytesAsync(outputSentinel, oldOutput);
        await File.WriteAllBytesAsync(cacheSentinel, oldCache);
        var overrides = new ConfigOverrides
        {
            Output = Path.Combine(_root, "staging-output"),
            CacheDir = Path.Combine(_root, "staging-cache"),
            Clean = false,
            Jobs = 1
        };
        var factory = new CountingContentProviderFactory();
        var logger = new ConsoleLogger(LogLevel.Error);
        var engine = new SiteEngine(logger, factory, new DefaultSearchIndexBuilder());
        await using var prepared = await engine.PrepareI18nWorktreeBuildAsync(
            config, root, root, finalOutput, overrides, CancellationToken.None);
        Assert.Contains("i18n:home", prepared.SeoAlternates.Keys);
        var variants = await RunWorkerVariantsAsync(engine, prepared, config, configPath, root, overrides, logger);
        Assert.Equal(1, factory.LoadCount);
        Assert.Equal(new[] { "en", "zh-CN" }, variants.Select(x => x.Language));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => engine.FinalizeI18nWorktreeBuildAsync(
            prepared, duplicate ? [variants[0], variants[0]] : [variants[0]], 0, 0, CancellationToken.None));

        Assert.Contains("complete configured language set", error.Message, StringComparison.Ordinal);
        Assert.Equal(oldOutput, await File.ReadAllBytesAsync(outputSentinel));
        Assert.Equal(oldCache, await File.ReadAllBytesAsync(cacheSentinel));
        Assert.Equal(1, factory.LoadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorktreeBuild_ReportsVariantDiagnosticsOnceAfterTheWorkerRoundTrip(bool rootOutputsFail)
    {
        var root = Path.Combine(_root, "project");
        var configPath = CreateSite(root);
        Directory.CreateDirectory(Path.Combine(root, "data"));
        File.WriteAllText(Path.Combine(root, "data", "menu.json"),
            "{\"image\":\"https://bucket.s3.amazonaws.com/a.png?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Signature=abc\"}");
        var config = ConfigLoader.Load(configPath);
        var overrides = new ConfigOverrides
        {
            Output = Path.Combine(_root, "staging-output"),
            CacheDir = Path.Combine(_root, "staging-cache"),
            Clean = false,
            Incremental = true,
            Jobs = 1
        };
        var logger = new WarningLogger();
        var engine = new SiteEngine(logger, new CountingContentProviderFactory(), new DefaultSearchIndexBuilder());
        await using var prepared = await engine.PrepareI18nWorktreeBuildAsync(
            config, root, root, Path.Combine(root, "dist"), overrides, CancellationToken.None);
        var variants = await RunWorkerVariantsAsync(engine, prepared, config, configPath, root, overrides, logger);
        Assert.All(variants, variant => Assert.Equal([new SignedLinkFinding("site.data_files", 1)], variant.SignedLinks));
        variants[0] = variants[0] with
        {
            AuditIssues = [.. variants[0].AuditIssues, new SeoAuditIssue("warning", "test.variant_only", "/a/", "Variant-only issue.")]
        };

        if (rootOutputsFail)
        {
            // A directory where the root agent-manifest.json goes makes writing the root outputs fail.
            Directory.CreateDirectory(Path.Combine(prepared.Plan.OutputDir, "agent-manifest.json"));
            await Assert.ThrowsAnyAsync<Exception>(() => engine.FinalizeI18nWorktreeBuildAsync(prepared, variants, 0, 0, CancellationToken.None));
        }
        else
        {
            await engine.FinalizeI18nWorktreeBuildAsync(prepared, variants, 0, 0, CancellationToken.None);
        }

        var warning = Assert.Single(logger.Warnings, message => message.Contains("event=incremental.signed_links", StringComparison.Ordinal));
        Assert.Contains(" language=en,zh-CN keys=site.data_files:1 ", warning, StringComparison.Ordinal);
        var issue = Assert.Single(logger.Warnings, message => message.Contains("code=test.variant_only", StringComparison.Ordinal));
        Assert.Contains("/en/a/", issue, StringComparison.Ordinal);
    }

    // Runs each language the way a worktree worker does, through the content and result files.
    private async Task<List<BuildVariantResult>> RunWorkerVariantsAsync(
        SiteEngine engine,
        I18nPreparedBuild prepared,
        AppConfig config,
        string configPath,
        string root,
        ConfigOverrides overrides,
        ILogger logger)
    {
        var snapshot = await I18nWorktreeProtocol.CaptureContentAsync(
            "head", prepared.Plan.StartedAt, prepared.Content, root, root, CancellationToken.None);
        var snapshotPath = Path.Combine(_root, "content-snapshot.json");
        await I18nWorktreeProtocolStore.WriteContentAsync(snapshotPath, snapshot, CancellationToken.None);
        snapshot = await I18nWorktreeProtocolStore.ReadContentAsync(snapshotPath, CancellationToken.None);
        Assert.Equal(2, snapshot.Documents.Count);
        Assert.Equal(2, snapshot.BodyFiles.Count);
        var contentHash = await I18nWorktreeProtocolStore.Sha256Async(snapshotPath, CancellationToken.None);
        var configHash = I18nWorktreeProtocol.ComputeConfigHash(config, overrides);
        var variants = new List<BuildVariantResult>();
        foreach (var language in new[] { "en", "zh-CN" })
        {
            var output = Path.Combine(overrides.Output!, language);
            var request = new I18nWorkerRequest(
                I18nWorktreeProtocol.RequestSchema, "head", configHash, contentHash, language, "text",
                configPath, root, root, config, overrides, output, Path.Combine(_root, "worker-cache", language),
                prepared.AssetsDir, prepared.ScssOutputDir, prepared.Plan.MediaCacheDir, prepared.SeoAlternates,
                snapshotPath, Path.Combine(_root, "result-" + language + ".json"));
            var (result, warnings, errors) = await engine.ExecuteI18nWorktreeVariantAsync(
                request, snapshot, logger, CancellationToken.None);
            var captured = await I18nWorktreeProtocol.CaptureVariantAsync(
                request.Head, request.ConfigHash, request.ContentHash, result, warnings, errors, CancellationToken.None);
            await I18nWorktreeProtocolStore.WriteResultAsync(request.ResultPath, captured, CancellationToken.None);
            captured = await I18nWorktreeProtocolStore.ReadResultAsync(request.ResultPath, CancellationToken.None);
            Assert.Equal(snapshot.Head, captured.Head);
            Assert.Equal(configHash, captured.ConfigHash);
            Assert.Equal(contentHash, captured.ContentHash);
            Assert.True(captured.Completed);
            var restored = I18nWorktreeProtocol.RestoreVariant(
                captured, prepared.Content.ContentGraph!, "head", configHash, contentHash, language, output,
                Path.Combine(overrides.CacheDir!, "build-manifest." + language + ".json"), config.Site);
            var page = Assert.Single(restored.RoutedDocuments);
            Assert.Contains(language == "en" ? "English body" : "Chinese body",
                await File.ReadAllTextAsync(Path.Combine(output, page.Route.OutputPath)), StringComparison.Ordinal);
            variants.Add(restored);
        }
        return variants;
    }

    [Fact]
    public void VariantResult_RejectsIdentityMismatch()
    {
        var output = Path.Combine(_root, "output");
        var snapshot = CreateVariantSnapshot(output) with { ContentHash = "wrong" };

        Assert.Throws<InvalidDataException>(() => I18nWorktreeProtocol.RestoreVariant(
            snapshot,
            CanonicalContentGraph.Empty,
            "head",
            "config-hash",
            "hash",
            "en",
            output,
            Path.Combine(_root, "manifest.json"), PrefixedSite));
    }

    [Fact]
    public void VariantResult_RejectsConfigHashMismatch()
    {
        var output = Path.Combine(_root, "output");
        var snapshot = CreateVariantSnapshot(output) with { ConfigHash = "wrong" };

        Assert.Throws<InvalidDataException>(() => I18nWorktreeProtocol.RestoreVariant(
            snapshot,
            CanonicalContentGraph.Empty,
            "head",
            "config-hash",
            "hash",
            "en",
            output,
            Path.Combine(_root, "manifest.json"), PrefixedSite));
    }

    [Fact]
    public void VariantResult_RejectsOutputTraversal()
    {
        var output = Path.Combine(_root, "output");
        var snapshot = CreateVariantSnapshot(output) with
        {
            PlannedOutputs = [new I18nAssetOutputSnapshot("test", "../escape.html", AssetOutputCategory.Render, AssetOutputOperation.Render)]
        };

        Assert.Throws<OutputPathSecurityException>(() => I18nWorktreeProtocol.RestoreVariant(
            snapshot,
            CanonicalContentGraph.Empty,
            "head",
            "config-hash",
            "hash",
            "en",
            output,
            Path.Combine(_root, "manifest.json"), PrefixedSite));
    }

    [Fact]
    public void VariantResult_RejectsReservedLanguagePrefixForRootDefaultLanguage()
    {
        var output = Path.Combine(_root, "output");
        var snapshot = CreateVariantSnapshot(output) with
        {
            PlannedOutputs = [new I18nAssetOutputSnapshot("test", "zh-CN/sw.js", AssetOutputCategory.Plugin, AssetOutputOperation.Generate)]
        };
        var site = new SiteConfig
        {
            Name = "root-language",
            Title = "Root language",
            Languages = ["en", "zh-CN"],
            DefaultLanguage = "en",
            DefaultLanguageInSubdir = false
        };

        var error = Assert.Throws<BukitException>(() => I18nWorktreeProtocol.RestoreVariant(
            snapshot,
            CanonicalContentGraph.Empty,
            "head",
            "config-hash",
            "hash",
            "en",
            output,
            Path.Combine(_root, "manifest.json"),
            site));

        Assert.Equal(DiagnosticCode.I18nRouteReservedLanguagePrefix, error.Code);
        Assert.Contains("zh-CN", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VariantResult_AcceptsLegacyRedirectsUnderTheDefaultLanguageCode()
    {
        var output = Path.Combine(_root, "output");
        var snapshot = CreateVariantSnapshot(output) with
        {
            DerivedDocuments =
            [
                RedirectPageDocument.Create("i18n-legacy-en-0123456789abcdef", "Redirect", "i18n-legacy-en-0123456789abcdef",
                    DateTimeOffset.UnixEpoch, "https://example.test/about/", "/en/about/", "en/about/index.html")
            ]
        };
        var site = new SiteConfig
        {
            Name = "root-language",
            Title = "Root language",
            Languages = ["en", "zh-CN"],
            DefaultLanguage = "en",
            DefaultLanguageInSubdir = false,
            DefaultLanguageLegacyRedirects = true
        };

        I18nWorktreeProtocol.RestoreVariant(
            snapshot,
            CanonicalContentGraph.Empty,
            "head",
            "config-hash",
            "hash",
            "en",
            output,
            Path.Combine(_root, "manifest.json"),
            site);
    }

    [Fact]
    public void VariantResult_RejectsTraversalOutsidePlannedOutputs()
    {
        var output = Path.Combine(_root, "output");
        var snapshot = CreateVariantSnapshot(output) with
        {
            StaticRoutes = [new RouteInfo("/escape", "../escape.html", "page.html")]
        };

        Assert.Throws<OutputPathSecurityException>(() => I18nWorktreeProtocol.RestoreVariant(
            snapshot,
            CanonicalContentGraph.Empty,
            "head",
            "config-hash",
            "hash",
            "en",
            output,
            Path.Combine(_root, "manifest.json"), PrefixedSite));
    }

    [Fact]
    public async Task WorktreeBuild_WarnsWhenLegacyRedirectsAreIgnoredInThePrefixedLayout()
    {
        var root = Path.Combine(_root, "project");
        var configPath = CreateSite(root);
        var loaded = ConfigLoader.Load(configPath);
        var config = loaded with { Site = loaded.Site with { DefaultLanguageLegacyRedirects = true } };
        var overrides = new ConfigOverrides
        {
            Output = Path.Combine(_root, "staging-output"),
            CacheDir = Path.Combine(_root, "staging-cache"),
            Clean = false,
            Jobs = 1
        };
        var logger = new WarningLogger();
        var engine = new SiteEngine(logger, new CountingContentProviderFactory(), new DefaultSearchIndexBuilder());

        await using var prepared = await engine.PrepareI18nWorktreeBuildAsync(
            config, root, root, Path.Combine(root, "dist"), overrides, CancellationToken.None);

        Assert.Single(logger.Warnings, message => message.Contains("event=i18n.config.legacy_redirects_ignored", StringComparison.Ordinal));
    }

    private static string CreateSite(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "content", "en"));
        Directory.CreateDirectory(Path.Combine(root, "content", "zh"));
        Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
        var path = Path.Combine(root, "site.yaml");
        File.WriteAllText(path, """
            site:
              name: i18n-protocol-test
              title: I18N Protocol Test
              url: https://example.test
              languages: [en, zh-CN]
              defaultLanguage: en
              defaultLanguageInSubdir: true
              collections:
                page:
                  permalink: /{slug}/
                  template: pages/page.html
            content:
              sources:
                - type: markdown
                  name: pages
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
              clean: false
            theme:
              layouts: layouts
            """);
        File.WriteAllText(Path.Combine(root, "content", "en", "index.md"),
            "---\ntitle: English\nlanguage: en\ni18nKey: home\npublishAt: 2024-01-01T00:00:00Z\nupdatedAt: 2024-01-02T00:00:00Z\n---\nEnglish body");
        File.WriteAllText(Path.Combine(root, "content", "zh", "index.md"),
            "---\ntitle: Chinese\nlanguage: zh-CN\ni18nKey: home\npublishAt: 2024-01-01T00:00:00Z\nupdatedAt: 2024-01-02T00:00:00Z\n---\nChinese body");
        File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"),
            "<!doctype html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{{ content }}");
        return path;
    }

    private sealed class WarningLogger : ILogger
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _warnings = new();

        public IReadOnlyCollection<string> Warnings => _warnings;

        public void Debug(string message) { }

        public void Info(string message) { }

        public void Warn(string message) => _warnings.Enqueue(message);

        public void Error(string message) { }
    }

    private sealed class CountingContentProviderFactory : IContentProviderFactory
    {
        private readonly DefaultContentProviderFactory _inner = new();
        public int LoadCount { get; private set; }

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
            => new CountingContentProvider(_inner.Create(config, rootDir, isCi, logger), this);

        public Task<RawContentLoadResult> LocalizeContentImagesAsync(
            RawContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger,
            CancellationToken cancellationToken)
            => _inner.LocalizeContentImagesAsync(result, media, rootDir, cacheDir, logger, cancellationToken);

        private sealed class CountingContentProvider(IContentProvider inner, CountingContentProviderFactory owner) : IContentProvider
        {
            public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
            {
                owner.LoadCount++;
                return inner.LoadRawAsync(cancellationToken);
            }
        }
    }

    [Fact]
    public void VariantResult_CarriesDiagnostics_AndReadsResultsWithoutThem()
    {
        var output = Path.Combine(_root, "output");
        var snapshot = CreateVariantSnapshot(output) with
        {
            SignedLinks = [new SignedLinkFinding("site.data_files", 2)],
            AuditIssues = [new SeoAuditIssue("error", "seo.noindex_in_sitemap", "/a/", "Noindex route is in the sitemap.")]
        };
        var json = JsonSerializer.Serialize(snapshot, I18nWorktreeJsonContext.Default.I18nVariantSnapshot);
        var withoutField = JsonNode.Parse(json)!.AsObject();
        Assert.True(withoutField.Remove("signedLinks"));
        Assert.True(withoutField.Remove("auditIssues"));

        var restored = Restore(JsonSerializer.Deserialize(json, I18nWorktreeJsonContext.Default.I18nVariantSnapshot)!);
        var older = Restore(JsonSerializer.Deserialize(withoutField.ToJsonString(), I18nWorktreeJsonContext.Default.I18nVariantSnapshot)!);

        Assert.Equal([new SignedLinkFinding("site.data_files", 2)], restored.SignedLinks);
        Assert.Equal(snapshot.AuditIssues, restored.AuditIssues);
        Assert.Empty(older.SignedLinks);
        Assert.Empty(older.AuditIssues);

        BuildVariantResult Restore(I18nVariantSnapshot value) => I18nWorktreeProtocol.RestoreVariant(
            value, CanonicalContentGraph.Empty, "head", "config-hash", "hash", "en", output,
            Path.Combine(_root, "manifest.json"), PrefixedSite);
    }

    private static I18nVariantSnapshot CreateVariantSnapshot(string output)
        => new(
            I18nWorktreeProtocol.ResultSchema,
            "head",
            "config-hash",
            "hash",
            "en",
            Completed: true,
            output,
            "/en",
            SearchSnippetsEnabled: false,
            new Dictionary<string, ContentBody>(StringComparer.Ordinal),
            [],
            new Dictionary<string, SeoIndexEntry>(StringComparer.Ordinal),
            new Dictionary<string, SeoModel>(StringComparer.Ordinal),
            Array.Empty<PluginExecutionInfo>(),
            RenderedCount: 0,
            SkippedCount: 0,
            new Dictionary<string, int>(StringComparer.Ordinal),
            BuildStageMetrics.Empty,
            [],
            [],
            [],
            [],
            [],
            Array.Empty<PluginOutputTrackingInfo>(),
            new I18nBuildManifestSnapshot(
                3,
                output,
                [],
                string.Empty,
                new Dictionary<string, BuildManifestEntry>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, PluginOutputManifestEntry>(StringComparer.Ordinal)),
            string.Empty,
            IncrementalEnabled: true,
            [],
            WarningCount: 0,
            ErrorCount: 0);

    private sealed class OneBodyStore(ContentBody body) : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(body);
    }
}
