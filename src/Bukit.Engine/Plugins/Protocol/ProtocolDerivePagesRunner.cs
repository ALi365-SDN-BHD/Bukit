using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Routing;

namespace Bukit.Engine.Plugins.Protocol;

internal sealed class ProtocolDerivePagesRunner
{
    private const string DerivePagesHook = "derive-pages";
    private readonly IProtocolPluginInvoker _invoker;

    public ProtocolDerivePagesRunner(IProtocolPluginInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>> RunAsync(
        BuildContext context,
        ExternalPluginConfig config,
        string pluginName,
        string pluginVersion,
        CancellationToken cancellationToken = default)
    {
        var arguments = ProcessArgumentsBuilder.Build(config.Options);
        var requestJson = BuildRequestJson(context, config, pluginName, pluginVersion);
        var result = await _invoker.InvokeAsync(config, requestJson, arguments, cancellationToken);

        if (result.TimedOut)
        {
            throw new InvalidOperationException($"[plugin-timeout][derive-pages] protocol plugin '{pluginName}' timed out.");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"[plugin-exit][derive-pages] protocol plugin '{pluginName}' exited with code {result.ExitCode}: {result.StdErr}");
        }

        if (string.IsNullOrWhiteSpace(result.StdOut))
        {
            throw new InvalidOperationException($"[plugin-protocol][derive-pages] protocol plugin '{pluginName}' returned empty stdout.");
        }

        DerivePagesResponsePayload? response;
        try
        {
            response = JsonSerializer.Deserialize(result.StdOut, ProtocolPluginJsonContext.Default.DerivePagesResponsePayload);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"[plugin-protocol][derive-pages] protocol plugin '{pluginName}' returned invalid JSON: {ex.Message}", ex);
        }

        if (response is null)
        {
            throw new InvalidOperationException($"[plugin-protocol][derive-pages] protocol plugin '{pluginName}' returned no response.");
        }

        if (!response.Ok)
        {
            throw new InvalidOperationException($"[plugin-protocol][derive-pages] {response.Error?.Message ?? $"Protocol plugin '{pluginName}' returned ok=false."}");
        }

        foreach (var log in response.Logs ?? Array.Empty<ProtocolPluginLogEntry>())
        {
            context.Logger.Info($"plugin {pluginName} {log.Level}: {log.Message}");
        }

        var derived = new List<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>();
        foreach (var page in response.DerivedPages ?? Array.Empty<ProtocolDerivedPage>())
        {
            var meta = page.Meta is null
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(page.Meta, StringComparer.OrdinalIgnoreCase);
            var item = new ContentItem(
                page.Id,
                page.Title,
                page.Slug,
                page.PublishAt,
                page.ContentHtml,
                meta);
            var route = new RouteInfo(page.Url, page.OutputPath, page.Template);
            var lastModified = page.LastModified ?? page.PublishAt;
            derived.Add((item, route, lastModified));
        }

        return derived;
    }

    private static string BuildRequestJson(BuildContext context, ExternalPluginConfig config, string pluginName, string pluginVersion)
    {
        var request = new JsonObject
        {
            ["schemaVersion"] = "1",
            ["hook"] = DerivePagesHook,
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
            ["derivePages"] = new JsonObject
            {
                ["routedPages"] = new JsonArray(context.Routed
                    .Select(x => (JsonNode)new JsonObject
                    {
                        ["id"] = x.Item.Id,
                        ["url"] = x.Route.Url,
                        ["outputPath"] = x.Route.OutputPath,
                        ["meta"] = ToJsonNode(x.Item.Meta)
                    })
                    .ToArray())
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
