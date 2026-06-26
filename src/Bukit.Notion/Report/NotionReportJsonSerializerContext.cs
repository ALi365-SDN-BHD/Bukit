using System.Text.Json.Serialization;

namespace Bukit.Notion.Report;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NotionPushReport))]
public sealed partial class NotionReportJsonSerializerContext : JsonSerializerContext;
