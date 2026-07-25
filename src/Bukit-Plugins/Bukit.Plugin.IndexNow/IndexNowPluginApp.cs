using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.IndexNow;

public static class IndexNowPluginApp
{
    public static string Handle(string input)
        => HandleAsync(input).GetAwaiter().GetResult();

    public static async Task<string> HandleAsync(string input)
    {
        using var document = JsonDocument.Parse(input);
        var root = document.RootElement;
        var requestId = root.TryGetProperty("requestId", out var requestIdValue)
            ? requestIdValue.GetString() ?? "unknown"
            : "unknown";
        var type = root.TryGetProperty("type", out var typeValue)
            ? typeValue.GetString() ?? string.Empty
            : string.Empty;

        return type switch
        {
            PluginProtocolConstants.Handshake => Serialize(
                IndexNowPluginManifestProvider.CreateHandshakeResponse(requestId, ReadPlatform(root))),
            PluginProtocolConstants.Manifest => Serialize(
                IndexNowPluginManifestProvider.CreateManifestResponse(requestId)),
            PluginProtocolConstants.Invoke => Serialize(
                await IndexNowPluginInvoker.InvokeAsync(DeserializeInvoke(input))),
            _ => Serialize(new PluginResponseEnvelope(
                "errorResponse",
                PluginProtocolConstants.ProtocolVersion,
                requestId,
                false,
                new PluginError("plugin.indexnow.unknownRequest", $"Unsupported request type: {type}")))
        };
    }

    private static string ReadPlatform(JsonElement root)
        => root.TryGetProperty("host", out var host) &&
           host.TryGetProperty("platform", out var platform)
            ? platform.GetString() ?? string.Empty
            : string.Empty;

    private static PluginInvokeRequest DeserializeInvoke(string input)
        => JsonSerializer.Deserialize(input, PluginJsonSerializerContext.Default.PluginInvokeRequest)
           ?? throw new JsonException("Invoke request was null.");

    private static string Serialize(PluginHandshakeResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginHandshakeResponse);

    private static string Serialize(PluginManifestResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginManifestResponse);

    private static string Serialize(PluginInvokeResponse response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginInvokeResponse);

    private static string Serialize(PluginResponseEnvelope response)
        => JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginResponseEnvelope);
}
