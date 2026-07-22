using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Plugin.WechatSync;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncPluginInvokeCompatibilityTests : IDisposable
{
    private readonly string _rootDir;

    public WechatSyncPluginInvokeCompatibilityTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-wechat-sync-invoke-" + Guid.NewGuid().ToString("N"));
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
    public async Task Invoke_SyncWithoutOutput_ReturnsTwo()
    {
        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(new Dictionary<string, JsonElement>()));

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.missingOutput");
    }

    [Fact]
    public async Task Invoke_DryRunLoadsContentProjectionAndReturnsCandidateCount()
    {
        CreateMinimalBuildOutput();

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true)
            }));

        Assert.True(response.Success);
        Assert.Equal(0, response.ExitCode);
        Assert.Contains(response.Messages, message =>
            message.Level == "info" &&
            message.Message.Contains("wechat-sync dry-run: candidates=1 output=dist", StringComparison.Ordinal));
        Assert.Empty(response.Diagnostics);
    }

    [Fact]
    public async Task Invoke_DryRunUsesTheSameSourceFilterAsRealExecution()
    {
        CreateMinimalBuildOutput();

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true),
                ["--source-names"] = Json("missing-source")
            }));

        Assert.True(response.Success);
        Assert.Contains(response.Messages, message =>
            message.Message.Contains("candidates=0", StringComparison.Ordinal));
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.sourceDenied" &&
            diagnostic.Severity == "info");
    }

    [Fact]
    public async Task Invoke_DryRunUsesTheSameContentTypeFilterAsRealExecution()
    {
        CreateMinimalBuildOutput();

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true),
                ["--content-types"] = Json("page")
            }));

        Assert.True(response.Success);
        Assert.Contains(response.Messages, message =>
            message.Message.Contains("candidates=0", StringComparison.Ordinal));
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.contentTypeDenied" &&
            diagnostic.Severity == "info");
    }

    [Fact]
    public async Task Invoke_DryRunReportsReviewDenialWithoutCreatingACandidate()
    {
        CreateMinimalBuildOutput();
        ReplaceInFile(
            Path.Combine(_rootDir, "dist", "agent-manifest.json"),
            "\"reviewStatus\": \"approved\"",
            "\"reviewStatus\": \"needs-review\"");
        ReplaceInFile(
            Path.Combine(_rootDir, "dist", "content", "post-1.json"),
            "\"reviewStatus\": \"approved\"",
            "\"reviewStatus\": \"needs-review\"");

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true)
            }));

        Assert.True(response.Success);
        Assert.Contains(response.Messages, message =>
            message.Message.Contains("candidates=0", StringComparison.Ordinal));
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.reviewStatusDenied" &&
            diagnostic.Severity == "warning");
    }

    [Fact]
    public async Task Invoke_DryRunExcludesExpiredContent()
    {
        CreateMinimalBuildOutput();
        ReplaceInFile(
            Path.Combine(_rootDir, "dist", "content", "post-1.json"),
            "\"expiresAt\": null",
            "\"expiresAt\": \"2026-01-01T00:00:00Z\"");

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true)
            }));

        Assert.True(response.Success);
        Assert.Contains(response.Messages, message =>
            message.Message.Contains("candidates=0", StringComparison.Ordinal));
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.contentExpired" &&
            diagnostic.Severity == "warning");
    }

    [Fact]
    public async Task Invoke_DryRunFailsClosedWhenReviewStatusesMismatch()
    {
        CreateMinimalBuildOutput();
        ReplaceInFile(
            Path.Combine(_rootDir, "dist", "content", "post-1.json"),
            "\"reviewStatus\": \"approved\"",
            "\"reviewStatus\": \"verified\"");

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true)
            }));

        Assert.False(response.Success);
        Assert.Equal(1, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.reviewStatusMismatch" &&
            diagnostic.Severity == "error");
    }

    [Fact]
    public async Task Invoke_DryRunFailsClosedWhenReviewStatusIsMissing()
    {
        CreateMinimalBuildOutput();
        ReplaceInFile(
            Path.Combine(_rootDir, "dist", "agent-manifest.json"),
            "\"reviewStatus\": \"approved\"",
            "\"reviewStatus\": \"\"");

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true)
            }));

        Assert.False(response.Success);
        Assert.Equal(1, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.reviewStatusMissing" &&
            diagnostic.Severity == "error");
    }

    [Fact]
    public async Task Invoke_DryRunReportsZeroEffectiveCandidatesWhenAnySelectedItemHasAnError()
    {
        CreateMinimalBuildOutput();
        AddMismatchedSecondBuildItem();

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true)
            }));

        Assert.False(response.Success);
        Assert.Equal(1, response.ExitCode);
        Assert.Contains(response.Messages, message =>
            message.Message.Contains("candidates=0", StringComparison.Ordinal));
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.reviewStatusMismatch" &&
            diagnostic.Severity == "error");
    }

    [Fact]
    public async Task Invoke_DryRunReturnsFailureWhenManifestIsMissing()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "dist"));

        var response = await WechatSyncPluginInvoker.InvokeAsync(Request(
            new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true)
            }));

        Assert.False(response.Success);
        Assert.Equal(1, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic =>
            diagnostic.Code == "plugin.wechat-sync.failed" &&
            diagnostic.Message.Contains("requires agent-manifest.json", StringComparison.OrdinalIgnoreCase));
    }

    private PluginInvokeRequest Request(IReadOnlyDictionary<string, JsonElement> options)
        => new(
            Type: "invoke",
            Protocol: "bukit-plugin-v1",
            RequestId: "req",
            Host: new PluginHostInfo("Bukit", "1.0.0", "test"),
            Command: new PluginInvokeCommand("sync", Path: ["wechat-sync", "sync"], Options: options),
            Context: new PluginInvokeContext(_rootDir, _rootDir),
            Permissions: new PluginPermissionSet());

    private void CreateMinimalBuildOutput()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "dist", "content"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "dist", "posts", "hello"));
        File.WriteAllText(Path.Combine(_rootDir, "dist", "agent-manifest.json"), """
{
  "schema": "bukit.agent-manifest",
  "schemaVersion": "1.0.0",
  "generatedAt": "2026-01-01T00:00:00Z",
  "documents": [
    {
      "id": "post-1",
      "canonicalId": "post-1",
      "route": "/posts/hello/",
      "language": "zh",
      "reviewStatus": "approved",
      "entities": [],
      "representations": [
        { "kind": "json", "url": "content/post-1.json" },
        { "kind": "html", "url": "posts/hello/index.html" }
      ],
      "publishedAt": "2026-01-01T00:00:00Z"
    }
  ]
}
""");
        File.WriteAllText(Path.Combine(_rootDir, "dist", "content", "post-1.json"), """
{
  "id": "post-1",
  "slug": "hello",
  "canonicalUrlKey": "hello",
  "route": "/posts/hello/",
  "title": "Hello",
  "summary": "Summary",
  "body": "<p>Hello</p>",
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
""");
        File.WriteAllText(Path.Combine(_rootDir, "dist", "posts", "hello", "index.html"), "<main><p>Hello</p></main>");
    }

    private static JsonElement Json(string value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement Json(bool value)
        => JsonSerializer.SerializeToElement(value);

    private static void ReplaceInFile(string path, string oldValue, string newValue)
        => File.WriteAllText(path, File.ReadAllText(path).Replace(oldValue, newValue, StringComparison.Ordinal));

    private void AddMismatchedSecondBuildItem()
    {
        var manifestPath = Path.Combine(_rootDir, "dist", "agent-manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var document = manifest["documents"]!.AsArray()[0]!.DeepClone().AsObject();
        document["id"] = "post-2";
        document["canonicalId"] = "post-2";
        document["route"] = "/posts/second/";
        var representations = document["representations"]!.AsArray();
        representations[0]!["url"] = "content/post-2.json";
        representations[1]!["url"] = "posts/second/index.html";
        manifest["documents"]!.AsArray().Add(document);
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var firstContentPath = Path.Combine(_rootDir, "dist", "content", "post-1.json");
        var secondContent = JsonNode.Parse(File.ReadAllText(firstContentPath))!.AsObject();
        secondContent["id"] = "post-2";
        secondContent["slug"] = "second";
        secondContent["canonicalUrlKey"] = "second";
        secondContent["route"] = "/posts/second/";
        secondContent["reviewStatus"] = "verified";
        File.WriteAllText(
            Path.Combine(_rootDir, "dist", "content", "post-2.json"),
            secondContent.ToJsonString());

        var htmlDir = Path.Combine(_rootDir, "dist", "posts", "second");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "index.html"), "<main><p>Second</p></main>");
    }
}
