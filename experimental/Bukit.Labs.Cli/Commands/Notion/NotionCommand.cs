using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Shared.Notion;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace Bukit.Labs.Cli.Commands;

public static class NotionCommand
{
    internal static Func<HttpClient> CreateHttpClient { get; set; } = () => new HttpClient();

    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0) ?? "";
        return sub switch
        {
            "push" => PushAsync(command),
            "validate-schema" => ValidateSchemaAsync(command),
            _ => Unknown(sub)
        };
    }

    private static async Task<int> PushAsync(CliBoundCommand command)
    {
        var input = command.GetString("--input");
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.Error.WriteLine("缺少必填选项: --input <seed-dir>");
            return 2;
        }

        var inputDir = Path.GetFullPath(input);
        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"seed 目录不存在: {inputDir}");
            return 2;
        }

        var dryRun = command.GetBool("--dry-run");
        var tokenEnv = command.GetString("--token-env") ?? "NOTION_TOKEN";
        var mode = command.GetString("--mode") ?? "create";
        if (mode is not ("create" or "upsert"))
        {
            Console.Error.WriteLine($"不支持的推送模式: {mode}，可用: create | upsert");
            return 2;
        }
        var uniqueField = command.GetString("--unique-field") ?? "Slug";
        var updateContent = command.GetString("--update-content") ?? "";
        if (updateContent is not ("" or "append" or "replace"))
        {
            Console.Error.WriteLine($"不支持的 --update-content 值: {updateContent}，可用: append | replace");
            return 2;
        }

        if (!dryRun && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(tokenEnv)))
        {
            Console.Error.WriteLine($"{tokenEnv} is required for notion push. Use --dry-run to generate a local review plan.");
            return 2;
        }

        var reportPath = command.GetString("--report");
        if (string.IsNullOrWhiteSpace(reportPath))
            reportPath = Path.Combine(inputDir, dryRun ? "notion-push-plan.json" : "notion-push-report.json");
        reportPath = Path.GetFullPath(reportPath);

        var databaseId = command.GetString("--database-id");
        var databaseMapPath = command.GetString("--database-map");
        var createMissingDatabases = command.GetBool("--create-missing-databases");
        var parentPageId = command.GetString("--parent-page-id");
        var generatedMapPath = command.GetString("--generated-database-map");
        var validateSchema = !command.GetBool("--no-validate-schema");

        if (string.IsNullOrWhiteSpace(databaseMapPath) && !string.IsNullOrWhiteSpace(databaseId))
            return await PushSingleDatabaseAsync(inputDir, databaseId!, tokenEnv, mode, uniqueField, updateContent, dryRun, reportPath, validateSchema);

        if (string.IsNullOrWhiteSpace(databaseMapPath))
        {
            var defaultMapPath = Path.Combine(inputDir, "notion-database-map.yaml");
            if (File.Exists(defaultMapPath))
                databaseMapPath = defaultMapPath;
        }

        var targets = string.IsNullOrWhiteSpace(databaseMapPath)
            ? BuildDefaultDatabaseTargets(inputDir, uniqueField)
            : ReadDatabaseMap(Path.GetFullPath(databaseMapPath), inputDir, uniqueField);

        if (targets.Count == 0)
        {
            Console.Error.WriteLine($"没有可推送的 seed 文件: {inputDir}");
            return 2;
        }

        var missingTargets = targets.Where(t => string.IsNullOrWhiteSpace(t.DatabaseId)).ToList();
        if (missingTargets.Count > 0 && !createMissingDatabases)
        {
            Console.Error.WriteLine("缺少 databaseId。请提供 --database-map 中的 databaseId，或使用 --create-missing-databases --parent-page-id <id> 自动创建。");
            foreach (var target in missingTargets)
                Console.Error.WriteLine($"  {target.Key}: {target.SeedFile}");
            return 2;
        }
        if (missingTargets.Count > 0 && string.IsNullOrWhiteSpace(parentPageId))
        {
            Console.Error.WriteLine("--create-missing-databases 需要 --parent-page-id <id>。");
            return 2;
        }

        using var http = CreateHttpClient();
        var token = Environment.GetEnvironmentVariable(tokenEnv) ?? "";
        var completedTargets = new List<NotionDatabaseTarget>();
        var pushResults = new List<(NotionDatabaseTarget Target, NotionPushResult Result)>();
        var failed = false;

        foreach (var target in targets)
        {
            var activeTarget = target;
            if (string.IsNullOrWhiteSpace(activeTarget.DatabaseId))
            {
                if (dryRun)
                {
                    completedTargets.Add(activeTarget);
                    pushResults.Add((activeTarget, BuildDryRunResult(inputDir, activeTarget)));
                    continue;
                }

                var createdDatabaseId = await CreateDatabaseAsync(http, token, parentPageId!, activeTarget.Title, activeTarget.Collection);
                if (string.IsNullOrWhiteSpace(createdDatabaseId))
                {
                    failed = true;
                    pushResults.Add((activeTarget, new NotionPushResult(0, 0, 0, 0, 1, [])));
                    continue;
                }
                activeTarget = activeTarget with { DatabaseId = createdDatabaseId };
                if (validateSchema)
                {
                    var schemaReport = await NotionSchemaValidator.ValidateAsync(
                        http,
                        activeTarget.DatabaseId!,
                        token,
                        null,
                        GetAdditionalSchemaFields(activeTarget.Collection));
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
            else if (!dryRun && validateSchema)
            {
                var schemaReport = await NotionSchemaValidator.ValidateAsync(
                    http,
                    activeTarget.DatabaseId!,
                    token,
                    null,
                    GetAdditionalSchemaFields(activeTarget.Collection));
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

            var records = ImportSeedRecordReader.ReadSeedFile(inputDir, activeTarget.SeedFile, activeTarget.Collection);
            var result = await NotionSeedPusher.PushAsync(http, records, new NotionPushOptions(
                DatabaseId: activeTarget.DatabaseId ?? "",
                Token: token,
                ReportPath: reportPath,
                DryRun: dryRun,
                Mode: mode,
                UniqueField: activeTarget.UniqueField,
                UpdateContent: updateContent,
                WriteReport: false));
            if (result.Failed > 0) failed = true;
            completedTargets.Add(activeTarget);
            pushResults.Add((activeTarget, result));
        }

        WriteMultiDatabaseReport(reportPath, dryRun, completedTargets, pushResults);
        if (createMissingDatabases || !string.IsNullOrWhiteSpace(generatedMapPath))
        {
            var mapOutputPath = ResolveGeneratedMapPath(inputDir, databaseMapPath, generatedMapPath);
            WriteDatabaseMap(mapOutputPath, completedTargets);
        }

        var totalRecords = pushResults.Sum(r => r.Result.Total);
        var totalCreated = pushResults.Sum(r => r.Result.Created);
        var totalUpdated = pushResults.Sum(r => r.Result.Updated);
        var totalFailed = pushResults.Sum(r => r.Result.Failed);
        Console.WriteLine($"notion push {(dryRun ? "dry-run" : "api")} 完成: databases={completedTargets.Count} records={totalRecords} created={totalCreated} updated={totalUpdated} failed={totalFailed} report={reportPath}");
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
        using var http = CreateHttpClient();
        var token = Environment.GetEnvironmentVariable(tokenEnv) ?? "";
        if (!dryRun && validateSchema)
        {
            var schemaReport = await NotionSchemaValidator.ValidateAsync(http, databaseId, token, null);
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
                UniqueField: GetScalar(map, "uniqueField") ?? defaultUniqueField));
        }
        return targets.Where(t => File.Exists(Path.Combine(inputDir, t.SeedFile))).ToList();
    }

    private static async Task<string?> CreateDatabaseAsync(HttpClient http, string token, string parentPageId, string title, string collection)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{NotionApiUrls.Base}/{NotionApiUrls.ApiVersion}/databases");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Notion-Version", NotionApiUrls.NotionVersion);
        request.Content = new StringContent(BuildCreateDatabasePayload(parentPageId, title, collection), Encoding.UTF8, "application/json");

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

    private static string BuildCreateDatabasePayload(string parentPageId, string title, string collection)
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
        if (collection.Equals("navigation", StringComparison.OrdinalIgnoreCase))
        {
            WriteDatabaseProperty(writer, "Link", "url");
            WriteDatabaseProperty(writer, "Order", "number");
            WriteDatabaseProperty(writer, "Enabled", "checkbox");
        }
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

    private static IReadOnlyList<(string Name, string ExpectedType)>? GetAdditionalSchemaFields(string collection)
        => collection.Equals("navigation", StringComparison.OrdinalIgnoreCase)
            ? [("Link", "url"), ("Order", "number")]
            : null;

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

    private static YamlMappingNode? GetMap(YamlMappingNode map, string key)
        => map.Children.FirstOrDefault(kv =>
            kv.Key is YamlScalarNode scalar && scalar.Value == key).Value as YamlMappingNode;

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

    private static async Task<int> ValidateSchemaAsync(CliBoundCommand command)
    {
        var databaseId = command.GetString("--database-id");
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            Console.Error.WriteLine("缺少必填选项: --database-id <id>");
            return 2;
        }

        var tokenEnv = command.GetString("--token-env") ?? "NOTION_TOKEN";
        var token = Environment.GetEnvironmentVariable(tokenEnv);
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine($"{tokenEnv} is required for notion validate-schema.");
            return 2;
        }

        var reportPath = command.GetString("--report");

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

    private static Task<int> Unknown(string sub)
    {
        Console.Error.WriteLine($"未知的 notion 子命令: {sub}");
        Console.Error.WriteLine("可用: push, validate-schema");
        return Task.FromResult(2);
    }
}

internal sealed record NotionDatabaseTarget(
    string Key,
    string Title,
    string SeedFile,
    string Collection,
    string? DatabaseId,
    string UniqueField);
