namespace Bukit.Importing;

public sealed record ImportCommandResult
{
    public int ExitCode { get; init; }
    public ImportResult? HtmlDemoResult { get; init; }
    public ImportSeedResult? SeedResult { get; init; }
    public IReadOnlyList<ImportCommandMessage> Messages { get; init; } = [];
    public IReadOnlyList<ImportCommandDiagnostic> Diagnostics { get; init; } = [];
    public IReadOnlyList<ImportCommandArtifact> Artifacts { get; init; } = [];

    public bool Success => ExitCode == 0;
}

public sealed record ImportCommandMessage(string Level, string Message);

public sealed record ImportCommandDiagnostic(string Code, string Severity, string Message, string? Path = null);

public sealed record ImportCommandArtifact(string Type, string Path, string? Description = null);

public sealed record ImportSeedResult
{
    public required string InputDir { get; init; }
    public required string OutputDir { get; init; }
    public int RecordsRead { get; init; }
    public int FilesWritten { get; init; }
    public IReadOnlyList<string> WrittenFiles { get; init; } = [];
}
