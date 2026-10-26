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
            })
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
            Path.Combine(_root, "manifest.json")));
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
            var output = Path.Combine(overrides.Output, language);
            var request = new I18nWorkerRequest(
                I18nWorktreeProtocol.RequestSchema, "head", configHash, contentHash, language, "text",
                configPath, root, root, config, overrides, output, Path.Combine(_root, "worker-cache", language),
                prepared.AssetsDir, prepared.ScssOutputDir, prepared.Plan.MediaCacheDir, snapshotPath,
                Path.Combine(_root, "result-" + language + ".json"));
            var (result, warnings, errors) = await engine.ExecuteI18nWorktreeVariantAsync(
                request, snapshot, logger, CancellationToken.None);
            var captured = await I18nWorktreeProtocol.CaptureVariantAsync(
                request.Head, request.ConfigHash, request.ContentHash, result, warnings, errors, CancellationToken.None);
            Assert.Equal(snapshot.Head, captured.Head);
            Assert.Equal(configHash, captured.ConfigHash);
            Assert.Equal(contentHash, captured.ContentHash);
            Assert.True(captured.Completed);
            var restored = I18nWorktreeProtocol.RestoreVariant(
                captured, prepared.Content.ContentGraph!, "head", configHash, contentHash, language, output,
                Path.Combine(overrides.CacheDir, "build-manifest." + language + ".json"));
            var page = Assert.Single(restored.RoutedDocuments);
            Assert.Contains(language == "en" ? "English body" : "Chinese body",
                await File.ReadAllTextAsync(Path.Combine(output, page.Route.OutputPath)), StringComparison.Ordinal);
            variants.Add(restored);
        }
        Assert.Equal(1, factory.LoadCount);
        Assert.Equal(new[] { "en", "zh-CN" }, variants.Select(x => x.Language));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => engine.FinalizeI18nWorktreeBuildAsync(
            prepared, duplicate ? [variants[0], variants[0]] : [variants[0]], 0, 0, CancellationToken.None));

        Assert.Contains("complete configured language set", error.Message, StringComparison.Ordinal);
        Assert.Equal(oldOutput, await File.ReadAllBytesAsync(outputSentinel));
        Assert.Equal(oldCache, await File.ReadAllBytesAsync(cacheSentinel));
        Assert.Equal(1, factory.LoadCount);
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
            Path.Combine(_root, "manifest.json")));
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
            Path.Combine(_root, "manifest.json")));
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
            Path.Combine(_root, "manifest.json")));
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
            Path.Combine(_root, "manifest.json")));
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
