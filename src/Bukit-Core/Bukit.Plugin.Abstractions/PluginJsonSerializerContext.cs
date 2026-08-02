using System.Text.Json.Serialization;
using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.Abstractions;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PluginHostConfig))]
[JsonSerializable(typeof(PluginConfigEntry))]
[JsonSerializable(typeof(PluginResourceLimitOptions))]
[JsonSerializable(typeof(PluginManifest))]
[JsonSerializable(typeof(PluginPlatformEntry))]
[JsonSerializable(typeof(PluginCommandSpec))]
[JsonSerializable(typeof(PluginOptionSpec))]
[JsonSerializable(typeof(PluginArgumentSpec))]
[JsonSerializable(typeof(PluginRequestEnvelope))]
[JsonSerializable(typeof(PluginResponseEnvelope))]
[JsonSerializable(typeof(PluginHandshakeRequest))]
[JsonSerializable(typeof(PluginHandshakeResponse))]
[JsonSerializable(typeof(PluginIdentity))]
[JsonSerializable(typeof(PluginManifestRequest))]
[JsonSerializable(typeof(PluginManifestResponse))]
[JsonSerializable(typeof(PluginInvokeRequest))]
[JsonSerializable(typeof(PluginInvokeResponse))]
[JsonSerializable(typeof(PluginInvokeCommand))]
[JsonSerializable(typeof(PluginInvokeContext))]
[JsonSerializable(typeof(PluginPermissionSet))]
[JsonSerializable(typeof(PluginFileSystemPermission))]
[JsonSerializable(typeof(PluginEnvironmentPermission))]
[JsonSerializable(typeof(PluginMessage))]
[JsonSerializable(typeof(PluginDiagnostic))]
[JsonSerializable(typeof(PluginArtifact))]
[JsonSerializable(typeof(PluginError))]
public sealed partial class PluginJsonSerializerContext : JsonSerializerContext;
