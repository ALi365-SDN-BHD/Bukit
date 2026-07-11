using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bukit.Shared.Notion;
using YamlDotNet.RepresentationModel;

namespace Bukit.Importing;

public static class ImportNotionPushWorkflow
{
    internal static Func<HttpClient> CreateHttpClient { get; set; } = () => new HttpClient();

    public static Task<int> PushGeneratedSeedAsync(ImportGeneratedNotionPushOptions options)
    {
        if (options.DryRun)
        {
            Console.Error.WriteLine("--push-notion 不能与 --dry-run 同时使用。先生成草稿后再执行实际推送。");
            return Task.FromResult(2);
        }

        if (!options.GenerateSeed)
        {
            Console.Error.WriteLine("--push-notion 需要 seed 数据。请不要同时使用 --no-seed。");
            return Task.FromResult(2);
        }

        if (options.CreateMissingDatabases && string.IsNullOrWhiteSpace(options.ParentPageId))
        {
            Console.Error.WriteLine("--create-missing-notion-databases 需要 --notion-parent-page-id <id>。");
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
            if (!options.CreateMissingDatabases && DatabaseMapHasMissingDatabaseIds(resolvedDatabaseMap, seedDir))
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
            Console.Error.WriteLine("缺少必填选项: --database-id <id>");
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
        var report = await NotionSchemaValidator.ValidateAsync(http, databaseId, token, reportPath);

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
            Console.Error.WriteLine("缺少必填选项: --input <seed-dir>");
            return 2;
        }

        var inputDir = Path.GetFullPath(options.InputDir);
        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"seed 目录不存在: {inputDir}");
            return 2;
        }

        var mode = options.Mode;
        if (mode is not ("create" or "upsert"))
        {
            Console.Error.WriteLine($"不支持的推送模式: {mode}，可用: create | upsert");
            return 2;
        }

        var updateContent = options.UpdateContent;
        if (updateContent is not ("" or "append" or "replace"))
        {
            Console.Error.WriteLine($"不支持的 --update-content 值: {updateContent}，可用: append | replace");
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
                : ReadDatabaseMap(Path.GetFullPath(databaseMapPath), inputDir, options.UniqueField);
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
            Console.Error.WriteLine($"没有可推送的 seed 文件: {inputDir}");
            return 2;
        }

        var missingTargets = targets.Where(t => string.IsNullOrWhiteSpace(t.DatabaseId)).ToList();
        if (missingTargets.Count > 0 && !options.CreateMissingDatabases)
        {
            Console.Error.WriteLine("缺少 databaseId。请提供 --database-map 中的 databaseId，或使用 --create-missing-databases --parent-page-id <id> 自动创建。");
            foreach (var target in missingTargets)
                Console.Error.WriteLine($"  {target.Key}: {target.SeedFile}");
            return 2;
        }
        if (missingTargets.Count > 0 && string.IsNullOrWhiteSpace(options.ParentPageId))
        {
            Console.Error.WriteLine("--create-missing-databases 需要 --parent-page-id <id>。");
            return 2;
        }

        using var http = CreateHttpClient();
        var token = Environment.GetEnvironmentVariable(options.TokenEnv) ?? "";
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
                    http,
                    token,
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
                        http,
                        activeTarget.DatabaseId!,
                        token,
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
                    http,
                    activeTarget.DatabaseId!,
                    token,
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

            var result = await NotionSeedPusher.PushAsync(http, records, new NotionPushOptions(
                DatabaseId: activeTarget.DatabaseId ?? "",
                Token: token,
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
            var mapOutputPath = ResolveGeneratedMapPath(inputDir, databaseMapPath, options.GeneratedDatabaseMapPath);
            WriteDatabaseMap(mapOutputPath, completedTargets);
        }

        var totalRecords = pushResults.Sum(r => r.Result.Total);
        var totalCreated = pushResults.Sum(r => r.Result.Created);
        var totalUpdated = pushResults.Sum(r => r.Result.Updated);
        var totalFailed = pushResults.Sum(r => r.Result.Failed);
        Console.WriteLine($"notion push {(options.DryRun ? "dry-run" : "api")} 完成: databases={completedTargets.Count} records={totalRecords} created={totalCreated} updated={totalUpdated} failed={totalFailed} report={reportPath}");
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
        var additionalSchemaFields = BuildAdditionalSchemaFields(collection: "", records);
        using var http = CreateHttpClient();
        var token = Environment.GetEnvironmentVariable(tokenEnv) ?? "";
        if (!dryRun && validateSchema)
        {
            var schemaReport = await NotionSchemaValidator.ValidateAsync(
                http,
                databaseId,
                token,
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

        var result = await NotionSeedPusher.PushAsync(http, records, new NotionPushOptions(
            DatabaseId: databaseId,
            Token: token,
            ReportPath: reportPath,
            DryRun: dryRun,
            Mode: mode,
            UniqueField: uniqueField,
            UpdateContent: updateContent));
        Console.WriteLine($"notion push {(dryRun ? "dry-run" : "api")} 完成: records={result.Total} created={result.Created} updated={result.Updated} failed={result.Failed} report={reportPath}");
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
                Title: ToTitle(fileBase),
                SeedFile: seedFile,
                Collection: collection,
                DatabaseId: null,
                UniqueField: uniqueField));
        }
        return targets;
    }

    private static List<NotionDatabaseTarget> ReadDatabaseMap(string mapPath, string inputDir, string defaultUniqueField)
    {
        var stream = new YamlStream();
        using var reader = File.OpenText(mapPath);
        stream.Load(reader);
        if (stream.Documents.Count == 0 ||
            stream.Documents[0].RootNode is not YamlMappingNode root ||
            GetMap(root, "databases") is not { } databases)
            return [];

        var targets = new List<NotionDatabaseTarget>();
        foreach (var kv in databases.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value) ||
                kv.Value is not YamlMappingNode map)
                continue;

            var key = keyNode.Value.Trim();
            var seedFile = GetScalar(map, "seed") ?? $"{key}.json";
            var collection = GetScalar(map, "collection") ?? InferCollection(key, seedFile);
            targets.Add(new NotionDatabaseTarget(
                Key: key,
                Title: GetScalar(map, "title") ?? ToTitle(key),
                SeedFile: seedFile,
                Collection: collection,
                DatabaseId: GetScalar(map, "databaseId"),
                UniqueField: GetScalar(map, "uniqueField") ?? defaultUniqueField,
                Schema: ReadSchema(map, key, mapPath)));
        }
        return targets.Where(t => File.Exists(Path.Combine(inputDir, t.SeedFile))).ToList();
    }

    private static IReadOnlyDictionary<string, string>? ReadSchema(
        YamlMappingNode map,
        string databaseKey,
        string mapPath)
    {
        var schemaPath = $"{mapPath}:databases.{databaseKey}.schema";
        var schemaNode = GetNode(map, "schema");
        if (schemaNode is null)
            return null;
        if (schemaNode is not YamlMappingNode schemaMap)
            throw new FormatException($"{schemaPath}: schema must be a mapping.");

        var parsed = new List<(string Raw, string Canonical, string Type)>();
        var canonicalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in schemaMap.Children)
        {
            if (pair.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
                throw new FormatException($"{schemaPath}: schema contains an invalid field name.");
            var field = keyNode.Value;
            if (field != field.Trim())
                throw new FormatException($"{schemaPath}: schema key '{field}' must not contain boundary whitespace.");
            var canonical = ToNotionPropertyName(field);
            if (string.IsNullOrWhiteSpace(canonical))
                throw new FormatException($"{schemaPath}: Schema key '{field}' has an empty canonical Notion property name.");
            if (pair.Value is not YamlScalarNode typeNode || string.IsNullOrWhiteSpace(typeNode.Value))
                throw new FormatException($"{schemaPath}: Schema field '{field}' must declare a scalar type.");
            var type = typeNode.Value.Trim().ToLowerInvariant();
            if (type is not ("rich_text" or "select" or "multi_select" or "url" or "date" or "number" or "checkbox"))
                throw new FormatException($"{schemaPath}: Unsupported Notion schema type '{type}' for database '{databaseKey}', field '{field}'.");
            if (!canonicalKeys.Add(canonical))
                throw new FormatException($"{schemaPath}: schema keys normalize to duplicate Notion property '{canonical}'.");
            parsed.Add((field, canonical, type));
        }

        var schema = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (raw, canonical, type) in parsed)
        {
            if (!raw.Equals(canonical, StringComparison.Ordinal))
                throw new FormatException($"{schemaPath}: Schema key '{raw}' must use canonical Notion property name '{canonical}'.");
            if (IsCoreProperty(canonical))
                throw new FormatException($"{schemaPath}: Schema key '{raw}' conflicts with fixed core property '{canonical}'.");
            schema.Add(canonical, type);
        }
        return schema;
    }

    private static List<NotionDatabaseTarget> PrepareTargets(string inputDir, List<NotionDatabaseTarget> targets)
    {
        var prepared = new List<NotionDatabaseTarget>(targets.Count);
        foreach (var target in targets)
        {
            var records = ImportSeedRecordReader.ReadSeedFile(inputDir, target.SeedFile, target.Collection);
            var schema = BuildAdditionalSchemaFields(target.Collection, records)
                .ToDictionary(f => f.Name, f => f.ExpectedType, StringComparer.OrdinalIgnoreCase);
            if (target.Schema is not null)
            {
                foreach (var field in target.Schema)
                    schema[field.Key] = field.Value;
            }
            ValidateTypedValues(target.Key, records, target.Schema);
            prepared.Add(target with { Schema = schema });
        }
        return prepared;
    }

    private static void ValidateTypedValues(
        string databaseKey,
        IReadOnlyList<ImportSeedRecord> records,
        IReadOnlyDictionary<string, string>? declaredSchema)
    {
        if (declaredSchema is null)
            return;
        foreach (var record in records)
        {
            if (record.ExtraFields is null)
                continue;
            foreach (var (rawName, value) in record.ExtraFields)
            {
                var field = ToNotionPropertyName(rawName);
                if (value is null || !declaredSchema.TryGetValue(field, out var type))
                    continue;
                if (!IsCompatibleValue(type, value))
                    throw new FormatException($"Invalid typed Notion value in database '{databaseKey}', field '{field}', record '{record.Slug}': expected {type}.");
            }
        }
    }

    private static bool IsCompatibleValue(string type, object value)
        => type switch
        {
            "rich_text" or "select" => value is string,
            "multi_select" => value is IReadOnlyList<object?> items && items.All(item => item is string),
            "url" => value is string text && Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
            "date" => value is string text && IsIsoDate(text),
            "number" => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal,
            "checkbox" => value is bool,
            _ => false
        };

    private static bool IsIsoDate(string value)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _))
            return true;

        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
        };
        return DateTimeOffset.TryParseExact(value, formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _);
    }

    private static async Task<string?> CreateDatabaseAsync(
        HttpClient http,
        string token,
        string parentPageId,
        string title,
        IReadOnlyList<(string Name, string ExpectedType)> additionalSchemaFields)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{NotionApiUrls.Base}/{NotionApiUrls.ApiVersion}/databases");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Notion-Version", NotionApiUrls.NotionVersion);
        request.Content = new StringContent(
            BuildCreateDatabasePayload(parentPageId, title, additionalSchemaFields),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Notion database create failed for {title}: {body}");
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

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
        WriteDatabaseProperty(writer, "Content", "rich_text");
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

    private static IReadOnlyList<(string Name, string ExpectedType)> BuildAdditionalSchemaFields(
        string collection,
        IReadOnlyList<ImportSeedRecord> records)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (collection.Equals("navigation", StringComparison.OrdinalIgnoreCase))
        {
            fields["Link"] = "url";
            fields["Order"] = "number";
            fields["Enabled"] = "checkbox";
        }

        foreach (var record in records)
        {
            if (record.ExtraFields is null)
                continue;

            foreach (var (name, value) in record.ExtraFields)
            {
                var propertyName = ToNotionPropertyName(name);
                if (string.IsNullOrWhiteSpace(propertyName) || IsCoreProperty(propertyName) || value is null)
                    continue;

                fields.TryAdd(propertyName, ToNotionPropertyType(propertyName, value));
            }
        }

        return fields
            .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .Select(f => (f.Key, f.Value))
            .ToArray();
    }

    private static string ToNotionPropertyType(string propertyName, object value)
    {
        if (value is bool)
            return "checkbox";
        if (value is int or long or float or double or decimal)
            return "number";
        if (value is IReadOnlyList<object?>)
            return "multi_select";
        if (propertyName is "Link" or "Url" or "Href")
            return "url";
        return "rich_text";
    }

    private static string ToNotionPropertyName(string name)
        => name.Trim().ToLowerInvariant() switch
        {
            "link" => "Link",
            "url" => "Url",
            "href" => "Href",
            "order" or "sort_order" => "Order",
            "enabled" => "Enabled",
            _ => string.Concat(name.Trim().Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => char.ToUpperInvariant(p[0]) + p[1..]))
        };

    private static bool IsCoreProperty(string name)
        => name is "Title" or "Slug" or "Type" or "Summary" or "Content" or "Language" or
           "Published" or "SeoTitle" or "SeoDescription";

    private static NotionPushResult BuildDryRunResult(string inputDir, NotionDatabaseTarget target)
    {
        var records = ImportSeedRecordReader.ReadSeedFile(inputDir, target.SeedFile, target.Collection);
        var items = records.Select(r => new NotionPushItemResult(r, "review", true, null, null)).ToList();
        return new NotionPushResult(items.Count, 0, 0, 0, 0, items);
    }

    private static string ResolveGeneratedMapPath(string inputDir, string? databaseMapPath, string? generatedMapPath)
    {
        if (!string.IsNullOrWhiteSpace(generatedMapPath))
            return Path.GetFullPath(generatedMapPath);
        if (!string.IsNullOrWhiteSpace(databaseMapPath))
            return Path.GetFullPath(databaseMapPath);
        return Path.Combine(inputDir, "notion-database-map.generated.yaml");
    }

    private static void WriteDatabaseMap(string path, IReadOnlyList<NotionDatabaseTarget> targets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sb = new StringBuilder();
        sb.AppendLine("databases:");
        foreach (var target in targets)
        {
            sb.AppendLine($"  {target.Key}:");
            sb.AppendLine($"    title: {target.Title}");
            sb.AppendLine($"    seed: {target.SeedFile}");
            sb.AppendLine($"    collection: {target.Collection}");
            if (!string.IsNullOrWhiteSpace(target.DatabaseId))
                sb.AppendLine($"    databaseId: {target.DatabaseId}");
            sb.AppendLine($"    uniqueField: {target.UniqueField}");
            if (target.Schema is { Count: > 0 })
            {
                sb.AppendLine("    schema:");
                foreach (var field in target.Schema.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine($"      {field.Key}: {field.Value}");
            }
        }
        File.WriteAllText(path, sb.ToString());
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

    private static bool DatabaseMapHasMissingDatabaseIds(string databaseMapPath, string seedDir)
    {
        if (!File.Exists(databaseMapPath))
            return false;

        var stream = new YamlStream();
        using var reader = File.OpenText(databaseMapPath);
        stream.Load(reader);
        if (stream.Documents.Count == 0 ||
            stream.Documents[0].RootNode is not YamlMappingNode root ||
            root.Children.FirstOrDefault(kv =>
                kv.Key is YamlScalarNode scalar && scalar.Value == "databases").Value is not YamlMappingNode databases)
            return false;

        foreach (var kv in databases.Children)
        {
            if (kv.Key is not YamlScalarNode key ||
                string.IsNullOrWhiteSpace(key.Value) ||
                kv.Value is not YamlMappingNode database)
                continue;
            var seed = database.Children.FirstOrDefault(entry =>
                entry.Key is YamlScalarNode scalar && scalar.Value == "seed").Value is YamlScalarNode seedNode
                ? seedNode.Value
                : $"{key.Value.Trim()}.json";
            if (string.IsNullOrWhiteSpace(seed) || !File.Exists(Path.Combine(seedDir, seed)))
                continue;
            var id = database.Children.FirstOrDefault(entry =>
                entry.Key is YamlScalarNode scalar && scalar.Value == "databaseId").Value as YamlScalarNode;
            if (string.IsNullOrWhiteSpace(id?.Value))
                return true;
        }

        return false;
    }

    private static YamlMappingNode? GetMap(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value as YamlMappingNode;

    private static YamlNode? GetNode(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value;

    private static string? GetScalar(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value is YamlScalarNode value
            ? value.Value
            : null;

    private static string InferCollection(string key, string seedFile)
    {
        var fileBase = Path.GetFileNameWithoutExtension(seedFile);
        var found = ImportSeedRecordReader.KnownFiles.FirstOrDefault(k =>
            k.FileBase.Equals(fileBase, StringComparison.OrdinalIgnoreCase) ||
            k.FileBase.Equals(key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(found.Collection) ? key.TrimEnd('s') : found.Collection;
    }

    private static string ToTitle(string key)
        => string.IsNullOrWhiteSpace(key)
            ? "Content"
            : char.ToUpperInvariant(key[0]) + key[1..];
}
