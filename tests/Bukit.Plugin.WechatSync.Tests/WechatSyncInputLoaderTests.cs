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

    private Task<WechatSyncContext> LoadAsync(string outputDir)
        => WechatSyncInputLoader.LoadAsync(
            _rootDir,
            outputDir,
            null,
            "Bukit",
            "https://example.com",
            "/",
            null,
            new ConsoleLogger(LogLevel.Error));

    private static void WriteManifest(string outputDir, string jsonUrl, string route)
    {
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
      "source": "notion",
      "entities": [],
      "representations": [
        { "kind": "json", "url": "{{jsonUrl}}" }
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
  "source": "notion",
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
