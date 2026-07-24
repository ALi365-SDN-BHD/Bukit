using System.Text;
using System.Text.Json;
using Bukit.Notion.Transport;
using Bukit.Notion.Write;

namespace Bukit.Importing;

public static class ImportNotionPushWorkflow
{
    private static Func<HttpClient> _createHttpClient = () => new HttpClient();

    /// <summary>
    /// Factory for creating HttpClient instances. Thread-safe setter for test seams.
    /// </summary>
    internal static Func<HttpClient> CreateHttpClient
    {
#pragma warning disable CS8601 // Possible null reference assignment.
        get => Interlocked.CompareExchange(ref _createHttpClient, null, null);
#pragma warning restore CS8601
        set => Interlocked.Exchange(ref _createHttpClient, value);
    }

    public static Task<int> PushGeneratedSeedAsync(ImportGeneratedNotionPushOptions options)
    {
        if (options.DryRun)
        {
            Console.Error.WriteLine("--push-notion cannot be used with --dry-run. Generate first, then push.");
            return Task.FromResult(2);
        }

        if (!options.GenerateSeed)
        {
            Console.Error.WriteLine("--push-notion requires seed data. Do not use --no-seed.");
            return Task.FromResult(2);
        }

        if (options.CreateMissingDatabases && string.IsNullOrWhiteSpace(options.ParentPageId))
        {
            Console.Error.WriteLine("--create-missing-notion-databases requires --notion-parent-page-id <id>.");
            return Task.FromResult(2);
        }

        var siteDir = string.IsNullOrWhiteSpace(options.ImportResult.SitePath)
            ? Path.Combine(options.RootDir, "sites", options.ThemeName)
            : options.ImportResult.SitePath;

        var seedDir = options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(siteDir, "notion-seed")
            : Path.Combine(siteDir, "data");
        var effectiveDatabaseMap = options.DatabaseMap;
        if (string.IsNullOrWhiteSpace(options.DatabaseId) &&
            string.IsNullOrWhiteSpace(effectiveDatabaseMap) &&
            options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            var defaultMap = Path.Combine(seedDir, "notion-database-map.yaml");
            if (File.Exists(defaultMap))
                effectiveDatabaseMap = defaultMap;
        }

        string? resolvedDatabaseMap = null;
        if (!string.IsNullOrWhiteSpace(effectiveDatabaseMap))
        {
            resolvedDatabaseMap = Path.IsPathRooted(effectiveDatabaseMap)
                ? effectiveDatabaseMap
                : Path.Combine(siteDir, effectiveDatabaseMap);
            if (!options.CreateMissingDatabases && NotionDatabaseMapReader.DatabaseMapHasMissingDatabaseIds(resolvedDatabaseMap, seedDir))
            {
                Console.Error.WriteLine("Notion database map exists but one or more databaseId values are empty.");
                Console.Error.WriteLine("Use --create-missing-notion-databases --notion-parent-page-id <id>, or fill databaseId in the map.");
                return Task.FromResult(2);
            }
        }

        var resolvedGeneratedMap = string.IsNullOrWhiteSpace(options.GeneratedDatabaseMap)
            ? null
            : Path.IsPathRooted(options.GeneratedDatabaseMap)
                ? options.GeneratedDatabaseMap
                : Path.Combine(siteDir, options.GeneratedDatabaseMap);
        var resolvedReport = string.IsNullOrWhiteSpace(options.ReportPath)
            ? null
            : Path.IsPathRooted(options.ReportPath)
                ? options.ReportPath
                : Path.Combine(siteDir, options.ReportPath);

        return PushSeedDirectoryAsync(new ImportNotionSeedPushOptions
        {
            InputDir = seedDir,
            DatabaseId = options.DatabaseId,
            DatabaseMapPath = resolvedDatabaseMap,
            CreateMissingDatabases = options.CreateMissingDatabases,
            ParentPageId = options.ParentPageId,
            GeneratedDatabaseMapPath = resolvedGeneratedMap,
            TokenEnv = options.TokenEnv,
            Mode = "upsert",
            UniqueField = "Slug",
            UpdateContent = "replace",
            DryRun = false,
            ReportPath = resolvedReport,
            ValidateSchema = options.ValidateSchema
        });
    }

    public static async Task<int> ValidateSchemaAsync(ImportNotionSchemaValidationOptions options)
    {
        var databaseId = options.DatabaseId;
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            Console.Error.WriteLine("Missing required option: --database-id <id>");
            return 2;
        }

        var token = Environment.GetEnvironmentVariable(options.TokenEnv);
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine($"{options.TokenEnv} is required for notion validate-schema.");
            return 2;
        }

        var reportPath = string.IsNullOrWhiteSpace(options.ReportPath)
            ? null
            : Path.GetFullPath(options.ReportPath);

        using var http = CreateHttpClient();
        using var transport = CreateTransport(token, http);
        var client = new NotionWriteClient(transport);
        var report = await NotionSchemaValidator.ValidateAsync(client, databaseId, reportPath);

        Console.WriteLine($"schema validation: {(report.Success ? "PASSED" : "FAILED")}");
        foreach (var f in report.FieldResults)
            Console.WriteLine($"  {f.Name,-18} {f.ExpectedType,-10} {f.Result}");
        if (!report.Success)
        {
            foreach (var e in report.Errors)
                Console.Error.WriteLine($"  ERROR: {e}");
            return 1;
        }

        return 0;
    }

    public static async Task<int> PushSeedDirectoryAsync(ImportNotionSeedPushOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputDir))
        {
            Console.Error.WriteLine("Missing required option: --input <seed-dir>");
            return 2;
        }

        var inputDir = Path.GetFullPath(options.InputDir);
        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"Seed directory does not exist: {inputDir}");
            return 2;
        }

        var mode = options.Mode;
        if (mode is not ("create" or "upsert"))
        {
            Console.Error.WriteLine($"Unsupported push mode: {mode}. Available: create | upsert");
            return 2;
        }

        var updateContent = options.UpdateContent;
        if (updateContent is not ("" or "append" or "replace"))
        {
            Console.Error.WriteLine($"Unsupported --update-content value: {updateContent}. Available: append | replace");
            return 2;
        }

        if (!options.DryRun && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(options.TokenEnv)))
        {
            Console.Error.WriteLine($"{options.TokenEnv} is required for notion push. Use --dry-run to generate a local review plan.");
            return 2;
        }

        var reportPath = options.ReportPath;
        if (string.IsNullOrWhiteSpace(reportPath))
            reportPath = Path.Combine(inputDir, options.DryRun ? "notion-push-plan.json" : "notion-push-report.json");
        reportPath = Path.GetFullPath(reportPath);

        if (string.IsNullOrWhiteSpace(options.DatabaseMapPath) && !string.IsNullOrWhiteSpace(options.DatabaseId))
        {
            return await PushSingleDatabaseAsync(
                inputDir,
                options.DatabaseId!,
                options.TokenEnv,
                mode,
                options.UniqueField,
                updateContent,
                options.DryRun,
                reportPath,
                options.ValidateSchema);
        }

        var databaseMapPath = options.DatabaseMapPath;
        if (string.IsNullOrWhiteSpace(databaseMapPath))
        {
            var defaultMapPath = Path.Combine(inputDir, "notion-database-map.yaml");
            if (File.Exists(defaultMapPath))
                databaseMapPath = defaultMapPath;
        }

        List<NotionDatabaseTarget> targets;
        try
        {
            targets = string.IsNullOrWhiteSpace(databaseMapPath)
                ? BuildDefaultDatabaseTargets(inputDir, options.UniqueField)
                : NotionDatabaseMapReader.ReadDatabaseMap(Path.GetFullPath(databaseMapPath), inputDir, options.UniqueField);
            targets = PrepareTargets(inputDir, targets);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            var duplicatePrefix = "Duplicate key ";
            var detail = ex.Message.StartsWith(duplicatePrefix, StringComparison.Ordinal)
                ? $"Duplicate schema field '{ex.Message[duplicatePrefix.Length..]}'."
                : $"Invalid Notion database map YAML: {ex.Message}";
            var mapLocation = string.IsNullOrWhiteSpace(databaseMapPath)
                ? inputDir
                : Path.GetFullPath(databaseMapPath);
            Console.Error.WriteLine($"{mapLocation}: {detail}");
            return 2;
        }

        if (targets.Count == 0)
        {
            Console.Error.WriteLine($"No pushable seed files found: {inputDir}");
            return 2;
        }

        var missingTargets = targets.Where(t => string.IsNullOrWhiteSpace(t.DatabaseId)).ToList();
        if (missingTargets.Count > 0 && !options.CreateMissingDatabases)
        {
            Console.Error.WriteLine("Missing databaseId. Provide databaseId in --database-map, or use --create-missing-databases --parent-page-id <id>.");
            foreach (var target in missingTargets)
                Console.Error.WriteLine($"  {target.Key}: {target.SeedFile}");
            return 2;
        }
        if (missingTargets.Count > 0 && string.IsNullOrWhiteSpace(options.ParentPageId))
        {
            Console.Error.WriteLine("--create-missing-databases requires --parent-page-id <id>.");
            return 2;
        }

        using var http = CreateHttpClient();
        var token = Environment.GetEnvironmentVariable(options.TokenEnv) ?? "";
        using var transport = CreateTransport(options.DryRun ? "dry-run" : token, http);
        var client = new NotionWriteClient(transport);
        var completedTargets = new List<NotionDatabaseTarget>();
        var pushResults = new List<(NotionDatabaseTarget Target, NotionPushResult Result)>();
        var failed = false;

        foreach (var target in targets)
        {
            var activeTarget = target;
            var records = ImportSeedRecordReader.ReadSeedFile(inputDir, activeTarget.SeedFile, activeTarget.Collection);
            var additionalSchemaFields = activeTarget.Schema!
                .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
                .Select(f => (Name: f.Key, ExpectedType: f.Value))
                .ToArray();
            if (string.IsNullOrWhiteSpace(activeTarget.DatabaseId))
            {
                if (options.DryRun)
                {
                    completedTargets.Add(activeTarget);
                    pushResults.Add((activeTarget, BuildDryRunResult(inputDir, activeTarget)));
                    continue;
                }

                var createdDatabaseId = await CreateDatabaseAsync(
                    client,
                    options.ParentPageId!,
                    activeTarget.Title,
                    additionalSchemaFields);
                if (string.IsNullOrWhiteSpace(createdDatabaseId))
                {
                    failed = true;
                    pushResults.Add((activeTarget, new NotionPushResult(0, 0, 0, 0, 1, [])));
                    continue;
                }
                activeTarget = activeTarget with { DatabaseId = createdDatabaseId };
                if (options.ValidateSchema)
                {
                    var schemaReport = await NotionSchemaValidator.ValidateAsync(
                        client,
                        activeTarget.DatabaseId!,
                        null,
                        additionalSchemaFields);
                    if (!schemaReport.Success)
                    {
                        failed = true;
                        Console.Error.WriteLine($"Notion schema validation failed for {activeTarget.Key} ({activeTarget.DatabaseId}):");
                        foreach (var f in schemaReport.FieldResults.Where(r => r.Result != "OK"))
                            Console.Error.WriteLine($"  {f.Name}: {f.Result} - {f.Message}");
                        pushResults.Add((activeTarget, new NotionPushResult(0, 0, 0, 0, 1, [])));
                        completedTargets.Add(activeTarget);
                        continue;
                    }
                }
            }
            else if (!options.DryRun && options.ValidateSchema)
            {
                var schemaReport = await NotionSchemaValidator.ValidateAsync(
                    client,
                    activeTarget.DatabaseId!,
                    null,
                    additionalSchemaFields);
                if (!schemaReport.Success)
                {
                    failed = true;
                    Console.Error.WriteLine($"Notion schema validation failed for {activeTarget.Key} ({activeTarget.DatabaseId}):");
                    foreach (var f in schemaReport.FieldResults.Where(r => r.Result != "OK"))
                        Console.Error.WriteLine($"  {f.Name}: {f.Result} - {f.Message}");
                    pushResults.Add((activeTarget, new NotionPushResult(0, 0, 0, 0, 1, [])));
                    completedTargets.Add(activeTarget);
                    continue;
                }
            }

            var result = await NotionSeedPusher.PushAsync(client, records, new NotionPushOptions(
                DatabaseId: activeTarget.DatabaseId ?? "",
                ReportPath: reportPath,
                DryRun: options.DryRun,
                Mode: mode,
                UniqueField: activeTarget.UniqueField,
                UpdateContent: updateContent,
                WriteReport: false,
                Schema: activeTarget.Schema));
            if (result.Failed > 0) failed = true;
            completedTargets.Add(activeTarget);
            pushResults.Add((activeTarget, result));
        }

        WriteMultiDatabaseReport(reportPath, options.DryRun, completedTargets, pushResults);
        if (options.CreateMissingDatabases || !string.IsNullOrWhiteSpace(options.GeneratedDatabaseMapPath))
        {
            var mapOutputPath = NotionDatabaseMapReader.ResolveGeneratedMapPath(inputDir, databaseMapPath, options.GeneratedDatabaseMapPath);
            NotionDatabaseMapReader.WriteDatabaseMap(mapOutputPath, completedTargets);
        }

        var totalRecords = pushResults.Sum(r => r.Result.Total);
        var totalCreated = pushResults.Sum(r => r.Result.Created);
        var totalUpdated = pushResults.Sum(r => r.Result.Updated);
        var totalFailed = pushResults.Sum(r => r.Result.Failed);
        Console.WriteLine($"notion push {(options.DryRun ? "dry-run" : "api")} complete: databases={completedTargets.Count} records={totalRecords} created={totalCreated} updated={totalUpdated} failed={totalFailed} report={reportPath}");
        if (failed)
        {
            Console.Error.WriteLine("Notion push failed for one or more databases. See report for details.");
            return 1;
        }

        return 0;
    }

    private static async Task<int> PushSingleDatabaseAsync(
        string inputDir,
        string databaseId,
        string tokenEnv,
        string mode,
        string uniqueField,
        string updateContent,
        bool dryRun,
        string reportPath,
        bool validateSchema)
    {
        var records = ImportSeedRecordReader.ReadDirectory(inputDir);
        var additionalSchemaFields = NotionSchemaTypeValidator.BuildAdditionalSchemaFields(collection: "", records);
        var effectiveSchema = additionalSchemaFields
            .ToDictionary(field => field.Name, field => field.ExpectedType, StringComparer.OrdinalIgnoreCase);
        try
        {
            NotionSchemaTypeValidator.ValidateTypedValues(databaseId, records, effectiveSchema);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        using var http = CreateHttpClient();
        var token = Environment.GetEnvironmentVariable(tokenEnv) ?? "";
        using var transport = CreateTransport(dryRun ? "dry-run" : token, http);
        var client = new NotionWriteClient(transport);
        if (!dryRun && validateSchema)
        {
            var schemaReport = await NotionSchemaValidator.ValidateAsync(
                client,
                databaseId,
                null,
                additionalSchemaFields);
            if (!schemaReport.Success)
            {
                Console.Error.WriteLine($"Notion schema validation failed for {databaseId}:");
                foreach (var f in schemaReport.FieldResults.Where(r => r.Result != "OK"))
                    Console.Error.WriteLine($"  {f.Name}: {f.Result} - {f.Message}");
                return 2;
            }
        }

        var result = await NotionSeedPusher.PushAsync(client, records, new NotionPushOptions(
            DatabaseId: databaseId,
            ReportPath: reportPath,
            DryRun: dryRun,
            Mode: mode,
            UniqueField: uniqueField,
            UpdateContent: updateContent,
            Schema: effectiveSchema));
        Console.WriteLine($"notion push {(dryRun ? "dry-run" : "api")} complete: records={result.Total} created={result.Created} updated={result.Updated} failed={result.Failed} report={reportPath}");
        if (result.Failed > 0)
        {
            Console.Error.WriteLine("Notion push failed for one or more records. See report for details.");
            return 1;
        }

        return 0;
    }

    private static List<NotionDatabaseTarget> BuildDefaultDatabaseTargets(string inputDir, string uniqueField)
    {
        var targets = new List<NotionDatabaseTarget>();
        foreach (var (fileBase, collection) in ImportSeedRecordReader.KnownFiles)
        {
            var seedFile = $"{fileBase}.json";
            if (!File.Exists(Path.Combine(inputDir, seedFile)))
            {
                var yamlSeedFile = $"{fileBase}.yaml";
                if (!File.Exists(Path.Combine(inputDir, yamlSeedFile)))
                {
                    var ymlSeedFile = $"{fileBase}.yml";
                    if (!File.Exists(Path.Combine(inputDir, ymlSeedFile)))
                        continue;
                    seedFile = ymlSeedFile;
                }
                else
                {
                    seedFile = yamlSeedFile;
                }
            }

            targets.Add(new NotionDatabaseTarget(
                Key: fileBase,
                Title: NotionDatabaseMapReader.ToTitle(fileBase),
                SeedFile: seedFile,
                Collection: collection,
                DatabaseId: null,
                UniqueField: uniqueField));
        }
        return targets;
    }

    private static List<NotionDatabaseTarget> PrepareTargets(string inputDir, List<NotionDatabaseTarget> targets)
    {
        var prepared = new List<NotionDatabaseTarget>(targets.Count);
        foreach (var target in targets)
        {
            var records = ImportSeedRecordReader.ReadSeedFile(inputDir, target.SeedFile, target.Collection);
            var schema = NotionSchemaTypeValidator.BuildAdditionalSchemaFields(target.Collection, records)
                .ToDictionary(f => f.Name, f => f.ExpectedType, StringComparer.OrdinalIgnoreCase);
            if (target.Schema is not null)
            {
                foreach (var field in target.Schema)
                    schema[field.Key] = field.Value;
            }
            NotionSchemaTypeValidator.ValidateTypedValues(target.Key, records, schema);
            prepared.Add(target with { Schema = schema });
        }
        return prepared;
    }

    private static async Task<string?> CreateDatabaseAsync(
        NotionWriteClient client,
        string parentPageId,
        string title,
        IReadOnlyList<(string Name, string ExpectedType)> additionalSchemaFields)
    {
        var response = await client.CreateDatabaseAsync(
            BuildCreateDatabasePayload(parentPageId, title, additionalSchemaFields));
        if (!response.IsSuccess)
        {
            Console.Error.WriteLine(
                $"Notion database create failed for {title}: " +
                (response.ErrorMessage ?? response.ReasonPhrase ?? "Notion request failed."));
            return null;
        }

        return response.Payload is { } payload && payload.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    private static NotionClient CreateTransport(string token, HttpClient http)
        => new(
            new NotionClientOptions
            {
                Token = token,
                MaxRetries = 0
            },
            http);

    private static string BuildCreateDatabasePayload(
        string parentPageId,
        string title,
        IReadOnlyList<(string Name, string ExpectedType)> additionalSchemaFields)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteStartObject("parent");
        writer.WriteString("type", "page_id");
        writer.WriteString("page_id", parentPageId);
        writer.WriteEndObject();
        writer.WriteStartArray("title");
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteStartObject("text");
        writer.WriteString("content", title);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteStartObject("properties");
        WriteDatabaseProperty(writer, "Title", "title");
        WriteDatabaseProperty(writer, "Slug", "rich_text");
        WriteDatabaseProperty(writer, "Type", "select");
        WriteDatabaseProperty(writer, "Summary", "rich_text");
        WriteDatabaseProperty(writer, "Language", "select");
        WriteDatabaseProperty(writer, "Published", "checkbox");
        WriteDatabaseProperty(writer, "SeoTitle", "rich_text");
        WriteDatabaseProperty(writer, "SeoDescription", "rich_text");
        foreach (var (name, expectedType) in additionalSchemaFields)
            WriteDatabaseProperty(writer, name, expectedType);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteDatabaseProperty(Utf8JsonWriter writer, string name, string type)
    {
        writer.WriteStartObject(name);
        writer.WriteStartObject(type);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static NotionPushResult BuildDryRunResult(string inputDir, NotionDatabaseTarget target)
    {
        var records = ImportSeedRecordReader.ReadSeedFile(inputDir, target.SeedFile, target.Collection);
        var items = records.Select(r => new NotionPushItemResult(r, "review", true, null, null)).ToList();
        return new NotionPushResult(items.Count, 0, 0, 0, 0, items);
    }

    private static void WriteMultiDatabaseReport(
        string reportPath,
        bool dryRun,
        IReadOnlyList<NotionDatabaseTarget> targets,
        IReadOnlyList<(NotionDatabaseTarget Target, NotionPushResult Result)> results)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteBoolean("dryRun", dryRun);
        writer.WriteNumber("databaseCount", targets.Count);
        writer.WriteNumber("recordCount", results.Sum(r => r.Result.Total));
        writer.WriteNumber("created", results.Sum(r => r.Result.Created));
        writer.WriteNumber("updated", results.Sum(r => r.Result.Updated));
        writer.WriteNumber("failed", results.Sum(r => r.Result.Failed));
        writer.WriteStartArray("databases");
        foreach (var (target, result) in results)
        {
            writer.WriteStartObject();
            writer.WriteString("key", target.Key);
            writer.WriteString("title", target.Title);
            writer.WriteString("seedFile", target.SeedFile);
            writer.WriteString("collection", target.Collection);
            writer.WriteString("databaseId", target.DatabaseId);
            writer.WriteNumber("recordCount", result.Total);
            writer.WriteNumber("created", result.Created);
            writer.WriteNumber("updated", result.Updated);
            writer.WriteNumber("failed", result.Failed);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        File.WriteAllText(reportPath, Encoding.UTF8.GetString(stream.ToArray()));
    }
}
