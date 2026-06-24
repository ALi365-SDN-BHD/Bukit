namespace Bukit.PluginHost;

public sealed record PluginLockEntry(
    string Id,
    string Version,
    string Source,
    string Entry,
    string Platform,
    string Sha256,
    bool Sha256Verified);
