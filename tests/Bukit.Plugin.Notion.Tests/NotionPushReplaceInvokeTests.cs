using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionPushReplaceInvokeTests : IDisposable
{
    private readonly string _projectRoot;

    public NotionPushReplaceInvokeTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-plugin-replace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NOTION_TOKEN", null);
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public void App_PushReplace_WithoutConfirmFailsBeforeTokenRead()
    {
        Environment.SetEnvironmentVariable("NOTION_TOKEN", null);
        (string seedDir, string mapPath) = WriteValidHandoff();
        PluginInvokeRequest request = CreatePushReplaceRequest(seedDir, mapPath);

        string json = NotionPluginApp.Handle(JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("notion.replaceRequiresConfirmation", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void App_PushReplace_WithConfirmAndMissingTokenFails()
    {
        Environment.SetEnvironmentVariable("NOTION_TOKEN", null);
        (string seedDir, string mapPath) = WriteValidHandoff();
        PluginInvokeRequest request = CreatePushReplaceRequest(seedDir, mapPath, confirmReplace: true);

        string json = NotionPluginApp.Handle(JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest));

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(2, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("notion.tokenMissing", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    private (string SeedDir, string MapPath) WriteValidHandoff()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "notion-seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "slug": "home",
    "published": true,
    "content": "New body"
  }
]
""");
        string mapPath = Path.Combine(seedDir, "notion-database-map.yaml");
        File.WriteAllText(mapPath, """
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds-pages
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
      Published:
        source: published
        type: checkbox
""");
        return (seedDir, mapPath);
    }

    private PluginInvokeRequest CreatePushReplaceRequest(string seedDir, string mapPath, bool confirmReplace = false)
    {
        var options = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["--seed"] = JsonSerializer.SerializeToElement(seedDir),
            ["--database-map"] = JsonSerializer.SerializeToElement(mapPath),
            ["--mode"] = JsonSerializer.SerializeToElement("replace"),
            ["--token-env"] = JsonSerializer.SerializeToElement("NOTION_TOKEN")
        };
        if (confirmReplace)
        {
            options["--confirm-replace"] = JsonSerializer.SerializeToElement(true);
        }

        return new PluginInvokeRequest(
            Type: PluginProtocolConstants.Invoke,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-push-replace",
            Host: new PluginHostInfo("Bukit", "1.0.0", "test-rid"),
            Command: new PluginInvokeCommand(
                Name: "notion",
                Path: ["notion", "push"],
                Arguments: [],
                Options: options),
            Context: new PluginInvokeContext(_projectRoot, _projectRoot),
            Permissions: new PluginPermissionSet());
    }
}
