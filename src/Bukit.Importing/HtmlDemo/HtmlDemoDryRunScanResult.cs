namespace Bukit.Importing.HtmlDemo;

public sealed record HtmlDemoDryRunScanResult(
    bool Success,
    int ExitCode,
    IReadOnlyList<HtmlDemoPageCandidate>? Pages = null,
    IReadOnlyList<HtmlDemoDryRunAsset>? Assets = null,
    IReadOnlyList<HtmlDemoDryRunLink>? Links = null,
    IReadOnlyList<HtmlDemoDryRunDiagnostic>? Diagnostics = null,
    IReadOnlyList<HtmlDemoDryRunArtifact>? Artifacts = null)
{
    public IReadOnlyList<HtmlDemoPageCandidate> Pages { get; init; } = Pages ?? [];
    public IReadOnlyList<HtmlDemoDryRunAsset> Assets { get; init; } = Assets ?? [];
    public IReadOnlyList<HtmlDemoDryRunLink> Links { get; init; } = Links ?? [];
    public IReadOnlyList<HtmlDemoDryRunDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
    public IReadOnlyList<HtmlDemoDryRunArtifact> Artifacts { get; init; } = Artifacts ?? [];
}

public sealed record HtmlDemoPageCandidate(
    string Source,
    string Slug,
    string Type,
    string? Title);

public sealed record HtmlDemoDryRunAsset(
    string Source,
    string Reference,
    string Path,
    bool Exists);
