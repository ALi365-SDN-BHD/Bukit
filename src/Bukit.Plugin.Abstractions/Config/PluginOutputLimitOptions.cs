namespace Bukit.Plugin.Abstractions.Config;

public sealed record PluginOutputLimitOptions(
    int StdoutMaxBytes = 4194304,
    int StderrMaxBytes = 4194304,
    int ResponseMaxBytes = 4194304);
