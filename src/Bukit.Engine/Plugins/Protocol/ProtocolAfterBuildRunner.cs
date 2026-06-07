using Bukit.Engine.Abstractions.Plugins.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.Protocol;

internal sealed class ProtocolAfterBuildRunner
{
    private const string AfterBuildHook = "after-build";
    private readonly IProtocolPluginInvoker _invoker;
    private readonly ProtocolHandshakeNegotiator _handshake;

    public ProtocolAfterBuildRunner(IProtocolPluginInvoker invoker)
    {
        _invoker = invoker;
        _handshake = new ProtocolHandshakeNegotiator(invoker);
    }

    public async Task RunAsync(BuildContext context, ExternalPluginConfig config, string pluginName, string pluginVersion, CancellationToken cancellationToken = default)
    {
        var arguments = ProcessArgumentsBuilder.Build(config.Options);
        var schemaVersion = await _handshake.GetNegotiatedSchemaVersionAsync(context, config, pluginName, pluginVersion, AfterBuildHook, arguments, cancellationToken);
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
                    ["fields"] = ProtocolJsonHelper.ToJsonNode(document.Document.CustomFields),
                    ["content"] = ProtocolContentJsonBuilder.Build(document.Document.Record)
                };
            }

            yield break;
        }
    }

}
