using System.Text;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn.MiniProgram;

/// <summary>
/// Writes the declared mini-program datasets as content-addressed JSON plus a per-language
/// <c>catalog.json</c>, so a mini-program fetches only the files whose hash changed.
/// </summary>
internal sealed class MiniProgramPlugin : IBukitPlugin, IAfterBuildAsyncPlugin
{
    internal const string CatalogSchema = "https://bukit.dev/schemas/miniprogram-catalog.v1.json";
    internal const string SchemaVersion = "1.0";

    private readonly AppConfig _config;

    internal MiniProgramPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "miniprogram";
    public string Version => "1.0.0";

    public async Task AfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        var options = _config.MiniProgram;
        if (!options.Enabled)
        {
            return;
        }

        var siteUrl = RequireHttpsSiteUrl(_config.Site.Url);
        var dataModules = context.TryGet(BuildDataKeys.DataModules, out var prepared) ? prepared : null;
        var contentRecords = CollectContentRecords(context, siteUrl);
        // Only pages that get a detail record carry data-bukit-id; other links stay plain links.
        var collectionSources = options.Datasets
            .Where(dataset => dataset.Kind == MiniProgramKinds.Collection)
            .Select(dataset => dataset.Source!)
            .ToArray();
        var internalIds = contentRecords
            .Where(record => record.PageUrl is not null &&
                collectionSources.Any(source => MiniProgramDatasetBuilder.IsFromSource(record.Document, source)))
            .GroupBy(record => record.PageUrl!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
        var run = new Run(
            context,
            siteUrl,
            contentRecords,
            dataModules,
            new MiniProgramDatasetBuilder(siteUrl, context.BaseUrl, new MiniProgramHtmlSanitizer(siteUrl, context.BaseUrl, internalIds)),
            new FileSet(context.OutputDir, options.OutputDir, siteUrl, context.BaseUrl));
        var datasets = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            ["site"] = run.Writer.Write("site", BuildSite(siteUrl, context.BaseUrl), "site")
        };
        foreach (var dataset in options.Datasets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            datasets[dataset.Name] = await WriteDatasetAsync(run, dataset, options.MaxListItems, cancellationToken);
        }

        run.Writer.WriteCatalog(BuildCatalog(datasets, run.Dates));
        var outputs = context.GetOrAdd(BuildDataKeys.PluginOutputs, static () => []);
        foreach (var path in run.Writer.Paths)
        {
            outputs.Add(new PluginOutputTrackingInfo(Name, "after-build", path));
        }
    }

    private static string RequireHttpsSiteUrl(string? url)
    {
        var siteUrl = url?.Trim().TrimEnd('/');
        if (siteUrl is null || !siteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigException(
                "miniprogram requires site.url (or --site-url) to be an https:// URL.",
                DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return siteUrl;
    }

    private async Task<JsonObject> WriteDatasetAsync(Run run, MiniProgramDatasetConfig dataset, int maxListItems, CancellationToken cancellationToken)
        => dataset.Kind switch
        {
            MiniProgramKinds.Collection => await WriteCollectionAsync(run, dataset, maxListItems, cancellationToken),
            MiniProgramKinds.Module or MiniProgramKinds.Records => await WriteDataRecordsAsync(run, dataset, cancellationToken),
            MiniProgramKinds.DataIndex => run.Writer.Write(dataset.Name, new JsonObject { ["values"] = run.Builder.BuildMap(dataset, ResolveDataIndex(dataset, run.DataModules)) }, dataset.Kind),
            MiniProgramKinds.Taxonomy => run.Writer.Write(dataset.Name, new JsonObject { ["items"] = BuildTaxonomy(dataset, run.ContentRecords) }, dataset.Kind),
            MiniProgramKinds.Menu => run.Writer.Write(dataset.Name, new JsonObject { ["items"] = BuildMenu(_config.Site.Menus![dataset.Menu!], run.SiteUrl, run.Context.BaseUrl) }, dataset.Kind),
            _ => throw new InvalidOperationException($"Unsupported mini-program dataset kind '{dataset.Kind}'.")
        };

    /// <summary>State shared by the dataset writers of one variant build.</summary>
    private sealed record Run(
        BuildContext Context,
        string SiteUrl,
        IReadOnlyList<MiniProgramRecord> ContentRecords,
        DataModuleResult? DataModules,
        MiniProgramDatasetBuilder Builder,
        FileSet Writer)
    {
        internal List<DateTimeOffset> Dates { get; } = [];
    }

    private JsonObject BuildCatalog(SortedDictionary<string, JsonObject> datasets, List<DateTimeOffset> dates)
    {
        var hashInput = new StringBuilder();
        var entries = new JsonObject();
        foreach (var (name, entry) in datasets)
        {
            hashInput.Append(name).Append('\n').Append((string)entry["hash"]!).Append('\n');
            if (entry["detailCatalog"] is JsonObject detail)
            {
                hashInput.Append((string)detail["hash"]!).Append('\n');
            }

            entries[name] = entry;
        }

        return new JsonObject
        {
            ["schema"] = CatalogSchema,
            ["schemaVersion"] = SchemaVersion,
            ["minClientSchema"] = SchemaVersion,
            ["language"] = _config.Site.Language,
            ["generatedAt"] = MiniProgramJson.FormatDate(dates.Count == 0 ? DateTimeOffset.UnixEpoch : dates.Max()),
            ["catalogHash"] = "sha256:" + MiniProgramJson.Hash(Encoding.UTF8.GetBytes(hashInput.ToString())),
            ["datasets"] = entries
        };
    }

    private JsonObject BuildSite(string siteUrl, string baseUrl)
    {
        var languages = new JsonArray();
        foreach (var language in _config.Site.Languages ?? [_config.Site.Language])
        {
            languages.Add(language);
        }

        return new JsonObject
        {
            ["name"] = _config.Site.Name,
            ["title"] = _config.Site.Title,
            ["description"] = _config.Site.Description,
            ["url"] = MiniProgramJson.Absolute(siteUrl, baseUrl, "/", siteUrl),
            ["language"] = _config.Site.Language,
            ["languages"] = languages
        };
    }

    private static List<MiniProgramRecord> CollectContentRecords(BuildContext context, string siteUrl)
    {
        var records = new List<MiniProgramRecord>();
        foreach (var routed in context.RoutedDocuments)
        {
            if (routed.Route.Template == RedirectPageHtml.RouteTemplate || !MiniProgramDatasetBuilder.IsPublic(routed.Document) ||
                context.SeoIndex.TryGetValue(BuildPathUtils.NormalizeRelPath(routed.Route.OutputPath), out var entry) && !entry.Indexable)
            {
                continue;
            }

            records.Add(new MiniProgramRecord(
                PublicContentProjectionPolicy.ResolvePublicId(routed.Document.Record, routed.Route.Url),
                routed.Document,
                SeoModelBuilder.BuildAbsoluteUrl(siteUrl, context.BaseUrl, routed.Route.Url),
                BodyHtml: null));
        }

        return records;
    }

    private static async Task<JsonObject> WriteCollectionAsync(
        Run run,
        MiniProgramDatasetConfig dataset,
        int maxListItems,
        CancellationToken cancellationToken)
    {
        var (builder, writer) = (run.Builder, run.Writer);
        var selected = run.ContentRecords.Where(record => MiniProgramDatasetBuilder.IsFromSource(record.Document, dataset.Source!)).ToList();
        if (selected.Count > maxListItems)
        {
            throw MiniProgramDatasetBuilder.Invalid(dataset, "*", "items", $"has {selected.Count} records, more than miniprogram.maxListItems");
        }

        var duplicate = selected.GroupBy(record => record.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw MiniProgramDatasetBuilder.Invalid(dataset, duplicate.Key, "id", "is shared by more than one page");
        }

        var needsBody = dataset.ListFields!.Contains("body") || dataset.DetailFields!.Contains("body");
        var records = await WithBodiesAsync(selected, needsBody, run.Context.BodyStore, cancellationToken);
        run.Dates.AddRange(records.Select(record => record.Document.Record.Lifecycle.UpdatedAt ?? record.Document.PublishAt));

        var entry = writer.Write(dataset.Name + ".list", new JsonObject { ["items"] = builder.BuildItems(dataset, records, dataset.ListFields!) }, dataset.Kind);
        var details = new JsonArray();
        foreach (var record in records.OrderBy(record => record.Id, StringComparer.Ordinal))
        {
            var (hash, url, size) = writer.WriteDetail(dataset.Name, builder.BuildItem(dataset, record, dataset.DetailFields!));
            details.Add(new JsonObject
            {
                ["id"] = record.Id,
                ["hash"] = hash,
                ["url"] = url,
                ["size"] = size,
                ["updatedAt"] = MiniProgramJson.FormatDate(record.Document.Record.Lifecycle.UpdatedAt ?? record.Document.PublishAt)
            });
        }

        var detailCatalog = writer.Write(dataset.Name + ".details", new JsonObject { ["dataset"] = dataset.Name, ["items"] = details }, kind: null);
        entry["detailCatalog"] = new JsonObject { ["hash"] = detailCatalog["hash"]!.DeepClone(), ["url"] = detailCatalog["url"]!.DeepClone() };
        return entry;
    }

    private static async Task<JsonObject> WriteDataRecordsAsync(Run run, MiniProgramDatasetConfig dataset, CancellationToken cancellationToken)
    {
        var selected = new List<MiniProgramRecord>();
        foreach (var document in run.DataModules?.DataDocuments ?? [])
        {
            if (!MiniProgramDatasetBuilder.IsPublic(document) || !MiniProgramDatasetBuilder.IsFromSource(document, dataset.Source!) ||
                dataset.Kind == MiniProgramKinds.Module &&
                !string.Equals(ContentFieldReader.GetContentType(document).Trim(), dataset.Module, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Data records carry no public route; an opaque hash keeps source page ids out of public files.
            selected.Add(new MiniProgramRecord(MiniProgramJson.Hash(Encoding.UTF8.GetBytes(document.Id))[..16], document, PageUrl: null, BodyHtml: null));
        }

        var records = await WithBodiesAsync(selected, dataset.Fields!.Contains("body"), run.Context.BodyStore, cancellationToken);
        run.Dates.AddRange(records.Select(record => record.Document.Record.Lifecycle.UpdatedAt ?? record.Document.PublishAt));
        return run.Writer.Write(dataset.Name, new JsonObject { ["items"] = run.Builder.BuildItems(dataset, records, dataset.Fields!) }, dataset.Kind);
    }

    private static async Task<List<MiniProgramRecord>> WithBodiesAsync(
        List<MiniProgramRecord> records,
        bool needsBody,
        IContentBodyStore bodyStore,
        CancellationToken cancellationToken)
    {
        if (!needsBody)
        {
            return records;
        }

        var resolved = new List<MiniProgramRecord>(records.Count);
        foreach (var record in records)
        {
            var html = await ContentBodyResolver.GetHtmlAsync(record.Document, bodyStore, cancellationToken).ConfigureAwait(false);
            resolved.Add(record with { BodyHtml = html });
        }

        return resolved;
    }

    private static IReadOnlyDictionary<string, object> ResolveDataIndex(MiniProgramDatasetConfig dataset, DataModuleResult? dataModules)
    {
        if (dataModules?.DataIndex is { } index &&
            index.TryGetValue(dataset.Source!, out var source) &&
            source is IReadOnlyDictionary<string, object> scopes &&
            scopes.TryGetValue(dataset.Scope!, out var scope) &&
            scope is IReadOnlyDictionary<string, object> values)
        {
            return values;
        }

        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static JsonArray BuildTaxonomy(MiniProgramDatasetConfig dataset, IReadOnlyList<MiniProgramRecord> contentRecords)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var record in contentRecords)
        {
            var terms = dataset.Taxonomy switch
            {
                "tags" => record.Document.Record.Classification.Tags,
                "categories" => record.Document.Record.Classification.Sections,
                _ => ContentFieldReader.GetTextList(record.Document.CustomFields, dataset.Taxonomy!) ?? []
            };
            foreach (var term in terms.Where(term => !string.IsNullOrWhiteSpace(term)).Select(term => term.Trim()).Distinct(StringComparer.Ordinal))
            {
                counts[term] = counts.GetValueOrDefault(term) + 1;
            }
        }

        var items = new JsonArray();
        foreach (var (name, count) in counts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            items.Add(new JsonObject { ["name"] = name, ["count"] = count });
        }

        return items;
    }

    private static JsonArray BuildMenu(IReadOnlyList<MenuConfig> items, string siteUrl, string baseUrl)
    {
        var array = new JsonArray();
        foreach (var item in items)
        {
            var node = new JsonObject
            {
                ["title"] = item.Name,
                ["url"] = MiniProgramJson.Absolute(siteUrl, baseUrl, item.Url, MiniProgramJson.Absolute(siteUrl, baseUrl, "/", siteUrl))
            };
            if (item.Children is { Count: > 0 })
            {
                node["children"] = BuildMenu(item.Children, siteUrl, baseUrl);
            }

            array.Add(node);
        }

        return array;
    }

    /// <summary>Writes content-addressed files under the variant's mini-program directory and records their paths.</summary>
    private sealed class FileSet(string outputDir, string directory, string siteUrl, string baseUrl)
    {
        private readonly List<string> _paths = [];

        internal IReadOnlyList<string> Paths => _paths;

        internal JsonObject Write(string stem, JsonNode content, string? kind)
        {
            var (bytes, hash, url) = WriteFile($"{stem}.{{0}}.json", content);
            var entry = new JsonObject();
            if (kind is not null)
            {
                entry["kind"] = kind;
            }

            entry["hash"] = "sha256:" + hash;
            entry["url"] = url;
            entry["size"] = bytes.Length;
            return entry;
        }

        internal (string Hash, string Url, int Size) WriteDetail(string dataset, JsonNode content)
        {
            var (bytes, hash, url) = WriteFile(dataset + "/{0}.json", content);
            return ("sha256:" + hash, url, bytes.Length);
        }

        internal void WriteCatalog(JsonNode catalog) => WriteBytes(directory + "/catalog.json", MiniProgramJson.Serialize(catalog));

        private (byte[] Bytes, string Hash, string Url) WriteFile(string pattern, JsonNode content)
        {
            var bytes = MiniProgramJson.Serialize(content);
            var hash = MiniProgramJson.Hash(bytes);
            var relative = directory + "/" + string.Format(System.Globalization.CultureInfo.InvariantCulture, pattern, hash[..16]);
            WriteBytes(relative, bytes);
            return (bytes, hash, siteUrl + ContentProjectionPaths.GetPublicOutputUrl(relative, baseUrl));
        }

        private void WriteBytes(string relative, byte[] bytes)
        {
            var path = FileWriter.GetSafeFullPath(outputDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            _paths.Add(relative);
        }
    }
}
