using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.WechatSync;

public static class WechatSyncPluginApp
{
    public static string Handle(string input)
        => HandleAsync(input).GetAwaiter().GetResult();

    public static async Task<string> HandleAsync(string input)
    {
        using JsonDocument document = JsonDocument.Parse(input);
        JsonElement root = document.RootElement;
        string requestId = root.TryGetProperty("requestId", out JsonElement requestIdElement)
            ? requestIdElement.GetString() ?? "unknown"
            : "unknown";
        string type = root.TryGetProperty("type", out JsonElement typeElement)
            ? typeElement.GetString() ?? string.Empty
            : string.Empty;

        return type switch
        {
            PluginProtocolConstants.Handshake => Serialize(WechatSyncPluginManifestProvider.CreateHandshakeResponse(requestId, ReadHostPlatform(root))),
            PluginProtocolConstants.Manifest => Serialize(WechatSyncPluginManifestProvider.CreateManifestResponse(requestId)),
            PluginProtocolConstants.Invoke => Serialize(await WechatSyncPluginInvoker.InvokeAsync(DeserializeInvoke(input))),
            _ => Serialize(new PluginResponseEnvelope(
                Type: "errorResponse",
                Protocol: PluginProtocolConstants.ProtocolVersion,
                RequestId: requestId,
                Success: false,
                Error: new PluginError("plugin.wechat-sync.unknownRequest", $"Unsupported request type: {type}")))
        };
    }

    private static string ReadHostPlatform(JsonElement root)
        => root.TryGetProperty("host", out JsonElement host)
            && host.TryGetProperty("platform", out JsonElement platform)
                ? platform.GetString() ?? string.Empty
                : string.Empty;

    private static string Serialize(PluginHandshakeResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginHandshakeResponse);

    private static string Serialize(PluginManifestResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginManifestResponse);

    private static string Serialize(PluginInvokeResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginInvokeResponse);

    private static string Serialize(PluginResponseEnvelope response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginResponseEnvelope);

    private static PluginInvokeRequest DeserializeInvoke(string input)
        => JsonSerializer.Deserialize(input, PluginJsonSerializerContext.Default.PluginInvokeRequest)
           ?? throw new JsonException("Invoke request was null.");
}
