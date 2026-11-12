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
    string? LlmsFull)
{
    /// <summary>
    /// Legacy read locations inside a language's own output directory. Feeds fall back to the
    /// output directory root when the configured feed path has no file, as before.
    /// </summary>
    internal static AuditOutputReadPaths ForOutputDirectory(string outputDir, string feedPath)
        => new(
            Path.Combine(outputDir, "sitemap.xml"),
            Path.Combine(outputDir, "search.json"),
            Path.Combine(outputDir, "rss.xml"),
            FirstExisting(Path.Combine(outputDir, feedPath, "atom.xml"), Path.Combine(outputDir, "atom.xml")),
            FirstExisting(Path.Combine(outputDir, feedPath, "feed.json"), Path.Combine(outputDir, "feed.json")),
            Path.Combine(outputDir, "agent-manifest.json"),
            Path.Combine(outputDir, "robots.txt"),
            Path.Combine(outputDir, "llms.txt"),
            Path.Combine(outputDir, "llms-full.txt"));

    private static string FirstExisting(string preferred, string fallback)
        => File.Exists(preferred) ? preferred : fallback;
}
