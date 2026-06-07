using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Plugins.Protocol;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.Protocol;

internal sealed class ProtocolHandshakeNegotiator
{
    private const string HandshakeCacheKey = "__protocol_handshake_cache";
    private const string HandshakeHook = "handshake";
    private readonly IProtocolPluginInvoker _invoker;

    internal ProtocolHandshakeNegotiator(IProtocolPluginInvoker invoker)
    {
        _invoker = invoker;
    }

    internal async Task<string> GetNegotiatedSchemaVersionAsync(
        BuildContext context,
        ExternalPluginConfig config,
        string pluginName,
        string pluginVersion,
        string requestedHook,
        string? arguments,
        CancellationToken cancellationToken)
    {
        var cacheToken = BuildHandshakeCacheToken(pluginName, pluginVersion, requestedHook, config, arguments);
        Dictionary<string, string> cache;
        lock (context.Data)
        {
            if (context.Data.TryGetValue(HandshakeCacheKey, out var cacheObj)
                && cacheObj is Dictionary<string, string> existingCache)
            {
                cache = existingCache;
            }
            else
            {
                cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                context.Data[HandshakeCacheKey] = cache;
            }

            if (cache.TryGetValue(cacheToken, out var cachedVersion))
            {
                return cachedVersion;
            }
        }

        var negotiated = await NegotiateSchemaVersionAsync(config, requestedHook, arguments, cancellationToken);
        lock (context.Data)
        {
            cache[cacheToken] = negotiated;
        }

        return negotiated;
    }

    private async Task<string> NegotiateSchemaVersionAsync(
        ExternalPluginConfig config,
        string requestedHook,
        string? arguments,
        CancellationToken cancellationToken)
    {
        var handshakeRequest = BuildHandshakeRequestJson(requestedHook);
        var result = await _invoker.InvokeAsync(config, handshakeRequest, arguments, cancellationToken);
        if (result.TimedOut)
        {
            throw new ConfigException("[plugin-protocol][handshake] protocol plugin handshake timeout.", DiagnosticCode.PluginExecutionFailed);
        }

        if (result.ExitCode != 0)
        {
            throw new ConfigException($"[plugin-protocol][handshake] protocol plugin handshake exited with code {result.ExitCode}: {result.StdErr}", DiagnosticCode.PluginExecutionFailed);
        }

        if (string.IsNullOrWhiteSpace(result.StdOut))
        {
            throw new ConfigException("[plugin-protocol][handshake] protocol plugin handshake returned empty stdout.", DiagnosticCode.PluginExecutionFailed);
        }

        try
        {
            var response = JsonSerializer.Deserialize(result.StdOut, ProtocolPluginJsonContext.Default.ProtocolHandshakeResponse);
            if (response is null || !response.Ok)
            {
                throw new ConfigException($"[plugin-protocol][handshake] {response?.Error?.Message ?? "Protocol plugin handshake returned ok=false."} Bukit vNext requires protocol schema version 2.", DiagnosticCode.PluginExecutionFailed);
            }

            if (!string.Equals(response.NegotiatedSchemaVersion, "2", StringComparison.Ordinal))
            {
                throw new ConfigException($"[plugin-protocol][handshake] unsupported negotiated schema version '{response.NegotiatedSchemaVersion ?? "<missing>"}'. Bukit vNext requires protocol schema version 2.", DiagnosticCode.PluginExecutionFailed);
            }

            return "2";
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"[plugin-protocol][handshake] protocol plugin handshake returned invalid JSON: {ex.Message}", ex, DiagnosticCode.PluginExecutionFailed);
        }
    }

    private static string BuildHandshakeCacheToken(
        string pluginName,
        string pluginVersion,
        string requestedHook,
        ExternalPluginConfig config,
        string? arguments)
    {
        return string.Join("|", new[]
        {
            pluginName,
            pluginVersion,
            requestedHook,
            config.Runtime ?? string.Empty,
            config.Entry ?? string.Empty,
            arguments ?? string.Empty
        });
    }

    private static string BuildHandshakeRequestJson(string requestedHook)
    {
        return new JsonObject
        {
            ["schemaVersion"] = "2",
            ["hook"] = HandshakeHook,
            ["requestedHook"] = requestedHook,
            ["hostSupportedSchemaVersions"] = new JsonArray("2")
        }.ToJsonString();
    }
}
