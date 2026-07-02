namespace Bukit.PluginHost;

public sealed record PluginLockEntry(
    string Id,
    string Version,
    string Source,
    string ManifestVersion,
    string Protocol,
    string Entry,
    string Platform,
    string Sha256,
    IReadOnlyList<string>? Commands,
    DateTimeOffset ResolvedAt,
    bool Sha256Verified);
