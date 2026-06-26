using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Plugin.Notion;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionValidateDatabaseMapInvokeTests : IDisposable
{
    private readonly string _projectRoot;

    public NotionValidateDatabaseMapInvokeTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-plugin-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public void Handler_ValidMap_ReturnsValidationArtifact()
    {
        string mapPath = WriteValidMap();
        PluginInvokeRequest request = CreateRequest(arguments: [mapPath]);

        PluginInvokeResponse response = NotionValidateDatabaseMapCommandHandler.Handle("req-map", request);

        Assert.True(response.Success);
        Assert.Equal(0, response.ExitCode);
        Assert.Equal("database-map-validation", Assert.Single(response.Artifacts).Type);
    }

    [Fact]
    public void Handler_MissingArgument_ReturnsExitCodeTwo()
    {
        PluginInvokeRequest request = CreateRequest(arguments: []);

        PluginInvokeResponse response = NotionValidateDatabaseMapCommandHandler.Handle("req-bad", request);

        Assert.False(response.Success);
        Assert.Equal(2, response.ExitCode);
        Assert.Equal("notion.databaseMapMissingPath", Assert.Single(response.Diagnostics).Code);
    }

    [Fact]
    public void App_InvokeValidateDatabaseMap_ReturnsJsonResponse()
    {
        string mapPath = WriteValidMap();
        PluginInvokeRequest request = CreateRequest(requestId: "req-app", arguments: [mapPath]);
        string input = JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest);

        string json = NotionPluginApp.Handle(input);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal("invokeResponse", root.GetProperty("type").GetString());
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("database-map-validation", root.GetProperty("artifacts")[0].GetProperty("type").GetString());
    }

    private PluginInvokeRequest CreateRequest(
        string requestId = "req-1",
        IReadOnlyList<string>? path = null,
        IReadOnlyList<string>? arguments = null)
        => new(
            Type: PluginProtocolConstants.Invoke,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Host: new PluginHostInfo("Bukit", "1.0.0", "test-rid"),
            Command: new PluginInvokeCommand(
                Name: "notion",
                Path: path ?? ["notion", "validate-database-map"],
                Arguments: arguments ?? [Path.Combine(_projectRoot, "notion-database-map.yaml")],
                Options: new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
            Context: new PluginInvokeContext(_projectRoot, _projectRoot),
            Permissions: new PluginPermissionSet());

    private string WriteValidMap()
    {
        string path = Path.Combine(_projectRoot, "notion-database-map.yaml");
        File.WriteAllText(path, """
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds
    uniqueField: Slug
""");
        return path;
    }
}
