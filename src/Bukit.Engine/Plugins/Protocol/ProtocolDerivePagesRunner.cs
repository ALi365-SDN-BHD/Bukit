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
            var fields = BuildFields(page);
            var item = new ContentItem(
                page.Id,
                page.Title,
                page.Slug,
                page.PublishAt,
                page.ContentHtml,
                fields);
            var route = new RouteInfo(page.Url, page.OutputPath, page.Template);
            var lastModified = page.LastModified ?? page.PublishAt;
            derived.Add((item, route, lastModified));
        }

        return derived;
    }

    private static IReadOnlyDictionary<string, ContentField> BuildFields(ProtocolDerivedPage page)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (page.Fields is null)
        {
            return fields;
        }

        var materialized = JsonElementMaterializer.Materialize(page.Fields);
        if (materialized is IReadOnlyDictionary<string, object> map)
        {
            foreach (var (key, value) in map)
            {
                if (value is not null)
                {
                    fields[key] = new ContentField("protocol", value);
                }
            }
        }

        return fields;
    }

    private static string BuildRequestJson(BuildContext context, ExternalPluginConfig config, string pluginName, string pluginVersion)
    {
        var request = new JsonObject
        {
            ["schemaVersion"] = context.RoutedDocuments.Count > 0 ? "2" : "1",
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
                ["routedPages"] = BuildRoutedPagesJson(context)
            }
        };

        return request.ToJsonString();
    }

    private static JsonArray BuildRoutedPagesJson(BuildContext context)
    {
        if (context.RoutedDocuments.Count > 0)
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
                        ["noIndex"] = x.Document.Publish.NoIndex,
                        ["noFollow"] = x.Document.Publish.NoFollow,
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
            ["title"] = record.Presentation.Title,
            ["type"] = record.Identity.ContentType,
            ["collection"] = record.Classification.Collection,
            ["language"] = record.Presentation.Language,
            ["summary"] = record.Presentation.Summary
        };
    }
}
