using System.Text;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Content;
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

    internal MiniProgramPlugin(AppConfig config, BuildExecutionMode executionMode)
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
        var urls = MiniProgramUrls.For(context, _config, siteUrl);
        using var sanitizer = new MiniProgramHtmlSanitizer(urls, internalIds);
        // Each file is tracked as it is written, so a later failure leaves no unowned output behind.
        var outputs = context.GetOrAdd(BuildDataKeys.PluginOutputs, static () => []);
        var run = new Run(
            context,
            urls,
            contentRecords,
            dataModules,
            new MiniProgramDatasetBuilder(urls, sanitizer),
            new FileSet(context.OutputDir, options.OutputDir, siteUrl, context.BaseUrl, path => outputs.Add(new PluginOutputTrackingInfo(Name, "after-build", path))));
        var datasets = new SortedDictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            ["site"] = run.Writer.Write("site", BuildSite(urls), "site")
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
            MiniProgramKinds.Menu => run.Writer.Write(dataset.Name, new JsonObject { ["items"] = BuildMenu(_config.Site.Menus![dataset.Menu!], run.Urls) }, dataset.Kind),
            _ => throw new InvalidOperationException($"Unsupported mini-program dataset kind '{dataset.Kind}'.")
        };

    /// <summary>State shared by the dataset writers of one variant build.</summary>
    private sealed record Run(
        BuildContext Context,
        MiniProgramUrls Urls,
        IReadOnlyList<MiniProgramRecord> ContentRecords,
        DataModuleResult? DataModules,
        MiniProgramDatasetBuilder Builder,
        FileSet Writer)
    {
        internal List<DateTimeOffset> Dates { get; } = [];

        /// <summary>
        /// Called when no row of this language matches a module or scope. The content graph holds the
        /// rows of every language: no match in any site language is a typo (including a row whose
        /// locale names no site language), and an empty file would wipe every client's copy, so the
        /// build fails. A match in another language only means this language has no rows of its own,
        /// so it gets an empty dataset and a warning.
        /// </summary>
        internal void RequireMatchInAnyLanguage(AppConfig config, MiniProgramDatasetConfig dataset, string field, string value, Func<ContentDocument, bool> matches)
        {
            var documents = Context.ContentGraph.Documents;
            var languages = I18nOutputMerger.GetLanguages(config.Site);
            var defaultLanguage = languages.Count == 0 ? null : I18nOutputMerger.GetDefaultLanguage(config.Site, languages);
            var visible = defaultLanguage is null
                ? documents
                : languages.SelectMany(language => I18nOutputMerger.FilterDocumentsByLanguage(documents, language, defaultLanguage));
            if (!visible.Any(matches))
            {
                throw MiniProgramDatasetBuilder.Invalid(dataset, "*", field, $"'{value}' matches no record of source '{dataset.Source}' in any site language");
            }

            Context.Logger.Warn($"event=miniprogram.dataset_empty dataset={dataset.Name} {field}={value} source={dataset.Source} reason=no_rows_in_language");
        }
    }

    private JsonObject BuildCatalog(SortedDictionary<string, JsonObject> datasets, List<DateTimeOffset> dates)
    {
        var entries = new JsonObject();
        foreach (var (name, entry) in datasets)
        {
            entries[name] = entry;
        }

        var catalog = new JsonObject
        {
            ["schema"] = CatalogSchema,
            ["schemaVersion"] = SchemaVersion,
            ["minClientSchema"] = SchemaVersion,
            ["language"] = _config.Site.Language,
            ["datasets"] = entries
        };

        // The hash covers every field a client acts on (versions, language, file hashes and URLs), so a
        // moved output directory or a raised minClientSchema reaches clients even when no data changed.
        var catalogHash = "sha256:" + MiniProgramJson.Hash(MiniProgramJson.Serialize(catalog));
        catalog.Remove("datasets");
        catalog["generatedAt"] = MiniProgramJson.FormatDate(dates.Count == 0 ? DateTimeOffset.UnixEpoch : dates.Max());
        catalog["catalogHash"] = catalogHash;
        catalog["datasets"] = entries;
        return catalog;
    }

    private JsonObject BuildSite(MiniProgramUrls urls)
    {
        var languages = new JsonArray();
        foreach (var language in _config.Site.Languages ?? [_config.Site.Language])
        {
            languages.Add((JsonNode?)language);
        }

        return new JsonObject
        {
            ["name"] = _config.Site.Name,
            ["title"] = _config.Site.Title,
            ["description"] = _config.Site.Description,
            ["url"] = urls.SiteRoot,
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
                // Escaped like every other published URL, so a page's url matches body links to it.
                MiniProgramUrls.Escape(SeoModelBuilder.BuildAbsoluteUrl(siteUrl, context.BaseUrl, routed.Route.Url)),
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
            details.Add((JsonNode)new JsonObject
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

    private async Task<JsonObject> WriteDataRecordsAsync(Run run, MiniProgramDatasetConfig dataset, CancellationToken cancellationToken)
    {
        var selected = new List<MiniProgramRecord>();
        var matchedModule = false;
        bool Matches(ContentDocument document) =>
            MiniProgramDatasetBuilder.IsFromSource(document, dataset.Source!) &&
            (dataset.Kind != MiniProgramKinds.Module ||
             string.Equals(ContentFieldReader.GetContentType(document).Trim(), dataset.Module, StringComparison.OrdinalIgnoreCase));
        foreach (var document in run.DataModules?.DataDocuments ?? [])
        {
            if (!Matches(document))
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
        if (dataset.Kind == MiniProgramKinds.Module && !matchedModule)
        {
            run.RequireMatchInAnyLanguage(_config, dataset, "module", dataset.Module!, Matches);
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

        // Body reads can hit disk or a remote store, so they run concurrently, as page rendering reads
        // them. Sanitizing shares one parser, so it stays sequential and in record order.
        var bodies = new string[records.Count];
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = PageRenderDispatcher.ComputeOptimalParallelism(records.Count, 0),
            CancellationToken = cancellationToken
        };
        await Parallel.ForAsync(0, records.Count, options, async (index, token) =>
            bodies[index] = await ContentBodyResolver.GetHtmlAsync(records[index].Document, run.Context.BodyStore, token).ConfigureAwait(false))
            .ConfigureAwait(false);

        var resolved = new List<MiniProgramRecord>(records.Count);
        for (var index = 0; index < records.Count; index++)
        {
            resolved.Add(run.Builder.WithBody(dataset, records[index], bodies[index]));
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
        // Match the scope through the same aliases DataModuleBuilder reads. These rows include disabled
        // ones the index never reads, so an ambiguous alias must not fail the build here.
        bool Matches(ContentDocument document) =>
            MiniProgramDatasetBuilder.IsFromSource(document, dataset.Source!) &&
            ConfiguredContentFieldReader.AliasMatches(document.CustomFields, scopeField)
                .Any(field => string.Equals(field.Value?.ToString()?.Trim(), dataset.Scope, StringComparison.Ordinal));
        if (!(run.DataModules?.DataDocuments ?? []).Any(Matches))
        {
            run.RequireMatchInAnyLanguage(_config, dataset, "scope", dataset.Scope!, Matches);
        }

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
            items.Add((JsonNode)new JsonObject { ["name"] = name, ["count"] = count });
        }

        return items;
    }

    private static JsonArray BuildMenu(IReadOnlyList<MenuConfig> items, MiniProgramUrls urls)
    {
        var array = new JsonArray();
        // Same order as the website's menus.json: by weight, declaration order breaking ties.
        foreach (var item in items.OrderBy(item => item.Weight))
        {
            var node = new JsonObject
            {
                ["title"] = item.Name,
                ["url"] = urls.Absolute(item.Url, urls.SiteRoot)
            };
            if (item.Children is { Count: > 0 })
            {
                node["children"] = BuildMenu(item.Children, urls);
            }

            array.Add((JsonNode)node);
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
