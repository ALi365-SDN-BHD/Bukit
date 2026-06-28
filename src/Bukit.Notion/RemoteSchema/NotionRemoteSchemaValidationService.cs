using System.Net;
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

        if (!NotionPluginConstants.IsAllowedTokenEnvironmentVariable(options.TokenEnvironmentVariable))
        {
            return WriteResult(
                options,
                success: false,
                exitCode: 2,
                [],
                [
                    new NotionRemoteSchemaDiagnostic(
                        "notion.tokenEnvNotAllowed",
                        NotionDiagnosticSeverity.Error,
                        "Remote schema validation token must come from an allowlisted environment variable.")
                ]);
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
        bool runtimeFailure = false;
        foreach (NotionDatabaseMapEntry entry in mapValidation.DatabaseMap!.Databases.Values
                     .OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                NotionDataSourceResult remote = await client.RetrieveDataSourceAsync(
                    entry.EffectiveDataSourceId!,
                    cancellationToken).ConfigureAwait(false);
                dataSources.Add(Compare(entry, remote));
            }
            catch (NotionApiException ex)
            {
                string code = ex.StatusCode == HttpStatusCode.NotFound
                    ? "notion.remoteSchemaDataSourceNotFound"
                    : MapApiDiagnosticCode(ex);
                runtimeFailure |= ex.StatusCode != HttpStatusCode.NotFound;
                dataSources.Add(FailedDataSource(entry, code, ex.Message));
            }
            catch (HttpRequestException ex)
            {
                runtimeFailure = true;
                dataSources.Add(FailedDataSource(entry, "notion.httpError", ex.Message));
            }
        }

        IReadOnlyList<NotionRemoteSchemaDiagnostic> diagnostics = dataSources
            .SelectMany(dataSource => dataSource.Diagnostics)
            .ToArray();
        bool success = diagnostics.Count == 0;
        int exitCode = success ? 0 : runtimeFailure ? 1 : 2;
        return WriteResult(options, success, exitCode, dataSources, diagnostics);
    }

    private static NotionRemoteSchemaDataSourceResult Compare(
        NotionDatabaseMapEntry entry,
        NotionDataSourceResult remote)
    {
        var diagnostics = new List<NotionRemoteSchemaDiagnostic>();
        var properties = new List<NotionRemoteSchemaPropertyResult>();
        foreach (NotionPropertyMapping mapping in entry.Properties.Values
                     .OrderBy(mapping => mapping.Name, StringComparer.Ordinal))
        {
            if (!remote.Properties.TryGetValue(mapping.Name, out string? actualType))
            {
                properties.Add(new NotionRemoteSchemaPropertyResult(
                    mapping.Name,
                    mapping.Type,
                    null,
                    "missing"));
                diagnostics.Add(Error(
                    "notion.remoteSchemaPropertyMissing",
                    $"Remote property {mapping.Name} does not exist.",
                    $"{entry.Name}.properties.{mapping.Name}"));
            }
            else if (!string.Equals(mapping.Type, actualType, StringComparison.Ordinal))
            {
                properties.Add(new NotionRemoteSchemaPropertyResult(
                    mapping.Name,
                    mapping.Type,
                    actualType,
                    "type-mismatch"));
                diagnostics.Add(Error(
                    "notion.remoteSchemaPropertyTypeMismatch",
                    $"Remote property {mapping.Name} has type {actualType ?? "unknown"}; expected {mapping.Type}.",
                    $"{entry.Name}.properties.{mapping.Name}"));
            }
            else
            {
                properties.Add(new NotionRemoteSchemaPropertyResult(
                    mapping.Name,
                    mapping.Type,
                    actualType,
                    "matched"));
            }
        }

        string[] titleProperties = remote.Properties
            .Where(property => string.Equals(property.Value, "title", StringComparison.Ordinal))
            .Select(property => property.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string? titleProperty = titleProperties.Length == 1 ? titleProperties[0] : null;
        if (titleProperties.Length == 0)
        {
            diagnostics.Add(Error(
                "notion.remoteSchemaTitleMissing",
                "Remote data source must contain exactly one title property.",
                $"{entry.Name}.properties"));
        }
        else if (titleProperties.Length > 1)
        {
            diagnostics.Add(Error(
                "notion.remoteSchemaTitleNotUnique",
                "Remote data source contains more than one title property.",
                $"{entry.Name}.properties"));
        }

        if (!string.IsNullOrWhiteSpace(entry.UniqueField)
            && !remote.Properties.ContainsKey(entry.UniqueField))
        {
            diagnostics.Add(Error(
                "notion.remoteSchemaUniqueFieldMissing",
                $"Remote unique field {entry.UniqueField} does not exist.",
                $"{entry.Name}.uniqueField"));
        }

        return new NotionRemoteSchemaDataSourceResult(
            entry.Name,
            entry.Collection,
            remote.Id,
            string.IsNullOrWhiteSpace(entry.DataSourceId) ? "databaseId" : "dataSourceId",
            diagnostics.Count == 0,
            titleProperty,
            entry.UniqueField,
            properties,
            diagnostics);
    }

    private static NotionRemoteSchemaValidationResult WriteResult(
        NotionRemoteSchemaOptions options,
        bool success,
        int exitCode,
        IReadOnlyList<NotionRemoteSchemaDataSourceResult> dataSources,
        IReadOnlyList<NotionRemoteSchemaDiagnostic> diagnostics)
    {
        var finalDiagnostics = new List<NotionRemoteSchemaDiagnostic>(diagnostics);
        if (!success
            && !finalDiagnostics.Any(diagnostic => string.Equals(
                diagnostic.Code,
                "notion.remoteSchemaValidationFailed",
                StringComparison.Ordinal)))
        {
            finalDiagnostics.Add(Error(
                "notion.remoteSchemaValidationFailed",
                "Notion remote schema validation failed.",
                options.DatabaseMapPath));
        }

        string databaseMap = Path.GetRelativePath(options.ProjectRoot, options.DatabaseMapPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        var report = new NotionRemoteSchemaReport(
            ReportSchema,
            success,
            databaseMap,
            dataSources,
            finalDiagnostics);
        NotionRemoteSchemaReportWriter.WriteJson(options.ReportPath, report);
        string markdownPath = Path.ChangeExtension(options.ReportPath, ".md");
        NotionRemoteSchemaReportWriter.WriteMarkdown(markdownPath, report);
        return new NotionRemoteSchemaValidationResult(
            success,
            exitCode,
            dataSources,
            finalDiagnostics,
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

    private static NotionRemoteSchemaDataSourceResult FailedDataSource(
        NotionDatabaseMapEntry entry,
        string code,
        string message)
    {
        NotionRemoteSchemaDiagnostic diagnostic = Error(
            code,
            message,
            entry.EffectiveDataSourceId);
        return new NotionRemoteSchemaDataSourceResult(
            entry.Name,
            entry.Collection,
            entry.EffectiveDataSourceId!,
            string.IsNullOrWhiteSpace(entry.DataSourceId) ? "databaseId" : "dataSourceId",
            false,
            null,
            entry.UniqueField,
            [],
            [diagnostic]);
    }

    private static string MapApiDiagnosticCode(NotionApiException exception)
        => (int)exception.StatusCode switch
        {
            401 => "notion.apiUnauthorized",
            403 => "notion.apiForbidden",
            409 => "notion.apiConflict",
            429 => "notion.rateLimited",
            >= 500 and <= 599 => "notion.apiFailed",
            _ => "notion.apiError"
        };

    private static NotionRemoteSchemaDiagnostic Error(
        string code,
        string message,
        string? path = null)
        => new(code, NotionDiagnosticSeverity.Error, message, path);
}
