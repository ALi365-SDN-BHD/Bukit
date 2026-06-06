using Bukit.Engine.Abstractions.Plugins.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine.Plugins.Protocol;

internal sealed class ProtocolDerivePagesRunner
{
    private const string DerivePagesHook = "derive-pages";
    private readonly IProtocolPluginInvoker _invoker;

    public ProtocolDerivePagesRunner(IProtocolPluginInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<IReadOnlyList<RoutedContentDocument>> RunAsync(
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

        var derived = new List<RoutedContentDocument>();
        foreach (var page in response.DerivedPages ?? Array.Empty<ProtocolDerivedPage>())
        {
            var fields = page.Fields is null
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(JsonElementMaterializer.Materialize(page.Fields)!, StringComparer.OrdinalIgnoreCase);
            var document = ContentDocument.Create(
                page.Id,
                page.Title,
                page.Slug,
                page.PublishAt,
                page.ContentHtml,
                ContentFieldReader.ToFieldMap(fields));
            var route = new RouteInfo(page.Url, page.OutputPath, page.Template);
            var lastModified = page.LastModified ?? page.PublishAt;
            derived.Add(new RoutedContentDocument(document, route, lastModified));
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
                ["pluginOptions"] = ProtocolJsonHelper.ToJsonNode(config.Options)
            },
            ["derivePages"] = new JsonObject
            {
                ["projectRoot"] = context.RootDir,
                ["outputDir"] = context.OutputDir,
                ["routedPages"] = new JsonArray(BuildRoutedPages(context).ToArray())
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
