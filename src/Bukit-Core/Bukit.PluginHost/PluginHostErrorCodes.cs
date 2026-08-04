namespace Bukit.PluginHost;

internal static class PluginHostErrorCodes
{
    public const string UnsupportedProtocol = "plugin.unsupportedProtocol";
    public const string InvalidResponse = "plugin.invalidResponse";
    public const string Timeout = "plugin.timeout";
    public const string ExecutionFailed = "plugin.executionFailed";
    public const string PermissionDenied = "plugin.permissionDenied";
    public const string OutputTooLarge = "plugin.outputTooLarge";
    public const string ResourceLimitExceeded = "plugin.resourceLimitExceeded";
    public const string ResourceLimitUnsupported = "plugin.resourceLimitUnsupported";
}
