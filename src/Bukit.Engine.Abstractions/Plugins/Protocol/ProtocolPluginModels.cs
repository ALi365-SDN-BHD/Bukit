using System.Text.Json.Serialization;

namespace Bukit.Engine.Abstractions.Plugins.Protocol;

public sealed record ProtocolPluginInvocationRequest
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1";
    [JsonPropertyName("hook")]
    public required string Hook { get; init; }
    [JsonPropertyName("plugin")]
    public required ProtocolPluginIdentity Plugin { get; init; }
    [JsonPropertyName("site")]
    public required ProtocolSiteInfo Site { get; init; }
    [JsonPropertyName("config")]
    public ProtocolPluginConfig Config { get; init; } = new();
    [JsonPropertyName("afterBuild")]
    public AfterBuildRequestPayload? AfterBuild { get; init; }
}

public sealed record ProtocolPluginInvocationResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }
    [JsonPropertyName("logs")]
    public IReadOnlyList<ProtocolPluginLogEntry> Logs { get; init; } = Array.Empty<ProtocolPluginLogEntry>();
    [JsonPropertyName("outputs")]
    public IReadOnlyList<AfterBuildOutputFile> Outputs { get; init; } = Array.Empty<AfterBuildOutputFile>();
    [JsonPropertyName("error")]
    public ProtocolPluginError? Error { get; init; }
}

public sealed record ProtocolPluginIdentity
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

public sealed record ProtocolSiteInfo
{
    [JsonPropertyName("baseUrl")]
    public required string BaseUrl { get; init; }
    [JsonPropertyName("language")]
    public required string Language { get; init; }
    [JsonPropertyName("title")]
    public required string Title { get; init; }
}

public sealed record ProtocolPluginConfig
{
    [JsonPropertyName("pluginOptions")]
    public IReadOnlyDictionary<string, object>? PluginOptions { get; init; }
}

public sealed record ProtocolPluginLogEntry
{
    [JsonPropertyName("level")]
    public required string Level { get; init; }
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

public sealed record ProtocolPluginError
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
