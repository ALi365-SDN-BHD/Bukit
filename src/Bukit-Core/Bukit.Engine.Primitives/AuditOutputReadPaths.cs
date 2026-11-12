namespace Bukit.Engine.Primitives;

/// <summary>
/// Explicit audit read locations for a language whose output directory is the output root.
/// A null member means "this run does not provide that file"; the whole record being null
/// keeps the legacy behaviour of reading the language's own directory.
/// </summary>
internal sealed record AuditOutputReadPaths(
    string? Sitemap,
    string? Search,
    string? Rss,
    string? Atom,
    string? JsonFeed,
    string? AgentManifest,
    string? Robots,
    string? Llms,
    string? LlmsFull);
