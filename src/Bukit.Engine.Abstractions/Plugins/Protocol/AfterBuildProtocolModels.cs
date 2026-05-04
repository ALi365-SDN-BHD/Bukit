using System.Text.Json.Serialization;

namespace Bukit.Engine.Plugins.Protocol;

public sealed record AfterBuildRequestPayload
{
    [JsonPropertyName("outputDir")]
    public required string OutputDir { get; init; }
    [JsonPropertyName("routedPages")]
    public IReadOnlyList<AfterBuildRoutedPage> RoutedPages { get; init; } = Array.Empty<AfterBuildRoutedPage>();
}

public sealed record AfterBuildRoutedPage
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("url")]
    public required string Url { get; init; }
    [JsonPropertyName("outputPath")]
    public required string OutputPath { get; init; }
    [JsonPropertyName("meta")]
    public IReadOnlyDictionary<string, object>? Meta { get; init; }
}

public sealed record AfterBuildOutputFile
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }
    [JsonPropertyName("contentType")]
    public required string ContentType { get; init; }
    [JsonPropertyName("text")]
    public string? Text { get; init; }
    [JsonPropertyName("base64")]
    public string? Base64 { get; init; }
}
