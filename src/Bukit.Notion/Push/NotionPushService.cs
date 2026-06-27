using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Notion.Client;
using Bukit.Notion.Conversion;
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
            return WriteFailureReport(options, [], FromSeedValidation(options, seedValidation));
        }

        NotionDatabaseMapValidationResult mapValidation = NotionDatabaseMapValidator.Validate(options.ProjectRoot, options.DatabaseMapPath);
        if (!mapValidation.Success)
        {
            return WriteFailureReport(options, [], FromDatabaseMapValidation(options, mapValidation));
        }

        NotionSeedSet seedSet = seedValidation.SeedSet!;
        NotionDatabaseMap databaseMap = mapValidation.DatabaseMap!;
        var diagnostics = new List<NotionPushDiagnostic>();
        var records = new List<NotionPushRecordResult>();
        var planner = new NotionPushPlanner(options.Mode, diagnostics);

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
                NotionPushRecordResult? planned = planner.Plan(entry, collection, record);
                if (planned is not null)
                {
                    records.Add(planned);
                }
            }
        }

        if (diagnostics.Any(static diagnostic => diagnostic.Severity == NotionDiagnosticSeverity.Error))
        {
            var planningFailure = new NotionPushResult(
                false,
                2,
                options.DryRun,
                options.Mode,
                records,
                diagnostics,
                []);
            return WriteFailureReport(options, records, planningFailure);
        }

        if (!options.DryRun)
        {
            NotionPushResult? tokenValidation = ValidateNonDryRunPush(options);
            if (tokenValidation is not null)
            {
                return WriteFailureReport(options, records, tokenValidation);
            }

            NotionPushResult? pushResult = await PushNonDryRunAsync(
                options,
                databaseMap,
                seedSet,
                records,
                cancellationToken).ConfigureAwait(false);
            if (pushResult is not null)
            {
                return WriteFailureReport(options, pushResult.Records, pushResult);
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
        NotionPushRecordResult? currentRecord = null;
        string? currentRemotePageId = null;

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
                        && NotionUniqueValueResolver.TryResolve(entry, record, out string? uniqueValue)
                        && string.Equals(item.UniqueValue, uniqueValue, StringComparison.Ordinal));
                    if (planned is null)
                    {
                        continue;
                    }

                    currentRecord = planned;
                    currentRemotePageId = null;

                    if (options.Mode is NotionPushMode.Upsert or NotionPushMode.Replace)
                    {
                        NotionQueryResult queryResult = await client.QueryDataSourceAsync(
                            entry.EffectiveDataSourceId!,
                            new NotionQueryRequest(NotionUniqueValueResolver.BuildQueryJson(entry, planned.UniqueValue)),
                            cancellationToken).ConfigureAwait(false);

                        if (options.Mode == NotionPushMode.Replace)
                        {
                            currentRemotePageId = queryResult.ResultIds.Count == 1
                                ? queryResult.ResultIds[0]
                                : null;
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

                            currentRecord = null;
                            continue;
                        }

                        if (queryResult.ResultIds.Count > 1)
                        {
                            return NotionPushRuntimeFailure.Create(
                                options,
                                actualRecords,
                                planned,
                                null,
                                "notion.upsertMultipleMatches",
                                $"Upsert mode found multiple Notion pages for {planned.UniqueField}.",
                                exitCode: 2,
                                status: NotionPushRecordStatus.Skipped);
                        }

                        string? pageId = queryResult.ResultIds.FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(pageId))
                        {
                            currentRemotePageId = pageId;
                            await client.UpdatePagePropertiesAsync(
                                pageId,
                                new NotionUpdatePageRequest(BuildUpdatePageJson(entry, record)),
                                cancellationToken).ConfigureAwait(false);
                            actualRecords.Add(planned with
                            {
                                Operation = "update",
                                Status = NotionPushRecordStatus.Updated,
                                RemotePageId = pageId
                            });
                            currentRecord = null;
                            continue;
                        }
                    }

                    NotionPageResult createdPage = await client.CreatePageAsync(
                        new NotionCreatePageRequest(BuildCreatePageJson(entry, record)),
                        cancellationToken).ConfigureAwait(false);
                    currentRemotePageId = createdPage.Id;
                    IReadOnlyList<NotionBlock> contentBlocks = BuildReplacementBlocks(record);
                    if (!string.IsNullOrWhiteSpace(createdPage.Id) && contentBlocks.Count > 0)
                    {
                        await AppendBlockChildrenInBatchesAsync(
                            client,
                            createdPage.Id,
                            contentBlocks,
                            cancellationToken).ConfigureAwait(false);
                    }

                    actualRecords.Add(planned with
                    {
                        Operation = "create",
                        Status = NotionPushRecordStatus.Created,
                        RemotePageId = createdPage.Id
                    });
                    currentRecord = null;
                }
            }
        }
        catch (NotionApiException ex)
        {
            return NotionPushRuntimeFailure.Create(
                options,
                actualRecords,
                currentRecord,
                currentRemotePageId,
                NotionPushRuntimeFailure.MapApiDiagnosticCode(ex),
                $"Notion API request failed with status {(int)ex.StatusCode}, code {ex.Code ?? "unknown"}.");
        }
        catch (HttpRequestException)
        {
            return NotionPushRuntimeFailure.Create(
                options,
                actualRecords,
                currentRecord,
                currentRemotePageId,
                "notion.httpError",
                "Notion HTTP request failed.");
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
            return NotionPushRuntimeFailure.Create(
                options,
                actualRecords,
                planned,
                null,
                "notion.replaceNoMatch",
                $"Replace mode found no Notion page for {planned.UniqueField}.",
                exitCode: 2,
                status: NotionPushRecordStatus.Skipped);
        }

        if (queryResult.ResultIds.Count > 1)
        {
            return NotionPushRuntimeFailure.Create(
                options,
                actualRecords,
                planned,
                null,
                "notion.replaceMultipleMatches",
                $"Replace mode found multiple Notion pages for {planned.UniqueField}.",
                exitCode: 2,
                status: NotionPushRecordStatus.Skipped);
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
                    const string errorMessage = "Replace mode is not atomic; page properties may have been updated before block deletion failed.";
                    return NotionPushRuntimeFailure.Create(
                        options,
                        actualRecords,
                        planned,
                        pageId,
                        "notion.replaceDeleteFailed",
                        errorMessage,
                        exitCode: 2);
                }
            }
        }

        IReadOnlyList<NotionBlock> replacementBlocks = BuildReplacementBlocks(record);
        if (replacementBlocks.Count > 0)
        {
            try
            {
                await AppendBlockChildrenInBatchesAsync(
                    client,
                    pageId,
                    replacementBlocks,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is NotionApiException or HttpRequestException)
            {
                const string errorMessage = "Replace mode is not atomic; page properties may have been updated before block append failed.";
                return NotionPushRuntimeFailure.Create(
                    options,
                    actualRecords,
                    planned,
                    pageId,
                    "notion.replaceAppendFailed",
                    errorMessage,
                    exitCode: 2);
            }
        }

        actualRecords.Add(planned with
        {
            Operation = "replace",
            Status = NotionPushRecordStatus.Replaced,
            RemotePageId = pageId
        });
        return null;
    }

    private static async Task AppendBlockChildrenInBatchesAsync(
        INotionClient client,
        string blockId,
        IReadOnlyList<NotionBlock> blocks,
        CancellationToken cancellationToken)
    {
        foreach (IReadOnlyList<NotionBlock> batch in NotionBlockBatcher.Batch(blocks))
        {
            await client.AppendBlockChildrenAsync(blockId, batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildCreatePageJson(NotionDatabaseMapEntry entry, NotionSeedRecord record)
    {
        var root = new JsonObject
        {
            ["parent"] = new JsonObject
            {
                ["data_source_id"] = entry.EffectiveDataSourceId
            },
            ["properties"] = NotionPropertyMapper.BuildPropertiesJsonObject(entry, record)
        };
        return root.ToJsonString();
    }

    private static string BuildUpdatePageJson(NotionDatabaseMapEntry entry, NotionSeedRecord record)
    {
        var root = new JsonObject
        {
            ["properties"] = NotionPropertyMapper.BuildPropertiesJsonObject(entry, record)
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

        return MarkdownToNotionBlocks.Convert(content);
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

    private static NotionPushResult WriteFailureReport(
        NotionPushOptions options,
        IReadOnlyList<NotionPushRecordResult> records,
        NotionPushResult result)
    {
        NotionPushReport report = NotionPushReportWriter.CreateReport(options.Mode, options.DryRun, records, result.Diagnostics);
        NotionPushReportWriter.WriteJson(options.ReportPath, report);
        string markdownReportPath = Path.ChangeExtension(options.ReportPath, ".md");
        NotionPushReportWriter.WriteMarkdown(markdownReportPath, report);

        return result with
        {
            Records = records,
            Artifacts =
            [
                new NotionPushArtifact(
                    "notion-push-report",
                    options.ReportPath,
                    "Notion push failure JSON report."),
                new NotionPushArtifact(
                    "notion-push-report-md",
                    markdownReportPath,
                    "Notion push failure Markdown report.")
            ]
        };
    }

}
