using System.Text.Json.Serialization;

namespace Bukit.Engine.Plugins.Protocol;

public sealed record DerivePagesRequestPayload
{
    [JsonPropertyName("routedPages")]
    public IReadOnlyList<AfterBuildRoutedPage> RoutedPages { get; init; } = Array.Empty<AfterBuildRoutedPage>();
}

public sealed record DerivePagesResponsePayload
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }
    [JsonPropertyName("logs")]
    public IReadOnlyList<ProtocolPluginLogEntry> Logs { get; init; } = Array.Empty<ProtocolPluginLogEntry>();
    [JsonPropertyName("derivedPages")]
    public IReadOnlyList<ProtocolDerivedPage> DerivedPages { get; init; } = Array.Empty<ProtocolDerivedPage>();
    [JsonPropertyName("error")]
    public ProtocolPluginError? Error { get; init; }
}

public sealed record ProtocolDerivedPage
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }
    [JsonPropertyName("publishAt")]
    public DateTimeOffset PublishAt { get; init; }
    [JsonPropertyName("contentHtml")]
    public string? ContentHtml { get; init; }
    [JsonPropertyName("meta")]
    public IReadOnlyDictionary<string, object>? Meta { get; init; }
    [JsonPropertyName("url")]
    public required string Url { get; init; }
    [JsonPropertyName("outputPath")]
    public required string OutputPath { get; init; }
    [JsonPropertyName("template")]
    public required string Template { get; init; }
    [JsonPropertyName("lastModified")]
    public DateTimeOffset? LastModified { get; init; }
}
