using Bukit.Engine.Abstractions.Plugins.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
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
            ? BuildRoutedPages(context, schemaVersion)
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

    private static JsonArray BuildRoutedPages(BuildContext context, string schemaVersion)
    {
        if (string.Equals(schemaVersion, "2", StringComparison.Ordinal) && context.RoutedDocuments.Count > 0)
        {
            return new JsonArray(context.RoutedDocuments
                .Select(x => (JsonNode)new JsonObject
                {
                    ["content"] = ToContentJson(x.Document),
                    ["route"] = new JsonObject
                    {
                        ["url"] = x.Route.Url,
                        ["outputPath"] = x.Route.OutputPath,
                        ["template"] = x.Route.Template
                    },
                    ["publish"] = new JsonObject
                    {
                        ["draft"] = x.Document.Publish.Draft,
                        ["noindex"] = x.Document.Publish.NoIndex,
                        ["nofollow"] = x.Document.Publish.NoFollow,
                        ["excludeFromFeed"] = x.Document.Publish.ExcludeFromFeed,
                        ["excludeFromSearch"] = x.Document.Publish.ExcludeFromSearch,
                        ["excludeFromSitemap"] = x.Document.Publish.ExcludeFromSitemap,
                        ["isDataModule"] = x.Document.Publish.IsDataModule
                    },
                    ["fields"] = ProtocolJsonHelper.ToJsonNode(x.Document.CustomFields)
                })
                .ToArray());
        }

        return new JsonArray(context.Routed
            .Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Item.Id,
                ["title"] = x.Item.Title,
                ["slug"] = x.Item.Slug,
                ["url"] = x.Route.Url,
                ["outputPath"] = x.Route.OutputPath
            })
            .ToArray());
    }

    private static JsonObject ToContentJson(ContentDocument document)
    {
        var record = document.Record;
        return new JsonObject
        {
            ["id"] = record.Identity.Id,
            ["slug"] = record.Identity.Slug,
            ["canonicalUrlKey"] = record.Identity.CanonicalUrlKey,
            ["contentType"] = record.Identity.ContentType,
            ["status"] = record.Identity.Status,
            ["title"] = record.Presentation.Title,
            ["summary"] = record.Presentation.Summary,
            ["language"] = record.Presentation.Language,
            ["publishedAt"] = record.Lifecycle.PublishedAt.ToString("O"),
            ["updatedAt"] = record.Lifecycle.UpdatedAt?.ToString("O"),
            ["source"] = record.Provenance.Source,
            ["reviewStatus"] = record.Trust.ReviewStatus,
            ["tags"] = new JsonArray(record.Classification.Tags.Select(tag => (JsonNode)tag).ToArray()),
            ["sections"] = new JsonArray(record.Classification.Sections.Select(section => (JsonNode)section).ToArray()),
            ["entities"] = new JsonArray(record.Entities
                .Select(entity => (JsonNode)new JsonObject
                {
                    ["type"] = entity.Type,
                    ["name"] = entity.Name,
                    ["id"] = entity.Id,
                    ["description"] = entity.Description
                })
                .ToArray())
        };
    }

}
