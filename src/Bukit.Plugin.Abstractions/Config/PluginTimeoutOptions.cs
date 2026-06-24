namespace Bukit.Plugin.Abstractions.Config;

public sealed record PluginTimeoutOptions(
    int HandshakeMs = 5000,
    int ManifestMs = 5000,
    int InvokeMs = 120000);
