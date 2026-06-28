using Bukit.Notion.Client;
using Bukit.Notion.Mapping;
using Bukit.Notion.Push;

namespace Bukit.Notion.RemoteSchema;

public sealed class NotionRemoteSchemaValidationService : INotionRemoteSchemaValidationService
{
    private const string ReportSchema = "bukit.notion.schema.validation.report.v1";
    private readonly INotionClientFactory _clientFactory;
    private readonly INotionTokenProvider _tokenProvider;

    public NotionRemoteSchemaValidationService()
        : this(new HttpNotionClientFactory(), new EnvironmentNotionTokenProvider())
    {
    }

    public NotionRemoteSchemaValidationService(
        INotionClientFactory clientFactory,
        INotionTokenProvider tokenProvider)
    {
        _clientFactory = clientFactory;
        _tokenProvider = tokenProvider;
    }

    public NotionRemoteSchemaValidationResult Validate(NotionRemoteSchemaOptions options)
        => ValidateAsync(options, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<NotionRemoteSchemaValidationResult> ValidateAsync(
        NotionRemoteSchemaOptions options,
        CancellationToken cancellationToken)
    {
        NotionDatabaseMapValidationResult mapValidation = NotionDatabaseMapValidator.Validate(
            options.ProjectRoot,
            options.DatabaseMapPath);
        if (!mapValidation.Success)
        {
            return WriteResult(
                options,
                success: false,
                exitCode: 2,
                [],
                mapValidation.Diagnostics.Select(diagnostic => new NotionRemoteSchemaDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message,
                    diagnostic.Path)).ToArray());
        }

        string? token = _tokenProvider.GetToken(options.TokenEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(token))
        {
            return WriteResult(
                options,
                success: false,
                exitCode: 2,
                [],
                [
                    new NotionRemoteSchemaDiagnostic(
                        "notion.tokenMissing",
                        NotionDiagnosticSeverity.Error,
                        $"Environment variable {options.TokenEnvironmentVariable} is required for remote schema validation.")
                ]);
        }

        INotionClient client = _clientFactory.Create(new NotionRequestOptions(token));
        var dataSources = new List<NotionRemoteSchemaDataSourceResult>();
        foreach (NotionDatabaseMapEntry entry in mapValidation.DatabaseMap!.Databases.Values
                     .OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            NotionDataSourceResult remote = await client.RetrieveDataSourceAsync(
                entry.EffectiveDataSourceId!,
                cancellationToken).ConfigureAwait(false);
            dataSources.Add(Compare(entry, remote));
        }

        return WriteResult(options, success: true, exitCode: 0, dataSources, []);
    }

    private static NotionRemoteSchemaDataSourceResult Compare(
        NotionDatabaseMapEntry entry,
        NotionDataSourceResult remote)
    {
        IReadOnlyList<NotionRemoteSchemaPropertyResult> properties = entry.Properties.Values
            .OrderBy(mapping => mapping.Name, StringComparer.Ordinal)
            .Select(mapping => new NotionRemoteSchemaPropertyResult(
                mapping.Name,
                mapping.Type,
                remote.Properties.TryGetValue(mapping.Name, out string? actualType) ? actualType : null,
                "matched"))
            .ToArray();
        string? titleProperty = remote.Properties
            .Where(property => string.Equals(property.Value, "title", StringComparison.Ordinal))
            .Select(property => property.Key)
            .SingleOrDefault();
        return new NotionRemoteSchemaDataSourceResult(
            entry.Name,
            entry.Collection,
            remote.Id,
            string.IsNullOrWhiteSpace(entry.DataSourceId) ? "databaseId" : "dataSourceId",
            true,
            titleProperty,
            entry.UniqueField,
            properties,
            []);
    }

    private static NotionRemoteSchemaValidationResult WriteResult(
        NotionRemoteSchemaOptions options,
        bool success,
        int exitCode,
        IReadOnlyList<NotionRemoteSchemaDataSourceResult> dataSources,
        IReadOnlyList<NotionRemoteSchemaDiagnostic> diagnostics)
    {
        string databaseMap = Path.GetRelativePath(options.ProjectRoot, options.DatabaseMapPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        var report = new NotionRemoteSchemaReport(
            ReportSchema,
            success,
            databaseMap,
            dataSources,
            diagnostics);
        NotionRemoteSchemaReportWriter.WriteJson(options.ReportPath, report);
        string markdownPath = Path.ChangeExtension(options.ReportPath, ".md");
        NotionRemoteSchemaReportWriter.WriteMarkdown(markdownPath, report);
        return new NotionRemoteSchemaValidationResult(
            success,
            exitCode,
            dataSources,
            diagnostics,
            [
                new NotionRemoteSchemaArtifact(
                    "notion-schema-validation-report",
                    options.ReportPath,
                    "Notion remote schema validation JSON report."),
                new NotionRemoteSchemaArtifact(
                    "notion-schema-validation-report-md",
                    markdownPath,
                    "Notion remote schema validation Markdown report.")
            ]);
    }
}
