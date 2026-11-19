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
    private readonly BuildExecutionMode _executionMode;

    internal MiniProgramPlugin(AppConfig config, BuildExecutionMode executionMode = BuildExecutionMode.Production)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _executionMode = executionMode;
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

        var siteUrl = MiniProgramConfigValidator.ValidateSiteUrl(_config, required: _executionMode == BuildExecutionMode.Production);
        if (siteUrl is null)
        {
            // Development builds without site.url skip the output; production builds failed above.
            context.Logger.Info("event=miniprogram.skipped reason=no_site_url");
            return;
        }

        var dataModules = context.TryGet(BuildDataKeys.DataModules, out var prepared) ? prepared : null;
        // Only pages of collection datasets get detail records, so only they carry data-bukit-id
        // and only they count toward taxonomy terms.
        var collectionSources = options.Datasets
            .Where(dataset => dataset.Kind == MiniProgramKinds.Collection)
            .Select(dataset => dataset.Source!)
            .ToArray();
        var contentRecords = CollectContentRecords(context, siteUrl, collectionSources);
        var internalIds = contentRecords
            .GroupBy(record => record.PageUrl!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
        using var sanitizer = new MiniProgramHtmlSanitizer(siteUrl, context.BaseUrl, internalIds);
        // Each file is tracked as it is written, so a later failure leaves no unowned output behind.
        var outputs = context.GetOrAdd(BuildDataKeys.PluginOutputs, static () => []);
        var run = new Run(
            context,
            siteUrl,
            contentRecords,
            dataModules,
            new MiniProgramDatasetBuilder(siteUrl, context.BaseUrl, sanitizer),
            new FileSet(context.OutputDir, options.OutputDir, siteUrl, context.BaseUrl, path => outputs.Add(new PluginOutputTrackingInfo(Name, "after-build", path))),
            Multilingual: (_config.Site.Languages?.Count ?? 1) > 1);
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
    }

    private async Task<JsonObject> WriteDatasetAsync(Run run, MiniProgramDatasetConfig dataset, int maxListItems, CancellationToken cancellationToken)
        => dataset.Kind switch
        {
            MiniProgramKinds.Collection => await WriteCollectionAsync(run, dataset, maxListItems, cancellationToken),
            MiniProgramKinds.Module or MiniProgramKinds.Records => await WriteDataRecordsAsync(run, dataset, cancellationToken),
            MiniProgramKinds.DataIndex => run.Writer.Write(dataset.Name, new JsonObject { ["values"] = run.Builder.BuildMap(dataset, ResolveDataIndex(run, dataset)) }, dataset.Kind),
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
        FileSet Writer,
        bool Multilingual)
    {
        internal List<DateTimeOffset> Dates { get; } = [];

        /// <summary>
        /// Fails when a module or scope matches no row at all, enabled or not: that is a typo, and an
        /// empty file would wipe every client's copy. A multilingual site warns instead, because data
        /// rows with a locale exist only in their own language.
        /// </summary>
        internal void RequireMatched(MiniProgramDatasetConfig dataset, string field, string value, bool matched)
        {
            if (matched)
            {
                return;
            }

            if (!Multilingual)
            {
                throw MiniProgramDatasetBuilder.Invalid(dataset, "*", field, $"'{value}' matches no record of source '{dataset.Source}'");
            }

            Context.Logger.Warn($"event=miniprogram.dataset_empty dataset={dataset.Name} {field}={value} source={dataset.Source}");
        }
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

    private static List<MiniProgramRecord> CollectContentRecords(BuildContext context, string siteUrl, string[] collectionSources)
    {
        var records = new List<MiniProgramRecord>();
        foreach (var routed in context.RoutedDocuments)
        {
            if (routed.Route.Template == RedirectPageHtml.RouteTemplate || !MiniProgramDatasetBuilder.IsPublic(routed.Document) ||
                !collectionSources.Any(source => MiniProgramDatasetBuilder.IsFromSource(routed.Document, source)) ||
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
        var records = await WithBodiesAsync(run, dataset, selected, needsBody, cancellationToken);
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
        var matchedModule = false;
        foreach (var document in run.DataModules?.DataDocuments ?? [])
        {
            if (!MiniProgramDatasetBuilder.IsFromSource(document, dataset.Source!) ||
                dataset.Kind == MiniProgramKinds.Module &&
                !string.Equals(ContentFieldReader.GetContentType(document).Trim(), dataset.Module, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matchedModule = true;
            if (!MiniProgramDatasetBuilder.IsPublic(document))
            {
                continue;
            }

            // Data records carry no public route; an opaque hash keeps source page ids out of public files.
            selected.Add(new MiniProgramRecord(MiniProgramJson.Hash(Encoding.UTF8.GetBytes(document.Id))[..16], document, PageUrl: null, BodyHtml: null));
        }

        // Rows that exist but are all disabled still produce an intentionally empty dataset.
        if (dataset.Kind == MiniProgramKinds.Module)
        {
            run.RequireMatched(dataset, "module", dataset.Module!, matchedModule);
        }

        var records = await WithBodiesAsync(run, dataset, selected, dataset.Fields!.Contains("body"), cancellationToken);
        run.Dates.AddRange(records.Select(record => record.Document.Record.Lifecycle.UpdatedAt ?? record.Document.PublishAt));
        return run.Writer.Write(dataset.Name, new JsonObject { ["items"] = run.Builder.BuildItems(dataset, records, dataset.Fields!) }, dataset.Kind);
    }

    private static async Task<List<MiniProgramRecord>> WithBodiesAsync(
        Run run,
        MiniProgramDatasetConfig dataset,
        List<MiniProgramRecord> records,
        bool needsBody,
        CancellationToken cancellationToken)
    {
        if (!needsBody)
        {
            return records;
        }

        var resolved = new List<MiniProgramRecord>(records.Count);
        foreach (var record in records)
        {
            var html = await ContentBodyResolver.GetHtmlAsync(record.Document, run.Context.BodyStore, cancellationToken).ConfigureAwait(false);
            resolved.Add(run.Builder.WithBody(dataset, record, html));
        }

        return resolved;
    }

    private IReadOnlyDictionary<string, object> ResolveDataIndex(Run run, MiniProgramDatasetConfig dataset)
    {
        if (run.DataModules?.DataIndex is { } index &&
            index.TryGetValue(dataset.Source!, out var source) &&
            source is IReadOnlyDictionary<string, object> scopes &&
            scopes.TryGetValue(dataset.Scope!, out var scope) &&
            scope is IReadOnlyDictionary<string, object> values)
        {
            return values;
        }

        // The index drops disabled rows, so look for the scope on every row of the source:
        // all rows disabled is an intentionally empty scope, no row at all is a typo.
        var scopeField = _config.Content.Sources!
            .First(candidate => string.Equals(candidate.Name?.Trim(), dataset.Source, StringComparison.OrdinalIgnoreCase))
            .DataIndex!.ScopeField;
        var matched = (run.DataModules?.DataDocuments ?? []).Any(document =>
            MiniProgramDatasetBuilder.IsFromSource(document, dataset.Source!) &&
            string.Equals(ContentFieldReader.GetText(document.CustomFields, scopeField)?.Trim(), dataset.Scope, StringComparison.Ordinal));
        run.RequireMatched(dataset, "scope", dataset.Scope!, matched);
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static JsonArray BuildTaxonomy(MiniProgramDatasetConfig dataset, IReadOnlyList<MiniProgramRecord> contentRecords)
    {
        // Terms that differ only in case are one term, as on the website; the ordinal-first spelling names it.
        var counts = new Dictionary<string, (string Name, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in contentRecords)
        {
            var terms = dataset.Taxonomy switch
            {
                "tags" => record.Document.Record.Classification.Tags,
                "categories" => record.Document.Record.Classification.Sections,
                _ => ContentFieldReader.GetTextList(record.Document.CustomFields, dataset.Taxonomy!) ?? []
            };
            foreach (var term in terms.Where(term => !string.IsNullOrWhiteSpace(term)).Select(term => term.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var current = counts.TryGetValue(term, out var existing) ? existing : (Name: term, Count: 0);
                var name = StringComparer.Ordinal.Compare(term, current.Name) < 0 ? term : current.Name;
                counts[term] = (name, current.Count + 1);
            }
        }

        var items = new JsonArray();
        foreach (var (name, count) in counts.Values.OrderBy(pair => pair.Name, StringComparer.Ordinal))
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

    /// <summary>Writes content-addressed files under the variant's mini-program directory and reports each path.</summary>
    private sealed class FileSet(string outputDir, string directory, string siteUrl, string baseUrl, Action<string> track)
    {
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
            track(relative);
            File.WriteAllBytes(path, bytes);
        }
    }
}
