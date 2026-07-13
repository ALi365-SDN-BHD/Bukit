using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncInputLoaderTests : IDisposable
{
    private readonly string _rootDir;

    public WechatSyncInputLoaderTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-sync-loader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_RejectsJsonRepresentationEscapingOutputDirectory()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(outputDir);
        WriteManifest(outputDir, "../secret.json", "/posts/hello/");
        File.WriteAllText(Path.Combine(_rootDir, "secret.json"), ContentJson(body: "<p>secret</p>", route: "/posts/hello/"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LoadAsync(outputDir));

        Assert.Contains("content json", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("build output", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_RejectsRenderedHtmlRouteEscapingOutputDirectory()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(Path.Combine(outputDir, "content"));
        WriteManifest(outputDir, "content/post-1.json", "/posts/hello/");
        File.WriteAllText(Path.Combine(outputDir, "content", "post-1.json"), ContentJson(body: null, route: "../secret.html"));
        File.WriteAllText(Path.Combine(_rootDir, "secret.html"), "<main>secret</main>");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LoadAsync(outputDir));

        Assert.Contains("rendered html", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("build output", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_UsesHtmlRepresentationAndStripsBaseUrlForRenderedHtmlFallback()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(Path.Combine(outputDir, "content"));
        Directory.CreateDirectory(Path.Combine(outputDir, "blog", "hello"));
        WriteManifest(outputDir, "content/post-1.json", "/docs/blog/hello/", "/docs/blog/hello/");
        File.WriteAllText(Path.Combine(outputDir, "content", "post-1.json"), ContentJson(body: null, route: "/docs/blog/hello/"));
        File.WriteAllText(Path.Combine(outputDir, "blog", "hello", "index.html"), "<main>rendered html</main>");

        var context = await LoadAsync(outputDir, baseUrl: "/docs");

        var (item, route) = Assert.Single(context.Routed);
        Assert.Equal("<main>rendered html</main>", item.ContentHtml);
        Assert.Equal("blog/hello/index.html", route.OutputPath.Replace('\\', '/'));
    }

    [Fact]
    public async Task LoadAsync_PublicProjectionWithoutProviderSource_UsesCollectionAsSyncSource()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(Path.Combine(outputDir, "content"));
        WriteManifest(outputDir, "content/post-1.json", "/posts/hello/");
        File.WriteAllText(Path.Combine(outputDir, "content", "post-1.json"), ContentJson(body: "<p>Hello</p>", route: "/posts/hello/"));

        var context = await LoadAsync(outputDir);

        var (item, _) = Assert.Single(context.Routed);
        Assert.Equal("posts", item.Metadata["sourceKey"]);
        Assert.Equal("posts", item.Metadata["source"]);
        Assert.Equal("post-1", item.Metadata["sourceId"]);
    }

    [Fact]
    public async Task LoadAsync_IgnoresExternalHtmlRepresentationForRenderedHtmlFallback()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(Path.Combine(outputDir, "content"));
        Directory.CreateDirectory(Path.Combine(outputDir, "blog", "hello"));
        WriteManifest(outputDir, "content/post-1.json", "/docs/fallback/", "https://evil.example/docs/blog/hello/");
        File.WriteAllText(Path.Combine(outputDir, "content", "post-1.json"), ContentJson(body: null, route: "/docs/fallback/"));
        File.WriteAllText(Path.Combine(outputDir, "blog", "hello", "index.html"), "<main>external html</main>");

        var context = await LoadAsync(outputDir, baseUrl: "/docs");

        var (item, route) = Assert.Single(context.Routed);
        Assert.Null(item.ContentHtml);
        Assert.Equal("fallback/index.html", route.OutputPath.Replace('\\', '/'));
    }

    [Fact]
    public async Task LoadAsync_RejectsRenderedHtmlSymlinkEscapingOutputDirectory()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(Path.Combine(outputDir, "content"));
        Directory.CreateDirectory(Path.Combine(outputDir, "posts", "hello"));
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-loader-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            WriteManifest(outputDir, "content/post-1.json", "/posts/hello/");
            File.WriteAllText(Path.Combine(outputDir, "content", "post-1.json"), ContentJson(body: null, route: "/posts/hello/"));
            var outsideHtml = Path.Combine(outsideDir, "secret.html");
            File.WriteAllText(outsideHtml, "<main>secret</main>");
            File.CreateSymbolicLink(Path.Combine(outputDir, "posts", "hello", "index.html"), outsideHtml);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LoadAsync(outputDir));

            Assert.Contains("rendered html", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("build output", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_RejectsDefaultManifestSymlinkEscapingOutputDirectory()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(outputDir);
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-wechat-manifest-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            var outsideManifest = Path.Combine(outsideDir, "agent-manifest.json");
            File.WriteAllText(outsideManifest, """
{
  "schema": "bukit.agent-manifest",
  "schemaVersion": "1.0.0",
  "generatedAt": "2026-01-01T00:00:00Z",
  "documents": []
}
""");
            File.CreateSymbolicLink(Path.Combine(outputDir, "agent-manifest.json"), outsideManifest);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LoadAsync(outputDir));

            Assert.Contains("agent manifest", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("build output", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    private Task<WechatSyncContext> LoadAsync(string outputDir, string baseUrl = "/")
        => WechatSyncInputLoader.LoadAsync(
            _rootDir,
            outputDir,
            null,
            "Bukit",
            "https://example.com",
            baseUrl,
            null,
            new ConsoleLogger(LogLevel.Error));

    private static void WriteManifest(string outputDir, string jsonUrl, string route, string? htmlUrl = null)
    {
        var htmlRepresentation = htmlUrl is null
            ? string.Empty
            : ",\n        { \"kind\": \"html\", \"url\": \"" + htmlUrl + "\" }";
        File.WriteAllText(Path.Combine(outputDir, "agent-manifest.json"), $$"""
{
  "schema": "bukit.agent-manifest",
  "schemaVersion": "1.0.0",
  "generatedAt": "2026-01-01T00:00:00Z",
  "documents": [
    {
      "id": "post-1",
      "canonicalId": "post-1",
      "route": "{{route}}",
      "language": "zh",
      "reviewStatus": "approved",
      "entities": [],
      "representations": [
        { "kind": "json", "url": "{{jsonUrl}}" }{{htmlRepresentation}}
      ],
      "publishedAt": "2026-01-01T00:00:00Z"
    }
  ]
}
""");
    }

    private static string ContentJson(string? body, string route)
        => $$"""
{
  "id": "post-1",
  "slug": "hello",
  "canonicalUrlKey": "hello",
  "route": "{{route}}",
  "title": "Hello",
  "summary": "Summary",
  "body": {{(body is null ? "null" : "\"" + body + "\"")}},
  "language": "zh",
  "type": "post",
  "collection": "posts",
  "tags": [],
  "sections": [],
  "author": "Ali",
  "organization": null,
  "publishedAt": "2026-01-01T00:00:00Z",
  "updatedAt": null,
  "expiresAt": null,
  "reviewedAt": null,
  "originalSource": null,
  "citations": [],
  "references": [],
  "syncStatus": null,
  "reviewStatus": "approved",
  "credibilityScore": null,
  "qualityFlags": [],
  "media": [],
  "canonical": null
}
""";
}
