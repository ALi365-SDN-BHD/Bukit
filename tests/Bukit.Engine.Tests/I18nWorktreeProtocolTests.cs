using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Engine.Output;
using Bukit.Rendering;
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
        await I18nWorktreeProtocol.WriteContentAsync(path, snapshot, CancellationToken.None);
        Assert.DoesNotContain("<p>body</p>", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
        var restored = await I18nWorktreeProtocol.ReadContentAsync(path, CancellationToken.None);

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
