namespace Bukit.Plugin.Abstractions.Results;

public sealed record PluginDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Path = null);
