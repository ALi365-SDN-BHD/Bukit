using System.Text.Json.Serialization;

namespace Bukit.Engine.Plugins.Protocol;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ProtocolPluginInvocationResponse))]
[JsonSerializable(typeof(ProtocolHandshakeResponse))]
[JsonSerializable(typeof(DerivePagesResponsePayload))]
public sealed partial class ProtocolPluginJsonContext : JsonSerializerContext;
