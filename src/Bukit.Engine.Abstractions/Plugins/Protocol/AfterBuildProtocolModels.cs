using System.Text.Json.Serialization;

namespace Bukit.Engine.Abstractions.Plugins.Protocol;

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
    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, object>? Fields { get; init; }
    [JsonPropertyName("content")]
    public ProtocolContentRecord? Content { get; init; }
}

public sealed record ProtocolContentRecord
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }
    [JsonPropertyName("canonicalUrlKey")]
    public required string CanonicalUrlKey { get; init; }
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    [JsonPropertyName("collection")]
    public required string Collection { get; init; }
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }
    [JsonPropertyName("language")]
    public required string Language { get; init; }
    [JsonPropertyName("translations")]
    public IReadOnlyList<string> Translations { get; init; } = Array.Empty<string>();
    [JsonPropertyName("author")]
    public string? Author { get; init; }
    [JsonPropertyName("organization")]
    public string? Organization { get; init; }
    [JsonPropertyName("owner")]
    public string? Owner { get; init; }
    [JsonPropertyName("reviewer")]
    public string? Reviewer { get; init; }
    [JsonPropertyName("publishedAt")]
    public DateTimeOffset PublishedAt { get; init; }
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; init; }
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }
    [JsonPropertyName("reviewedAt")]
    public DateTimeOffset? ReviewedAt { get; init; }
    [JsonPropertyName("source")]
    public string? Source { get; init; }
    [JsonPropertyName("originalSource")]
    public string? OriginalSource { get; init; }
    [JsonPropertyName("citations")]
    public IReadOnlyList<string> Citations { get; init; } = Array.Empty<string>();
    [JsonPropertyName("references")]
    public IReadOnlyList<string> References { get; init; } = Array.Empty<string>();
    [JsonPropertyName("syncStatus")]
    public string? SyncStatus { get; init; }
    [JsonPropertyName("reviewStatus")]
    public required string ReviewStatus { get; init; }
    [JsonPropertyName("credibilityScore")]
    public double? CredibilityScore { get; init; }
    [JsonPropertyName("qualityFlags")]
    public IReadOnlyList<string> QualityFlags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("entities")]
    public IReadOnlyList<ProtocolEntityRecord> Entities { get; init; } = Array.Empty<ProtocolEntityRecord>();
    [JsonPropertyName("relations")]
    public IReadOnlyList<ProtocolContentRelation> Relations { get; init; } = Array.Empty<ProtocolContentRelation>();
    [JsonPropertyName("media")]
    public IReadOnlyList<ProtocolMediaAsset> Media { get; init; } = Array.Empty<ProtocolMediaAsset>();
}

public sealed record ProtocolEntityRecord(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("sameAs")] IReadOnlyList<string>? SameAs);

public sealed record ProtocolContentRelation(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("targetType")] string? TargetType,
    [property: JsonPropertyName("targetId")] string? TargetId);

public sealed record ProtocolMediaAsset(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("alt")] string? Alt,
    [property: JsonPropertyName("caption")] string? Caption,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("license")] string? License);

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
