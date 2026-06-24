namespace Bukit.PluginHost;

public sealed record PluginHashVerificationResult(
    bool Success,
    string ActualSha256,
    string ExpectedSha256,
    string? Message = null);
