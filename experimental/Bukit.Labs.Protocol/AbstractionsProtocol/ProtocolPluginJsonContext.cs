using System.Text.Json.Serialization;

namespace Bukit.Engine.Abstractions.Plugins.Protocol;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ProtocolPluginInvocationRequest))]
[JsonSerializable(typeof(ProtocolPluginInvocationResponse))]
[JsonSerializable(typeof(ProtocolHandshakeResponse))]
[JsonSerializable(typeof(DerivePagesResponsePayload))]
public sealed partial class ProtocolPluginJsonContext : JsonSerializerContext;
