using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

// stabilize-render-dependency-inputs: signed links left in shared render data change on every
// fetch. The hash keeps them (skipping would keep an expired link online); a warning names them.
public sealed class SignedLinkDiagnosticsTests
{
    private const string AwsSigned =
        "https://bucket.s3.amazonaws.com/a.png?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Date=20261110T000000Z&X-Amz-Expires=3600&X-Amz-Signature=";

    [Theory]
    [InlineData(AwsSigned + "aaa", 1)]
    [InlineData("<img src=\"" + "https://bucket.s3.amazonaws.com/a.png?X-Amz-Algorithm=AWS4-HMAC-SHA256&amp;X-Amz-Signature=aaa" + "\">", 1)]
    [InlineData("https://bucket.s3.amazonaws.com/a.png?X-Amz-Algorithm=AWS4-HMAC-SHA256&#38;X-Amz-Signature=aaa", 1)]
    [InlineData("https://bucket.s3.amazonaws.com/a.png?X-Amz-Algorithm=AWS4-HMAC-SHA256&#x26;X-Amz-Signature=aaa", 1)]
    [InlineData("https://file.notion.so/f/x.png?expirationTimestamp=1&signature=abc and " + AwsSigned + "b", 2)]
    [InlineData("https://example.com/a.png?signature=abc", 0)]
    [InlineData("https://example.com/a.png?v=1", 0)]
    [InlineData("no links, just the word signature", 0)]
    public void CountInText_CountsOnlyLinksOfARecognizedSigningScheme(string text, int expected)
    {
        Assert.Equal(expected, SignedLinkDiagnostics.CountInText(text));
    }

    [Fact]
    public async Task SameInputTwice_KeepsEveryDependencyHashAndWarnsNothing()
    {
        using var site = CreateSite("https://example.com/menu.png");
        var first = new RecordingLogger();
        await BuildAsync(site, first);
        var hashesBefore = DependencyHashes(site);

        var second = new RecordingLogger();
        var result = await BuildAsync(site, second);

        Assert.Equal(0, result.Incremental.CacheMissCount);
        Assert.Equal(hashesBefore, DependencyHashes(site));
        Assert.DoesNotContain(first.Warnings.Concat(second.Warnings), message => message.Contains("incremental.signed_links", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResignedLinkInSharedData_WarnsAndRendersReadersAgain()
    {
        using var site = CreateSite(AwsSigned + "first");
        var logger = new RecordingLogger();
        await BuildAsync(site, logger);
        var warning = Assert.Single(logger.Warnings, message => message.Contains("event=incremental.signed_links", StringComparison.Ordinal));
        Assert.Contains("keys=site.data_files:1", warning, StringComparison.Ordinal);
        Assert.Contains("links=1", warning, StringComparison.Ordinal);

        // Notion re-signs the link on the next fetch: the reader renders again with the new link.
        site.Sandbox.Write("data/menu.json", JsonSerializer.Serialize(new { image = AwsSigned + "second" }));
        var result = await BuildAsync(site, new RecordingLogger());

        var reasons = RenderReasons(site);
        Assert.Equal(1, reasons["data_dependency_changed"]);
        Assert.False(reasons.ContainsKey("render_dependency_changed"));
        Assert.Equal(1, reasons["unchanged"]);
        Assert.True(result.Incremental.CacheMissCount >= 1);
        Assert.Contains("X-Amz-Signature=second", File.ReadAllText(Path.Combine(site.Output, "m", "index.html")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedLinkInPageField_IsReportedUnderPages()
    {
        using var site = CreateSite("https://example.com/menu.png");
        site.Sandbox.Write("content/c.md", Article("c", "C", $"cover: \"{AwsSigned}c\""));
        var logger = new RecordingLogger();
        await BuildAsync(site, logger);

        var warning = Assert.Single(logger.Warnings, message => message.Contains("event=incremental.signed_links", StringComparison.Ordinal));
        Assert.Contains("keys=pages:1", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("language=", warning, StringComparison.Ordinal);
        Assert.Contains("may change on every fetch", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MultiLanguageBuild_ReportsSharedSignedLinksOnceForAllLanguages()
    {
        using var site = new SiteBuilder("i18n-site");
        site.Sandbox.Write("data/menu.json", JsonSerializer.Serialize(new { image = AwsSigned + "shared" }));
        var logger = new RecordingLogger();

        await BuildAsync(site, logger);

        var warning = Assert.Single(logger.Warnings, message => message.Contains("event=incremental.signed_links", StringComparison.Ordinal));
        Assert.Contains(" language=en,zh-CN ", warning, StringComparison.Ordinal);
        Assert.Contains("keys=site.data_files:1", warning, StringComparison.Ordinal);
    }

    private static SiteBuilder CreateSite(string image)
    {
        var site = new SiteBuilder();
        site.Sandbox.Write("layouts/pages/page.html", """
            <html><head><title>{{ page.title }}</title></head><body>
            {{ if page.fields.menu }}{{ include "partials/menu.html" }}{{ end }}
            {{ page.title }}{{ page.content }}</body></html>
            """);
        site.Sandbox.Write("layouts/partials/menu.html", "<img src=\"{{ site.data_files.menu.image }}\">");
        site.Sandbox.Write("data/menu.json", JsonSerializer.Serialize(new { image }));
        site.Sandbox.Write("content/a.md", Article("a", "A"));
        site.Sandbox.Write("content/m.md", Article("m", "M", "menu: true"));
        return site;
    }

    private static string Article(string slug, string title, string? extra = null) =>
        $"---\nslug: {slug}\ntitle: {title}\npublishAt: 2024-06-01T00:00:00Z\ncollection: page\n{(extra is null ? string.Empty : extra + "\n")}---\nBody {slug}\n";

    private static Task<BuildResult> BuildAsync(SiteBuilder site, ILogger logger) =>
        new SiteEngine(logger).BuildAsync(site.Config, site.Root, new ConfigOverrides { Clean = false, Incremental = true });

    private static Dictionary<string, int> RenderReasons(SiteBuilder site)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(site.Output, ".bukit", "incremental-manifest.json")));
        return report.RootElement.GetProperty("renderReasons").EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetInt32(), StringComparer.Ordinal);
    }

    private static Dictionary<string, string> DependencyHashes(SiteBuilder site) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(site.Cache, "build-manifest.json")))!["entries"]!.AsObject()
            .ToDictionary(entry => entry.Key, entry => entry.Value!["renderDependencyHash"]!.GetValue<string>(), StringComparer.Ordinal);

    private sealed class RecordingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _warnings = new();

        public IReadOnlyCollection<string> Warnings => _warnings;

        public void Debug(string message) { }

        public void Info(string message) { }

        public void Warn(string message) => _warnings.Enqueue(message);

        public void Error(string message) { }
    }
}
