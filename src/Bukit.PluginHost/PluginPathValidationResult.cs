namespace Bukit.PluginHost;

public sealed record PluginPathValidationResult(
    bool Success,
    string? FullPath = null,
    string? NormalizedRelativePath = null,
    string? Message = null)
{
    public static PluginPathValidationResult Valid(string fullPath, string normalizedRelativePath)
        => new(true, fullPath, normalizedRelativePath);

    public static PluginPathValidationResult Invalid(string message)
        => new(false, Message: message);
}
