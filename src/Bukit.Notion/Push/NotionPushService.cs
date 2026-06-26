using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Notion.Client;
using Bukit.Notion.Mapping;
using Bukit.Notion.Report;
using Bukit.Notion.Seed;

namespace Bukit.Notion.Push;

public sealed class NotionPushService : INotionPushService
{
    private readonly INotionClientFactory _clientFactory;
    private readonly INotionTokenProvider _tokenProvider;

    public NotionPushService()
        : this(new HttpNotionClientFactory(), new EnvironmentNotionTokenProvider())
    {
    }

    public NotionPushService(INotionClientFactory clientFactory, INotionTokenProvider tokenProvider)
    {
        _clientFactory = clientFactory;
        _tokenProvider = tokenProvider;
    }

    public NotionPushResult Push(NotionPushOptions options)
        => PushAsync(options, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<NotionPushResult> PushAsync(NotionPushOptions options, CancellationToken cancellationToken)
    {
        NotionSeedValidationResult seedValidation = NotionSeedValidator.Validate(options.ProjectRoot, options.SeedDirectory);
        if (!seedValidation.Success)
        {
            return FromSeedValidation(options, seedValidation);
        }

        NotionDatabaseMapValidationResult mapValidation = NotionDatabaseMapValidator.Validate(options.ProjectRoot, options.DatabaseMapPath);
        if (!mapValidation.Success)
        {
            return FromDatabaseMapValidation(options, mapValidation);
        }

        NotionSeedSet seedSet = seedValidation.SeedSet!;
        NotionDatabaseMap databaseMap = mapValidation.DatabaseMap!;
        var diagnostics = new List<NotionPushDiagnostic>();
        var records = new List<NotionPushRecordResult>();

        foreach (NotionDatabaseMapEntry entry in databaseMap.Databases.Values)
        {
            NotionSeedCollection? collection = FindSeedCollection(seedSet, entry.Seed);
            if (collection is null)
            {
                diagnostics.Add(new NotionPushDiagnostic(
                    "notion.seedFileMissing",
                    NotionDiagnosticSeverity.Error,
                    $"Seed file declared by database map was not found: {entry.Seed}",
                    options.DatabaseMapPath));
                continue;
            }

            foreach (NotionSeedRecord record in collection.Records)
            {
                NotionPushRecordResult? planned = PlanRecord(options.Mode, entry, collection, record, diagnostics);
                if (planned is not null)
                {
                    records.Add(planned);
                }
            }
        }

        if (diagnostics.Any(static diagnostic => diagnostic.Severity == NotionDiagnosticSeverity.Error))
        {
            return new NotionPushResult(false, 2, options.DryRun, options.Mode, records, diagnostics, []);
        }

        if (!options.DryRun)
        {
            NotionPushResult? tokenValidation = ValidateNonDryRunPush(options);
            if (tokenValidation is not null)
            {
                return tokenValidation;
            }

            NotionPushResult? pushResult = await PushNonDryRunAsync(
                options,
                databaseMap,
                seedSet,
                records,
                cancellationToken).ConfigureAwait(false);
            if (pushResult is not null)
            {
                return pushResult;
            }
        }

        NotionPushReport report = NotionPushReportWriter.CreateReport(options.Mode, options.DryRun, records);
        NotionPushReportWriter.WriteJson(options.ReportPath, report);
        string markdownReportPath = Path.ChangeExtension(options.ReportPath, ".md");
        NotionPushReportWriter.WriteMarkdown(markdownReportPath, report);

        return new NotionPushResult(
            true,
            0,
            options.DryRun,
            options.Mode,
            records,
            Diagnostics:
            [
                new NotionPushDiagnostic(
                    options.DryRun ? "notion.pushDryRunPlanned" : "notion.pushCreated",
                    NotionDiagnosticSeverity.Info,
                    options.DryRun ? "Notion dry-run push plan was generated." : "Notion push completed.",
                    options.ReportPath)
            ],
            Artifacts:
            [
                new NotionPushArtifact(
                    "notion-push-report",
                    options.ReportPath,
                    options.DryRun ? "Notion dry-run push JSON report." : "Notion push JSON report."),
                new NotionPushArtifact(
                    "notion-push-report-md",
                    markdownReportPath,
                    options.DryRun ? "Notion dry-run push Markdown report." : "Notion push Markdown report.")
            ]);
    }

    private NotionPushResult? ValidateNonDryRunPush(NotionPushOptions options)
    {
        if (options.Mode == NotionPushMode.Replace && !options.ConfirmReplace)
        {
            return NotionPushResult.Failed(
                options.Mode,
                options.DryRun,
                new NotionPushDiagnostic(
                    "notion.replaceRequiresConfirmation",
                    NotionDiagnosticSeverity.Error,
                    "Replace mode requires --confirm-replace."));
        }

        if (options.Mode is not (NotionPushMode.Create or NotionPushMode.Upsert or NotionPushMode.Replace))
        {
            return NotionPushResult.Failed(
                options.Mode,
                options.DryRun,
                new NotionPushDiagnostic(
                    "notion.pushCreateOnly",
                    NotionDiagnosticSeverity.Error,
                    "Only create, upsert, and replace modes are implemented for non-dry-run push in this phase."));
        }

        if (!NotionPluginConstants.IsAllowedTokenEnvironmentVariable(options.TokenEnvironmentVariable))
        {
            return NotionPushResult.Failed(
                options.Mode,
                options.DryRun,
                new NotionPushDiagnostic(
                    "notion.tokenEnvNotAllowed",
                    NotionDiagnosticSeverity.Error,
                    "Notion token must come from an allowlisted environment variable."));
        }

        string? token = _tokenProvider.GetToken(options.TokenEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(token))
        {
            return NotionPushResult.Failed(
                options.Mode,
                options.DryRun,
                new NotionPushDiagnostic(
                    "notion.tokenMissing",
                    NotionDiagnosticSeverity.Error,
                    $"Environment variable {options.TokenEnvironmentVariable} is required for Notion push."));
        }

        return null;
    }

    private async Task<NotionPushResult?> PushNonDryRunAsync(
        NotionPushOptions options,
        NotionDatabaseMap databaseMap,
        NotionSeedSet seedSet,
        List<NotionPushRecordResult> records,
        CancellationToken cancellationToken)
    {
        string token = _tokenProvider.GetToken(options.TokenEnvironmentVariable)!;
        INotionClient client = _clientFactory.Create(new NotionRequestOptions(token));
        var actualRecords = new List<NotionPushRecordResult>();

        try
        {
            foreach (NotionDatabaseMapEntry entry in databaseMap.Databases.Values)
            {
                NotionSeedCollection? collection = FindSeedCollection(seedSet, entry.Seed);
                if (collection is null)
                {
                    continue;
                }

                foreach (NotionSeedRecord record in collection.Records)
                {
                    NotionPushRecordResult? planned = records.FirstOrDefault(item =>
                        string.Equals(item.SeedFile, Path.GetFileName(collection.Path), StringComparison.Ordinal)
                        && string.Equals(item.UniqueField, entry.UniqueField, StringComparison.Ordinal)
                        && TryResolveUniqueValue(entry, record, out string? uniqueValue)
                        && string.Equals(item.UniqueValue, uniqueValue, StringComparison.Ordinal));
                    if (planned is null)
                    {
                        continue;
                    }

                    if (options.Mode is NotionPushMode.Upsert or NotionPushMode.Replace)
                    {
                        NotionQueryResult queryResult = await client.QueryDataSourceAsync(
                            entry.EffectiveDataSourceId!,
                            new NotionQueryRequest(BuildUniqueQueryJson(entry, planned.UniqueValue)),
                            cancellationToken).ConfigureAwait(false);

                        if (options.Mode == NotionPushMode.Replace)
                        {
                            NotionPushResult? replaceResult = await ReplaceRecordAsync(
                                client,
                                options,
                                entry,
                                record,
                                planned,
                                queryResult,
                                actualRecords,
                                cancellationToken).ConfigureAwait(false);
                            if (replaceResult is not null)
                            {
                                return replaceResult;
                            }

                            continue;
                        }

                        string? pageId = queryResult.ResultIds.FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(pageId))
                        {
                            await client.UpdatePagePropertiesAsync(
                                pageId,
                                new NotionUpdatePageRequest(BuildUpdatePageJson(entry, record)),
                                cancellationToken).ConfigureAwait(false);
                            actualRecords.Add(planned with { Operation = "update" });
                            continue;
                        }
                    }

                    await client.CreatePageAsync(
                        new NotionCreatePageRequest(BuildCreatePageJson(entry, record)),
                        cancellationToken).ConfigureAwait(false);
                    actualRecords.Add(planned with { Operation = "create" });
                }
            }
        }
        catch (NotionApiException ex)
        {
            return new NotionPushResult(
                false,
                2,
                options.DryRun,
                options.Mode,
                records,
                Diagnostics:
                [
                    new NotionPushDiagnostic(
                        "notion.apiError",
                        NotionDiagnosticSeverity.Error,
                        $"Notion API request failed with status {(int)ex.StatusCode}, code {ex.Code ?? "unknown"}.")
                ],
                Artifacts: []);
        }
        catch (HttpRequestException)
        {
            return new NotionPushResult(
                false,
                2,
                options.DryRun,
                options.Mode,
                records,
                Diagnostics:
                [
                    new NotionPushDiagnostic(
                        "notion.httpError",
                        NotionDiagnosticSeverity.Error,
                        "Notion HTTP request failed.")
                ],
                Artifacts: []);
        }

        records.Clear();
        records.AddRange(actualRecords);
        return null;
    }

    private static async Task<NotionPushResult?> ReplaceRecordAsync(
        INotionClient client,
        NotionPushOptions options,
        NotionDatabaseMapEntry entry,
        NotionSeedRecord record,
        NotionPushRecordResult planned,
        NotionQueryResult queryResult,
        List<NotionPushRecordResult> actualRecords,
        CancellationToken cancellationToken)
    {
        if (queryResult.ResultIds.Count == 0)
        {
            return NotionPushResult.Failed(
                options.Mode,
                options.DryRun,
                new NotionPushDiagnostic(
                    "notion.replaceNoMatch",
                    NotionDiagnosticSeverity.Error,
                    $"Replace mode found no Notion page for {planned.UniqueField}."));
        }

        if (queryResult.ResultIds.Count > 1)
        {
            return NotionPushResult.Failed(
                options.Mode,
                options.DryRun,
                new NotionPushDiagnostic(
                    "notion.replaceMultipleMatches",
                    NotionDiagnosticSeverity.Error,
                    $"Replace mode found multiple Notion pages for {planned.UniqueField}."));
        }

        string pageId = queryResult.ResultIds[0];
        await client.UpdatePagePropertiesAsync(
            pageId,
            new NotionUpdatePageRequest(BuildUpdatePageJson(entry, record)),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<NotionBlockResult> children = await client.ListBlockChildrenAsync(pageId, cancellationToken).ConfigureAwait(false);
        foreach (NotionBlockResult child in children)
        {
            if (!string.IsNullOrWhiteSpace(child.Id))
            {
                try
                {
                    await client.DeleteBlockAsync(child.Id, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is NotionApiException or HttpRequestException)
                {
                    return NotionPushResult.Failed(
                        options.Mode,
                        options.DryRun,
                        new NotionPushDiagnostic(
                            "notion.replaceDeleteFailed",
                            NotionDiagnosticSeverity.Error,
                            "Replace mode failed while deleting existing Notion blocks."));
                }
            }
        }

        IReadOnlyList<NotionBlock> replacementBlocks = BuildReplacementBlocks(record);
        if (replacementBlocks.Count > 0)
        {
            try
            {
                await client.AppendBlockChildrenAsync(pageId, replacementBlocks, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is NotionApiException or HttpRequestException)
            {
                return NotionPushResult.Failed(
                    options.Mode,
                    options.DryRun,
                    new NotionPushDiagnostic(
                        "notion.replaceAppendFailed",
                        NotionDiagnosticSeverity.Error,
                        "Replace mode failed while appending new Notion blocks."));
            }
        }

        actualRecords.Add(planned with { Operation = "replace" });
        return null;
    }

    private static string BuildCreatePageJson(NotionDatabaseMapEntry entry, NotionSeedRecord record)
    {
        var root = new JsonObject
        {
            ["parent"] = new JsonObject
            {
                ["data_source_id"] = entry.EffectiveDataSourceId
            },
            ["properties"] = BuildPropertiesJsonObject(entry, record)
        };
        return root.ToJsonString();
    }

    private static string BuildUpdatePageJson(NotionDatabaseMapEntry entry, NotionSeedRecord record)
    {
        var root = new JsonObject
        {
            ["properties"] = BuildPropertiesJsonObject(entry, record)
        };
        return root.ToJsonString();
    }

    private static IReadOnlyList<NotionBlock> BuildReplacementBlocks(NotionSeedRecord record)
    {
        string? content = ReadOptionalString(record, "content")
            ?? ReadOptionalString(record, "body")
            ?? ReadOptionalString(record, "markdown")
            ?? ReadOptionalString(record, "content_markdown");
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var block = new JsonObject
        {
            ["object"] = "block",
            ["type"] = "paragraph",
            ["paragraph"] = new JsonObject
            {
                ["rich_text"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = new JsonObject
                        {
                            ["content"] = content
                        }
                    }
                }
            }
        };
        return [new NotionBlock(block.ToJsonString())];
    }

    private static JsonObject BuildPropertiesJsonObject(NotionDatabaseMapEntry entry, NotionSeedRecord record)
    {
        var properties = new JsonObject();
        foreach (NotionPropertyMapping property in entry.Properties.Values)
        {
            if (string.IsNullOrWhiteSpace(property.Source)
                || string.IsNullOrWhiteSpace(property.Type)
                || !record.Fields.TryGetValue(property.Source, out JsonElement value)
                || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            JsonNode? notionValue = ToNotionPropertyValue(property.Type, value);
            if (notionValue is not null)
            {
                properties[property.Name] = notionValue;
            }
        }

        return properties;
    }

    private static string BuildUniqueQueryJson(NotionDatabaseMapEntry entry, string uniqueValue)
    {
        string propertyType = entry.UniqueField is not null
            && entry.Properties.TryGetValue(entry.UniqueField, out NotionPropertyMapping? property)
            && !string.IsNullOrWhiteSpace(property.Type)
                ? property.Type
                : "rich_text";

        var root = new JsonObject
        {
            ["filter"] = new JsonObject
            {
                ["property"] = entry.UniqueField,
                [propertyType] = CreateFilterValue(propertyType, uniqueValue)
            },
            ["page_size"] = 1
        };
        return root.ToJsonString();
    }

    private static JsonObject CreateFilterValue(string propertyType, string uniqueValue)
    {
        if (propertyType == "number")
        {
            var number = new JsonObject();
            number["equals"] = decimal.TryParse(uniqueValue, out decimal value) ? value : null;
            return number;
        }

        return propertyType switch
        {
            "checkbox" => new JsonObject { ["equals"] = bool.TryParse(uniqueValue, out bool value) && value },
            "multi_select" => new JsonObject { ["contains"] = uniqueValue },
            _ => new JsonObject { ["equals"] = uniqueValue }
        };
    }

    private static JsonNode? ToNotionPropertyValue(string type, JsonElement value)
        => type switch
        {
            "title" => CreateRichTextProperty("title", ElementToString(value)),
            "rich_text" => CreateRichTextProperty("rich_text", ElementToString(value)),
            "checkbox" => value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? new JsonObject { ["checkbox"] = value.GetBoolean() }
                : null,
            "number" => value.ValueKind == JsonValueKind.Number
                ? new JsonObject { ["number"] = JsonValue.Create(value.GetDecimal()) }
                : null,
            "select" => CreateNamedProperty("select", ElementToString(value)),
            "multi_select" => CreateMultiSelectProperty(value),
            "url" => CreateStringProperty("url", ElementToString(value)),
            "email" => CreateStringProperty("email", ElementToString(value)),
            "phone_number" => CreateStringProperty("phone_number", ElementToString(value)),
            "date" => CreateDateProperty(ElementToString(value)),
            _ => null
        };

    private static JsonObject? CreateRichTextProperty(string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new JsonObject
        {
            [type] = new JsonArray
            {
                new JsonObject
                {
                    ["text"] = new JsonObject
                    {
                        ["content"] = value
                    }
                }
            }
        };
    }

    private static JsonObject? CreateNamedProperty(string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new JsonObject
        {
            [type] = new JsonObject
            {
                ["name"] = value
            }
        };
    }

    private static JsonObject? CreateStringProperty(string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new JsonObject { [type] = value };
    }

    private static JsonObject? CreateDateProperty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new JsonObject
        {
            ["date"] = new JsonObject
            {
                ["start"] = value
            }
        };
    }

    private static JsonObject? CreateMultiSelectProperty(JsonElement value)
    {
        var items = new JsonArray();
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                string? name = ElementToString(item);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    items.Add(new JsonObject { ["name"] = name });
                }
            }
        }
        else
        {
            string? name = ElementToString(value);
            if (!string.IsNullOrWhiteSpace(name))
            {
                items.Add(new JsonObject { ["name"] = name });
            }
        }

        return items.Count == 0 ? null : new JsonObject { ["multi_select"] = items };
    }

    private static string? ElementToString(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

    private NotionPushRecordResult? PlanRecord(
        NotionPushMode mode,
        NotionDatabaseMapEntry entry,
        NotionSeedCollection collection,
        NotionSeedRecord record,
        List<NotionPushDiagnostic> diagnostics)
    {
        string recordPath = $"{collection.Path}#{record.Index}";
        if (string.IsNullOrWhiteSpace(entry.UniqueField))
        {
            diagnostics.Add(new NotionPushDiagnostic(
                "notion.uniqueFieldMissing",
                NotionDiagnosticSeverity.Error,
                "Database map entry uniqueField is required for push planning.",
                recordPath));
            return null;
        }

        string? uniqueSource = ResolveUniqueSource(entry);
        if (string.IsNullOrWhiteSpace(uniqueSource)
            || !TryGetNonEmptyString(record, uniqueSource, out string? uniqueValue))
        {
            diagnostics.Add(new NotionPushDiagnostic(
                "notion.uniqueFieldMissing",
                NotionDiagnosticSeverity.Error,
                $"Seed record does not contain a value for unique field {entry.UniqueField}.",
                recordPath));
            return null;
        }

        return new NotionPushRecordResult(
            Collection: entry.Collection ?? collection.Name,
            SeedFile: Path.GetFileName(collection.Path),
            Operation: NotionPushReportWriter.ToOperation(mode),
            Title: ReadOptionalString(record, "title") ?? ReadOptionalString(record, "name"),
            UniqueField: entry.UniqueField,
            UniqueValue: uniqueValue!,
            DataSourceId: entry.EffectiveDataSourceId!);
    }

    private static string? ResolveUniqueSource(NotionDatabaseMapEntry entry)
    {
        if (entry.UniqueField is not null
            && entry.Properties.TryGetValue(entry.UniqueField, out NotionPropertyMapping? property)
            && !string.IsNullOrWhiteSpace(property.Source))
        {
            return property.Source;
        }

        return entry.UniqueField is null ? null : ToSnakeCase(entry.UniqueField);
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var chars = new List<char>();
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (char.IsUpper(current))
            {
                if (i > 0)
                {
                    chars.Add('_');
                }

                chars.Add(char.ToLowerInvariant(current));
            }
            else
            {
                chars.Add(current);
            }
        }

        return new string(chars.ToArray());
    }

    private static NotionSeedCollection? FindSeedCollection(NotionSeedSet seedSet, string? seedFile)
    {
        if (string.IsNullOrWhiteSpace(seedFile))
        {
            return null;
        }

        return seedSet.Collections.FirstOrDefault(collection =>
            string.Equals(Path.GetFileName(collection.Path), Path.GetFileName(seedFile), StringComparison.Ordinal));
    }

    private static bool TryGetNonEmptyString(NotionSeedRecord record, string key, out string? value)
    {
        value = null;
        if (!record.Fields.TryGetValue(key, out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryResolveUniqueValue(NotionDatabaseMapEntry entry, NotionSeedRecord record, out string? value)
    {
        value = null;
        string? uniqueSource = ResolveUniqueSource(entry);
        return !string.IsNullOrWhiteSpace(uniqueSource)
            && TryGetNonEmptyString(record, uniqueSource, out value);
    }

    private static string? ReadOptionalString(NotionSeedRecord record, string key)
        => record.Fields.TryGetValue(key, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static NotionPushResult FromSeedValidation(NotionPushOptions options, NotionSeedValidationResult result)
        => new(
            false,
            result.ExitCode,
            options.DryRun,
            options.Mode,
            Records: [],
            Diagnostics: result.Diagnostics.Select(static diagnostic => new NotionPushDiagnostic(
                diagnostic.Code,
                diagnostic.Severity,
                diagnostic.Message,
                diagnostic.Path)).ToArray(),
            Artifacts: []);

    private static NotionPushResult FromDatabaseMapValidation(NotionPushOptions options, NotionDatabaseMapValidationResult result)
        => new(
            false,
            result.ExitCode,
            options.DryRun,
            options.Mode,
            Records: [],
            Diagnostics: result.Diagnostics.Select(static diagnostic => new NotionPushDiagnostic(
                diagnostic.Code,
                diagnostic.Severity,
                diagnostic.Message,
                diagnostic.Path)).ToArray(),
            Artifacts: []);

}
