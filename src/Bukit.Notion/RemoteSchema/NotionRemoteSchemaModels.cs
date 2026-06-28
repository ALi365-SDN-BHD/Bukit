namespace Bukit.Notion.RemoteSchema;

public sealed record NotionRemoteSchemaOptions(
    string ProjectRoot,
    string DatabaseMapPath,
    string ReportPath,
    string TokenEnvironmentVariable);

public sealed record NotionRemoteSchemaDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null);

public sealed record NotionRemoteSchemaPropertyResult(
    string Name,
    string? ExpectedType,
    string? ActualType,
    string Status);

public sealed record NotionRemoteSchemaDataSourceResult(
    string Entry,
    string? Collection,
    string DataSourceId,
    string IdentifierSource,
    bool Success,
    string? TitleProperty,
    string? UniqueField,
    IReadOnlyList<NotionRemoteSchemaPropertyResult>? Properties = null,
    IReadOnlyList<NotionRemoteSchemaDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<NotionRemoteSchemaPropertyResult> Properties { get; init; } = Properties ?? [];

    public IReadOnlyList<NotionRemoteSchemaDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public sealed record NotionRemoteSchemaArtifact(
    string Type,
    string Path,
    string Description);

public sealed record NotionRemoteSchemaValidationResult(
    bool Success,
    int ExitCode,
    IReadOnlyList<NotionRemoteSchemaDataSourceResult>? DataSources = null,
    IReadOnlyList<NotionRemoteSchemaDiagnostic>? Diagnostics = null,
    IReadOnlyList<NotionRemoteSchemaArtifact>? Artifacts = null)
{
    public IReadOnlyList<NotionRemoteSchemaDataSourceResult> DataSources { get; init; } = DataSources ?? [];

    public IReadOnlyList<NotionRemoteSchemaDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];

    public IReadOnlyList<NotionRemoteSchemaArtifact> Artifacts { get; init; } = Artifacts ?? [];
}

public sealed record NotionRemoteSchemaReport(
    string Schema,
    bool Success,
    string DatabaseMap,
    IReadOnlyList<NotionRemoteSchemaDataSourceResult>? DataSources = null,
    IReadOnlyList<NotionRemoteSchemaDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<NotionRemoteSchemaDataSourceResult> DataSources { get; init; } = DataSources ?? [];

    public IReadOnlyList<NotionRemoteSchemaDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}
