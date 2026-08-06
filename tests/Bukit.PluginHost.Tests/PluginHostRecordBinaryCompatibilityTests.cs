using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginHostRecordBinaryCompatibilityTests
{
    [Fact]
    public void PluginProcessRequest_KeepsEightValues()
    {
        var value = new PluginProcessRequest("tool", null, "{}", "/tmp",
            TimeSpan.FromSeconds(1), 10, 20, null)
        { MaxCpuTime = TimeSpan.FromMilliseconds(50), MaxMemoryBytes = 100 };
        var (path, arguments, input, directory, timeout, stdout, stderr, environment) = value;
        Assert.Equal("tool", path);
        Assert.NotNull(arguments);
        Assert.Empty(arguments);
        Assert.Equal("{}", input);
        Assert.Equal("/tmp", directory);
        Assert.Equal(TimeSpan.FromSeconds(1), timeout);
        Assert.Equal(10, stdout);
        Assert.Equal(20, stderr);
        Assert.NotNull(environment);
        Assert.Empty(environment);
        Assert.Equal(100, value.MaxMemoryBytes);
    }

    [Fact]
    public void PluginProcessResult_KeepsSixValues()
    {
        var value = new PluginProcessResult(0, "{}", "", false, false, null)
        { ResourceLimitExceeded = "cpu" };
        var (exitCode, stdout, stderr, timedOut, outputExceeded, stream) = value;
        Assert.Equal(0, exitCode);
        Assert.Equal("{}", stdout);
        Assert.Equal("", stderr);
        Assert.False(timedOut);
        Assert.False(outputExceeded);
        Assert.Null(stream);
        Assert.Equal("cpu", value.ResourceLimitExceeded);
    }

    [Fact]
    public void ProcessRunRequest_KeepsEightValues()
    {
        var value = new ProcessRunRequest("tool", null, "input", "/tmp",
            TimeSpan.FromSeconds(1), 10, 20, null)
        { MaxCpuTime = TimeSpan.FromMilliseconds(50), MaxMemoryBytes = 100 };
        var (path, arguments, input, directory, timeout, stdout, stderr, environment) = value;
        Assert.Equal("tool", path);
        Assert.NotNull(arguments);
        Assert.Empty(arguments);
        Assert.Equal("input", input);
        Assert.Equal("/tmp", directory);
        Assert.Equal(TimeSpan.FromSeconds(1), timeout);
        Assert.Equal(10, stdout);
        Assert.Equal(20, stderr);
        Assert.NotNull(environment);
        Assert.Empty(environment);
        Assert.Equal(TimeSpan.FromMilliseconds(50), value.MaxCpuTime);
    }

    [Fact]
    public void ProcessRunResult_KeepsSixValues()
    {
        var value = new ProcessRunResult(0, "ok", "", false, false, null)
        { ResourceLimitExceeded = "memory" };
        var (exitCode, stdout, stderr, timedOut, outputExceeded, stream) = value;
        Assert.Equal(0, exitCode);
        Assert.Equal("ok", stdout);
        Assert.Equal("", stderr);
        Assert.False(timedOut);
        Assert.False(outputExceeded);
        Assert.Null(stream);
        Assert.Equal("memory", value.ResourceLimitExceeded);
    }

    [Fact]
    public void ResolvedPlugin_KeepsThirteenValues()
    {
        var host = new PluginHostInfo("Bukit", "2.0.0", "osx-arm64");
        var value = new ResolvedPlugin("id", "1", "osx-arm64", "tool", "/tmp",
            host, null, null, null, null, null, null, null)
        { Resources = new PluginResourceLimitOptions(50, 100) };
        var (id, version, platform, executable, workingDirectory, resolvedHost,
            projectRoot, arguments, timeout, output, permissions, environment,
            sha256Verified) = value;
        Assert.Equal("id", id);
        Assert.Equal("1", version);
        Assert.Equal("osx-arm64", platform);
        Assert.Equal("tool", executable);
        Assert.Equal("/tmp", workingDirectory);
        Assert.Same(host, resolvedHost);
        Assert.Null(projectRoot);
        Assert.NotNull(arguments);
        Assert.Empty(arguments);
        Assert.NotNull(timeout);
        Assert.NotNull(output);
        Assert.NotNull(permissions);
        Assert.NotNull(environment);
        Assert.Empty(environment);
        Assert.Null(sha256Verified);
        Assert.Equal(100, value.Resources!.MaxMemoryBytes);
    }
}
