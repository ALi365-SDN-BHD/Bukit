using Bukit.Engine.Abstractions.Plugins.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.Protocol;

internal sealed class ProtocolAfterBuildRunner
{
    private const string HandshakeCacheKey = "__protocol_handshake_cache";
    private const string AfterBuildHook = "after-build";
    private const string HandshakeHook = "handshake";
    private readonly IProtocolPluginInvoker _invoker;

    public ProtocolAfterBuildRunner(IProtocolPluginInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task RunAsync(BuildContext context, ExternalPluginConfig config, string pluginName, string pluginVersion, CancellationToken cancellationToken = default)
    {
        var arguments = ProcessArgumentsBuilder.Build(config.Options);
        var schemaVersion = await GetNegotiatedSchemaVersionAsync(context, config, pluginName, pluginVersion, arguments, cancellationToken);
        var requestJson = BuildRequestJson(context, config, pluginName, pluginVersion, schemaVersion);
        var result = await InvokeAsync(config, requestJson, arguments, cancellationToken);
        if (result.TimedOut)
        {
            throw new InvalidOperationException($"[plugin-timeout][after-build] protocol plugin '{pluginName}' timed out.");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"[plugin-exit][after-build] protocol plugin '{pluginName}' exited with code {result.ExitCode}: {result.StdErr}");
        }

        if (string.IsNullOrWhiteSpace(result.StdOut))
        {
            throw new InvalidOperationException($"[plugin-protocol][after-build] protocol plugin '{pluginName}' returned empty stdout.");
        }

        ProtocolPluginInvocationResponse? response;
        try
        {
            response = JsonSerializer.Deserialize(result.StdOut, ProtocolPluginJsonContext.Default.ProtocolPluginInvocationResponse);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"[plugin-protocol][after-build] protocol plugin '{pluginName}' returned invalid JSON: {ex.Message}", ex);
        }

        if (response is null)
        {
            throw new InvalidOperationException($"[plugin-protocol][after-build] protocol plugin '{pluginName}' returned no response.");
        }

        if (!response.Ok)
        {
            throw new InvalidOperationException($"[plugin-protocol][after-build] {response.Error?.Message ?? $"Protocol plugin '{pluginName}' returned ok=false."}");
        }

        foreach (var log in response.Logs ?? Array.Empty<ProtocolPluginLogEntry>())
        {
            context.Logger.Info($"plugin {pluginName} {log.Level}: {log.Message}");
        }

        var writtenOutputs = ProtocolOutputWriter.WriteOutputs(context.OutputDir, response.Outputs ?? Array.Empty<AfterBuildOutputFile>());
        if (writtenOutputs.Count > 0)
        {
            if (!context.Data.TryGetValue("__plugin_outputs", out var outputsObj) || outputsObj is not HashSet<PluginOutputTrackingInfo> outputs)
            {
                outputs = new HashSet<PluginOutputTrackingInfo>();
                context.Data["__plugin_outputs"] = outputs;
            }

            foreach (var output in writtenOutputs)
            {
                outputs.Add(new PluginOutputTrackingInfo(pluginName, AfterBuildHook, output));
            }
        }
    }

    private Task<ProtocolPluginInvocationResult> InvokeAsync(
        ExternalPluginConfig config,
        string requestJson,
        string? arguments,
        CancellationToken cancellationToken)
    {
        return _invoker.InvokeAsync(config, requestJson, arguments, cancellationToken);
    }

    private async Task<string> NegotiateSchemaVersionAsync(ExternalPluginConfig config, string? arguments, CancellationToken cancellationToken)
    {
        var handshakeRequest = BuildHandshakeRequestJson();
        var result = await InvokeAsync(config, handshakeRequest, arguments, cancellationToken);
        if (result.TimedOut)
        {
            throw new InvalidOperationException("[plugin-protocol][handshake] protocol plugin handshake timeout.");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"[plugin-protocol][handshake] protocol plugin handshake exited with code {result.ExitCode}: {result.StdErr}");
        }

        if (string.IsNullOrWhiteSpace(result.StdOut))
        {
            throw new InvalidOperationException("[plugin-protocol][handshake] protocol plugin handshake returned empty stdout.");
        }

        try
        {
            var response = JsonSerializer.Deserialize(result.StdOut, ProtocolPluginJsonContext.Default.ProtocolHandshakeResponse);
            if (response is null || !response.Ok)
            {
                throw new InvalidOperationException($"[plugin-protocol][handshake] {response?.Error?.Message ?? "Protocol plugin handshake returned ok=false."} Bukit vNext requires protocol schema version 2.");
            }

            if (!string.Equals(response.NegotiatedSchemaVersion, "2", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"[plugin-protocol][handshake] unsupported negotiated schema version '{response.NegotiatedSchemaVersion ?? "<missing>"}'. Bukit vNext requires protocol schema version 2.");
            }

            return "2";
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"[plugin-protocol][handshake] protocol plugin handshake returned invalid JSON: {ex.Message}", ex);
        }
    }

    private async Task<string> GetNegotiatedSchemaVersionAsync(
        BuildContext context,
        ExternalPluginConfig config,
        string pluginName,
        string pluginVersion,
        string? arguments,
        CancellationToken cancellationToken)
    {
        var cacheToken = BuildHandshakeCacheToken(pluginName, pluginVersion, config, arguments);
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

        var negotiated = await NegotiateSchemaVersionAsync(config, arguments, cancellationToken);
        lock (context.Data)
        {
            cache[cacheToken] = negotiated;
        }

        return negotiated;
    }

    private static string BuildHandshakeCacheToken(
        string pluginName,
        string pluginVersion,
        ExternalPluginConfig config,
        string? arguments)
    {
        return string.Join("|", new[]
        {
            pluginName,
            pluginVersion,
            config.Runtime ?? string.Empty,
            config.Entry ?? string.Empty,
            arguments ?? string.Empty
        });
    }

    private static string BuildHandshakeRequestJson()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "2",
            ["hook"] = HandshakeHook,
            ["requestedHook"] = AfterBuildHook,
            ["hostSupportedSchemaVersions"] = new JsonArray("2")
        }.ToJsonString();
    }

    private static string BuildRequestJson(BuildContext context, ExternalPluginConfig config, string pluginName, string pluginVersion, string schemaVersion)
    {
        var includeRoutedPages = context.Config.Site.ExternalProtocolIncludeRoutedPages;
        var routedPages = includeRoutedPages
            ? new JsonArray(BuildRoutedPages(context).ToArray())
            : new JsonArray();

        var request = new JsonObject
        {
            ["schemaVersion"] = schemaVersion,
            ["hook"] = AfterBuildHook,
            ["plugin"] = new JsonObject
            {
                ["name"] = pluginName,
                ["version"] = pluginVersion
            },
            ["site"] = new JsonObject
            {
                ["baseUrl"] = context.BaseUrl,
                ["language"] = context.Config.Site.Language,
                ["title"] = context.Config.Site.Title
            },
            ["config"] = new JsonObject
            {
                ["pluginOptions"] = ProtocolJsonHelper.ToJsonNode(config.Options)
            },
            ["afterBuild"] = new JsonObject
            {
                ["projectRoot"] = context.RootDir,
                ["outputDir"] = context.OutputDir,
                ["routedPages"] = routedPages
            }
        };

        return request.ToJsonString();
    }

    private static IEnumerable<JsonNode> BuildRoutedPages(BuildContext context)
    {
        if (context.RoutedDocuments.Count > 0)
        {
            foreach (var document in context.RoutedDocuments)
            {
                yield return new JsonObject
                {
                    ["id"] = document.Document.Id,
                    ["url"] = document.Route.Url,
                    ["outputPath"] = document.Route.OutputPath,
                    ["fields"] = ProtocolJsonHelper.ToJsonNode(document.Document.Fields),
                    ["content"] = ProtocolContentJsonBuilder.Build(document.Document.Record)
                };
            }

            yield break;
        }
    }

}
