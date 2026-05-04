using System.Text.Json.Serialization;

namespace Bukit.Engine.Plugins.Protocol;

public sealed record ProtocolHandshakeRequest
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "2";
    [JsonPropertyName("hook")]
    public string Hook { get; init; } = "handshake";
    [JsonPropertyName("requestedHook")]
    public required string RequestedHook { get; init; }
    [JsonPropertyName("hostSupportedSchemaVersions")]
    public IReadOnlyList<string> HostSupportedSchemaVersions { get; init; } = new[] { "2", "1" };
}

public sealed record ProtocolHandshakeResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }
    [JsonPropertyName("negotiatedSchemaVersion")]
    public string? NegotiatedSchemaVersion { get; init; }
    [JsonPropertyName("error")]
    public ProtocolPluginError? Error { get; init; }
}
