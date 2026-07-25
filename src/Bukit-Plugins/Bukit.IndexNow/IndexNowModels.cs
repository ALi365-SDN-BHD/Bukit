using System.Text.Json.Serialization;

namespace Bukit.IndexNow;

public sealed record IndexNowSubmissionRequest(
    string ChangeSetPath,
    string SnapshotPath,
    Uri SiteUrl,
    string StateDir,
    string OutputRoot,
    string? Key,
    bool DryRun);

public sealed record IndexNowSubmissionResult(
    bool Success,
    int DeployedCount,
    int NotifiedCount,
    int PendingCount,
    IReadOnlyList<IndexNowDiagnostic> Diagnostics);

public sealed record IndexNowDiagnostic(string Code, string Severity, string Message, string? Path = null);

public sealed record IndexNowPageResponse(
    int StatusCode,
    string? CanonicalUrl,
    string? Body = null);

public sealed record IndexNowSubmitResponse(int StatusCode);

public sealed record IndexNowSubmissionPayload(
    string Host,
    string Key,
    string KeyLocation,
    IReadOnlyList<string> Urls);

public sealed record IndexNowPendingChange(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("semanticHash")] string? SemanticHash);

public sealed record IndexNowState(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("deployed")] IReadOnlyList<string> Deployed,
    [property: JsonPropertyName("notified")] IReadOnlyDictionary<string, string> Notified,
    [property: JsonPropertyName("pending")] IReadOnlyList<IndexNowPendingChange> Pending)
{
    public static IndexNowState Empty { get; } = new(
        1,
        [],
        new Dictionary<string, string>(StringComparer.Ordinal),
        []);
}

internal sealed record PublishUrlSnapshotDocument(
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("siteUrl")] string SiteUrl,
    [property: JsonPropertyName("routes")] IReadOnlyList<PublishUrlSnapshotRouteDocument> Routes);

internal sealed record PublishUrlSnapshotRouteDocument(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("indexable")] bool Indexable,
    [property: JsonPropertyName("semanticHash")] string SemanticHash);

internal sealed record PublishUrlChangeSetDocument(
    [property: JsonPropertyName("changes")] IReadOnlyList<IndexNowPendingChange> Changes);
