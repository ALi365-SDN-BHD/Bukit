using System.Text.Json.Serialization;

namespace Bukit.CloudBase.Models;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CloudBaseSignRequestPayload))]
internal partial class CloudBaseJsonContext : JsonSerializerContext;
