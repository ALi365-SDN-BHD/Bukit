using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Plugins;

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

        ProtocolOutputWriter.WriteOutputs(context.OutputDir, response.Outputs ?? Array.Empty<AfterBuildOutputFile>());
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
        if (result.TimedOut || result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return "1";
        }

        try
        {
            var response = JsonSerializer.Deserialize(result.StdOut, ProtocolPluginJsonContext.Default.ProtocolHandshakeResponse);
            if (response is null || !response.Ok)
            {
                return "1";
            }

            return string.Equals(response.NegotiatedSchemaVersion, "2", StringComparison.Ordinal)
                ? "2"
                : "1";
        }
        catch (JsonException)
        {
            return "1";
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
            ["hostSupportedSchemaVersions"] = new JsonArray("2", "1")
        }.ToJsonString();
    }

    private static string BuildRequestJson(BuildContext context, ExternalPluginConfig config, string pluginName, string pluginVersion, string schemaVersion)
    {
        var includeRoutedPages = context.Config.Site.ExternalProtocolIncludeRoutedPages;
        var routedPages = includeRoutedPages
            ? new JsonArray(context.Routed
                .Select(x => (JsonNode)new JsonObject
                {
                    ["id"] = x.Item.Id,
                    ["url"] = x.Route.Url,
                    ["outputPath"] = x.Route.OutputPath,
                    ["meta"] = ToJsonNode(x.Item.Meta)
                })
                .ToArray())
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
                ["pluginOptions"] = ToJsonNode(config.Options)
            },
            ["afterBuild"] = new JsonObject
            {
                ["outputDir"] = context.OutputDir,
                ["routedPages"] = routedPages
            }
        };

        return request.ToJsonString();
    }

    private static JsonNode? ToJsonNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode node)
        {
            return node.DeepClone();
        }

        if (value is IReadOnlyDictionary<string, object> readOnlyMap)
        {
            return ToJsonObject(readOnlyMap);
        }

        if (value is IDictionary<string, object> map)
        {
            return ToJsonObject(map);
        }

        if (value is IEnumerable<object> sequence && value is not string)
        {
            var array = new JsonArray();
            foreach (var item in sequence)
            {
                array.Add(ToJsonNode(item));
            }

            return array;
        }

        return value switch
        {
            string text => JsonValue.Create(text),
            bool boolean => JsonValue.Create(boolean),
            byte number => JsonValue.Create(number),
            sbyte number => JsonValue.Create(number),
            short number => JsonValue.Create(number),
            ushort number => JsonValue.Create(number),
            int number => JsonValue.Create(number),
            uint number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            ulong number => JsonValue.Create(number),
            float number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            DateTime dateTime => JsonValue.Create(dateTime),
            DateTimeOffset dateTimeOffset => JsonValue.Create(dateTimeOffset),
            Guid guid => JsonValue.Create(guid),
            Enum enumValue => JsonValue.Create(Convert.ToString(enumValue, CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(value.ToString())
        };
    }

    private static JsonObject ToJsonObject(IEnumerable<KeyValuePair<string, object>> map)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in map)
        {
            obj[key] = ToJsonNode(value);
        }

        return obj;
    }
}
