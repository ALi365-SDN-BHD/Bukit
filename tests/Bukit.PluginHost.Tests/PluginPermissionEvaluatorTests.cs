using Bukit.Plugin.Abstractions.Security;
using Bukit.PluginHost;
using Bukit.Shared;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginPermissionEvaluatorTests
{
    [Fact]
    public void ValidateGrantedPermissions_AllowsSubset()
    {
        var evaluator = new PluginPermissionEvaluator();

        evaluator.ValidateGrantedPermissions(
            "echo",
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: ["content"], Write: [".bukit/reports"]),
                Network: false,
                Environment: new PluginEnvironmentPermission(Read: ["BUKIT_TOKEN"])),
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: ["content"], Write: []),
                Network: false,
                Environment: new PluginEnvironmentPermission(Read: ["BUKIT_TOKEN"])));
    }

    [Fact]
    public void ValidateGrantedPermissions_AllowsRequiredPathInsideGrantedRoot()
    {
        var evaluator = new PluginPermissionEvaluator();

        evaluator.ValidateGrantedPermissions(
            "import",
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: ["."], Write: ["./themes"])),
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: ["./content/posts"], Write: ["themes/starter"])));
    }

    [Fact]
    public void ValidateGrantedPermissions_NormalizesEquivalentRelativePaths()
    {
        var evaluator = new PluginPermissionEvaluator();

        evaluator.ValidateGrantedPermissions(
            "import",
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: ["./content"], Write: ["themes"])),
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: ["content"], Write: ["./themes"])));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("/tmp")]
    [InlineData(".bukit/bin")]
    [InlineData(".bukit/plugins")]
    public void ValidateGrantedPermissions_RejectsUnsafeGrantedFileSystemPaths(string unsafePath)
    {
        var evaluator = new PluginPermissionEvaluator();

        ConfigException exception = Assert.Throws<ConfigException>(() => evaluator.ValidateGrantedPermissions(
            "echo",
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: [unsafePath])),
            new PluginPermissionSet()));

        Assert.Contains("fileSystem.read permission path", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("/tmp")]
    [InlineData(".bukit/bin")]
    [InlineData(".bukit/plugins")]
    public void ValidateGrantedPermissions_RejectsUnsafeRequiredFileSystemPaths(string unsafePath)
    {
        var evaluator = new PluginPermissionEvaluator();

        ConfigException exception = Assert.Throws<ConfigException>(() => evaluator.ValidateGrantedPermissions(
            "echo",
            new PluginPermissionSet(FileSystem: new PluginFileSystemPermission(Read: ["."])),
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: [unsafePath]))));

        Assert.Contains("fileSystem.read permission path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateGrantedPermissions_RejectsNetworkWhenNotGranted()
    {
        var evaluator = new PluginPermissionEvaluator();

        ConfigException exception = Assert.Throws<ConfigException>(() => evaluator.ValidateGrantedPermissions(
            "echo",
            new PluginPermissionSet(Network: false),
            new PluginPermissionSet(Network: true)));

        Assert.Contains("requires network permission", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateGrantedPermissions_RejectsEnvironmentWhenNotGranted()
    {
        var evaluator = new PluginPermissionEvaluator();

        ConfigException exception = Assert.Throws<ConfigException>(() => evaluator.ValidateGrantedPermissions(
            "echo",
            new PluginPermissionSet(Environment: new PluginEnvironmentPermission(Read: ["PUBLIC_VALUE"])),
            new PluginPermissionSet(Environment: new PluginEnvironmentPermission(Read: ["SECRET_VALUE"]))));

        Assert.Contains("requires environment.read permission: SECRET_VALUE", exception.Message, StringComparison.Ordinal);
    }
}
