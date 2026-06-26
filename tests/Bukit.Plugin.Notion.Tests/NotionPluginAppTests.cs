using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Notion;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionPluginAppTests
{
    [Fact]
    public void Handle_Handshake_ReturnsJsonHandshakeResponse()
    {
        string response = NotionPluginApp.Handle(
            """
            {"type":"handshake","protocol":"bukit-plugin-v1","requestId":"req-handshake","host":{"platform":"test-platform"}}
            """);

        using JsonDocument document = JsonDocument.Parse(response);
        JsonElement root = document.RootElement;

        Assert.Equal("handshakeResponse", root.GetProperty("type").GetString());
        Assert.Equal("req-handshake", root.GetProperty("requestId").GetString());
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("notion", root.GetProperty("plugin").GetProperty("id").GetString());
        Assert.Equal("test-platform", root.GetProperty("plugin").GetProperty("platform").GetString());
    }

    [Fact]
    public void Handle_Manifest_ReturnsJsonManifestResponse()
    {
        string response = NotionPluginApp.Handle(
            """
            {"type":"manifest","protocol":"bukit-plugin-v1","requestId":"req-manifest"}
            """);

        using JsonDocument document = JsonDocument.Parse(response);
        JsonElement root = document.RootElement;

        Assert.Equal("manifestResponse", root.GetProperty("type").GetString());
        Assert.Equal("req-manifest", root.GetProperty("requestId").GetString());
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("notion", root.GetProperty("commands")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Handle_UnknownInvoke_ReturnsJsonInvokeResponse()
    {
        var request = new PluginInvokeRequest(
            Type: "invoke",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-invoke",
            Host: new PluginHostInfo(
                Name: "bukit",
                Version: "test",
                Platform: "test-platform"),
            Command: new PluginInvokeCommand(
                Name: "notion",
                Path: ["notion", "unknown"],
                Arguments: [],
                Options: new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
            Context: new PluginInvokeContext(
                RootDir: "/repo",
                WorkingDir: "/repo"),
            Permissions: new());
        string input = JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest);

        string response = NotionPluginApp.Handle(input);

        using JsonDocument document = JsonDocument.Parse(response);
        JsonElement root = document.RootElement;

        Assert.Equal("invokeResponse", root.GetProperty("type").GetString());
        Assert.Equal("req-invoke", root.GetProperty("requestId").GetString());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("plugin.notion.unsupportedCommand", root.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }
}
