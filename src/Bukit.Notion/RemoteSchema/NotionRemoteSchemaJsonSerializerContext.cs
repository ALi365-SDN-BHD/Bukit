using System.Text.Json.Serialization;

namespace Bukit.Notion.RemoteSchema;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NotionRemoteSchemaReport))]
public sealed partial class NotionRemoteSchemaJsonSerializerContext : JsonSerializerContext;
