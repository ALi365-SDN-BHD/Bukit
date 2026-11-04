using System.Text.Json.Serialization;

namespace Bukit.IndexNow;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(PublishUrlSnapshotDocument))]
[JsonSerializable(typeof(PublishUrlChangeSetDocument))]
[JsonSerializable(typeof(IndexNowState))]
[JsonSerializable(typeof(IndexNowHttpSubmissionPayload))]
internal sealed partial class IndexNowJsonContext : JsonSerializerContext;
