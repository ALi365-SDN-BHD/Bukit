using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Import;

public static class ImportPluginApp
{
    public static string Handle(string input)
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
            PluginProtocolConstants.Handshake => Serialize(ImportPluginManifestProvider.CreateHandshakeResponse(requestId, ReadHostPlatform(root))),
            PluginProtocolConstants.Manifest => Serialize(ImportPluginManifestProvider.CreateManifestResponse(requestId)),
            PluginProtocolConstants.Invoke => Serialize(ImportPluginInvoker.Invoke(ReadInvokeRequest(root))),
            _ => Serialize(new PluginResponseEnvelope(
                Type: "errorResponse",
                Protocol: PluginProtocolConstants.ProtocolVersion,
                RequestId: requestId,
                Success: false,
                Error: new PluginError("plugin.import.unknownRequest", $"Unsupported request type: {type}")))
        };
    }

    private static string ReadHostPlatform(JsonElement root)
        => root.TryGetProperty("host", out JsonElement host)
            && host.TryGetProperty("platform", out JsonElement platform)
                ? platform.GetString() ?? string.Empty
                : string.Empty;

    private static PluginInvokeRequest ReadInvokeRequest(JsonElement root)
        => JsonSerializer.Deserialize(root.GetRawText(), PluginJsonSerializerContext.Default.PluginInvokeRequest)
            ?? throw new InvalidOperationException("Invalid invoke request.");

    private static string Serialize(PluginHandshakeResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginHandshakeResponse);

    private static string Serialize(PluginManifestResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginManifestResponse);

    private static string Serialize(PluginInvokeResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginInvokeResponse);

    private static string Serialize(PluginResponseEnvelope response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginResponseEnvelope);
}
