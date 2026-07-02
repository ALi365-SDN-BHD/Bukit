namespace Bukit.Plugin.Abstractions.Security;

public sealed record PluginPermissionSet(
    PluginFileSystemPermission? FileSystem = null,
    bool Network = false,
    PluginEnvironmentPermission? Environment = null)
{
    public PluginFileSystemPermission FileSystem { get; init; } = FileSystem ?? new PluginFileSystemPermission();
    public PluginEnvironmentPermission Environment { get; init; } = Environment ?? new PluginEnvironmentPermission();
}
