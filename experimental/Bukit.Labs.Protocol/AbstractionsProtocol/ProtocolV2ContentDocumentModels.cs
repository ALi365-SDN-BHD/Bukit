using System.Text.Json.Serialization;

namespace Bukit.Engine.Abstractions.Plugins.Protocol;

public sealed record PluginContentDocumentDto
{
    [JsonPropertyName("content")]
    public required ContentRecordDto Content { get; init; }
    [JsonPropertyName("route")]
    public required ContentRoutePolicyDto Route { get; init; }
    [JsonPropertyName("publish")]
    public required ContentPublishPolicyDto Publish { get; init; }
    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, ContentFieldDto> Fields { get; init; } =
        new Dictionary<string, ContentFieldDto>(StringComparer.OrdinalIgnoreCase);
    [JsonPropertyName("source")]
    public required ContentSourceInfoDto Source { get; init; }
}

public sealed record ContentRecordDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    [JsonPropertyName("collection")]
    public required string Collection { get; init; }
    [JsonPropertyName("language")]
    public required string Language { get; init; }
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }
}

public sealed record ContentRoutePolicyDto
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }
    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; init; }
    [JsonPropertyName("template")]
    public string? Template { get; init; }
    [JsonPropertyName("permalinkPattern")]
    public string? PermalinkPattern { get; init; }
    [JsonPropertyName("listGroup")]
    public string? ListGroup { get; init; }
}

public sealed record ContentPublishPolicyDto
{
    [JsonPropertyName("draft")]
    public bool Draft { get; init; }
    [JsonPropertyName("noIndex")]
    public bool NoIndex { get; init; }
    [JsonPropertyName("noFollow")]
    public bool NoFollow { get; init; }
    [JsonPropertyName("excludeFromFeed")]
    public bool ExcludeFromFeed { get; init; }
    [JsonPropertyName("excludeFromSearch")]
    public bool ExcludeFromSearch { get; init; }
    [JsonPropertyName("excludeFromSitemap")]
    public bool ExcludeFromSitemap { get; init; }
    [JsonPropertyName("isDataModule")]
    public bool IsDataModule { get; init; }
}

public sealed record ContentFieldDto
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }
    [JsonPropertyName("value")]
    public object? Value { get; init; }
}

public sealed record ContentSourceInfoDto
{
    [JsonPropertyName("provider")]
    public required string Provider { get; init; }
    [JsonPropertyName("sourceKey")]
    public string? SourceKey { get; init; }
    [JsonPropertyName("sourcePath")]
    public string? SourcePath { get; init; }
    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }
    [JsonPropertyName("externalUrl")]
    public string? ExternalUrl { get; init; }
    [JsonPropertyName("syncedAt")]
    public DateTimeOffset? SyncedAt { get; init; }
    [JsonPropertyName("syncStatus")]
    public string? SyncStatus { get; init; }
}
