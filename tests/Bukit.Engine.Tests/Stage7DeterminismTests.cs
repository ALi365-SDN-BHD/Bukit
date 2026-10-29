using System.Text.Json;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class Stage7DeterminismTests
{
    [Theory, Trait("Category", "Integration")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncrementalBuild_AfterOneEdit_MatchesFreshFullBuild(bool disablePagesIndex)
    {
        using var incrementalSite = new SiteBuilder("taxonomy-site");
        ConfigurePagesIndex(incrementalSite, disablePagesIndex);
        var first = await Build(incrementalSite, clean: true, incremental: true);
        Assert.True(first.Incremental.Enabled);
        Assert.Equal(0, first.Incremental.CacheHitCount);
        Assert.True(first.Incremental.CacheMissCount > 0);
        var initial = OutputSnapshot.Public(incrementalSite.Output);
        Assert.True(initial.Entries.ContainsKey("index/index.html"));

        var unchanged = await Build(incrementalSite, clean: false, incremental: true);
        Assert.True(unchanged.Incremental.Enabled);
        Assert.True(unchanged.Incremental.CacheHitCount > 0);
        Assert.True(unchanged.Incremental.CacheMissCount < first.Incremental.CacheMissCount);
        initial.AssertEqual(OutputSnapshot.Public(incrementalSite.Output));
        var wrongCount = unchanged with { Incremental = unchanged.Incremental with { CacheHitCount = unchanged.Incremental.CacheHitCount + 1 } };
        var countError = Assert.ThrowsAny<Exception>(() => ReportAssertions.Generated(incrementalSite, wrongCount,
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, priorOutputMarker: true, expectedIncremental: true));
        Assert.Contains("build/cache/routes semantics", countError.Message, StringComparison.Ordinal);
        var markerError = Assert.ThrowsAny<Exception>(() => ReportAssertions.Generated(incrementalSite, unchanged,
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, priorOutputMarker: false, expectedIncremental: true));
        Assert.Contains("release-bundle-checksums", markerError.Message, StringComparison.Ordinal);

        const string input = "content/another-post.md";
        var original = File.ReadAllText(incrementalSite.Sandbox.PathFor(input));
        const string before = "This is another post in the news category.";
        const string after = "This is the updated post in the news category.";
        Assert.Contains(before, original, StringComparison.Ordinal);
        var changedInput = original.Replace(before, after, StringComparison.Ordinal);
        incrementalSite.Sandbox.Write(input, changedInput);
        RecordingRenderer? renderer = null;
        var edited = await Build(incrementalSite, clean: false, incremental: true,
            rendererFactory: disablePagesIndex ? layouts => renderer = new RecordingRenderer(layouts) : null);
        Assert.True(edited.Incremental.Enabled);
        Assert.True(edited.Incremental.CacheMissCount > 0);
        if (disablePagesIndex)
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(incrementalSite.Output, ".bukit/incremental-manifest.json")));
            var reasons = manifest.RootElement.GetProperty("renderReasons");
            Assert.True(edited.Incremental.CacheHitCount > 0, $"Edited build did not reuse a page: {reasons.GetRawText()}");
            Assert.True(reasons.TryGetProperty("unchanged", out var reused) && reused.GetInt32() > 0);
            Assert.True(reasons.TryGetProperty("content_changed", out var changed) && changed.GetInt32() > 0);
            Assert.NotNull(renderer);
            Assert.Contains("/another-post/", renderer.RenderedPages);
            Assert.DoesNotContain("/index/", renderer.RenderedPages);
        }
        var actual = OutputSnapshot.Public(incrementalSite.Output);
        Assert.False(initial.Entries["another-post/index.html"].Bytes.AsSpan()
            .SequenceEqual(actual.Entries["another-post/index.html"].Bytes));
        if (disablePagesIndex) Assert.Equal(initial.Entries["index/index.html"].Bytes, actual.Entries["index/index.html"].Bytes);

        using var fullSite = new SiteBuilder("taxonomy-site");
        ConfigurePagesIndex(fullSite, disablePagesIndex);
        Assert.Equal(original, File.ReadAllText(fullSite.Sandbox.PathFor(input)));
        fullSite.Sandbox.Write(input, changedInput);
        var full = await Build(fullSite, clean: true, incremental: false);
        Assert.False(full.Incremental.Enabled);
        Assert.Equal(0, full.Incremental.CacheHitCount);
        Assert.True(full.Incremental.CacheMissCount > 0);
        actual.AssertEqual(OutputSnapshot.Public(fullSite.Output));
    }

    private static void ConfigurePagesIndex(SiteBuilder site, bool disable)
    {
        if (!disable) return;
        // pages-index exposes each body fingerprint as a site-wide render dependency.
        var yaml = File.ReadAllText(site.Sandbox.PathFor("site.yaml"));
        Assert.Contains("site:\n", yaml, StringComparison.Ordinal);
        site.Sandbox.Write("site.yaml", yaml.Replace("site:\n", "site:\n  plugins:\n    pages-index: false\n", StringComparison.Ordinal));
        Assert.False(site.Config.Site.Plugins!["pages-index"].Enabled);
    }

    private static async Task<BuildResult> Build(SiteBuilder site, bool clean, bool incremental,
        Func<string, ITemplateRenderer>? rendererFactory = null)
    {
        var priorOutputMarker = !clean && File.Exists(Path.Combine(site.Output, ".bukit-output-marker"));
        var started = DateTimeOffset.UtcNow;
        var result = await site.BuildAsync(clean: clean, incremental: incremental, rendererFactory: rendererFactory);
        var ended = DateTimeOffset.UtcNow;
        ReportAssertions.Generated(site, result, started, ended, priorOutputMarker, expectedIncremental: incremental);
        return result;
    }

    private sealed class RecordingRenderer(string layouts) : ITemplateRenderer
    {
        private readonly ITemplateRenderer _inner = new ScribanTemplateRendererAdapter(layouts);
        public HashSet<string> RenderedPages { get; } = new(StringComparer.Ordinal);

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            RenderedPages.Add(model.Page.Url);
            return _inner.RenderPage(templateRelativePath, model);
        }

        public string RenderList(string templateRelativePath, ListPageModel model)
            => _inner.RenderList(templateRelativePath, model);
    }
}
