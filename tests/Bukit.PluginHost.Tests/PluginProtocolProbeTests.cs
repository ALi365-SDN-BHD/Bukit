using System.Diagnostics;
using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.PluginProcessProbe;
using Bukit.Shared;
using Xunit;

namespace Bukit.PluginHost.Tests;

[Trait("Category", "Integration")]
public sealed class PluginProtocolProbeTests
{
    [Fact]
    public async Task Handshake_RealProcessReturnsIdentityAndDynamicRequestId()
    {
        using var directory = TestDirectory.Create();
        var plugin = CreatePlugin(directory.Path, ["handshake"]);
        var requestIds = new ProbeRequestIdFactory();
        var client = new PluginProtocolClient(new PluginProcessInvoker(new SystemProcessRunner()), requestIds);

        var response = await client.HandshakeAsync(plugin, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.True(response.Success);
        Assert.Equal("handshakeResponse", response.Type);
        Assert.Equal(PluginProtocolConstants.ProtocolVersion, response.Protocol);
        Assert.True(Guid.TryParseExact(response.RequestId, "N", out _));
        Assert.Equal(requestIds.LastRequestId, response.RequestId);
        Assert.NotNull(response.Plugin);
        Assert.Equal(plugin.Id, response.Plugin.Id);
        Assert.Equal(plugin.Version, response.Plugin.Version);
        Assert.Equal(plugin.Platform, response.Plugin.Platform);
    }

    [Fact]
    public async Task Timeout_RealProcessIsRejectedWithinBoundedTime()
    {
        using var directory = TestDirectory.Create();
        var plugin = CreatePlugin(directory.Path, ["sleep", "30000"], timeoutMs: 2000);
        var client = new PluginProtocolClient(new PluginProcessInvoker(new SystemProcessRunner()), new PluginRequestIdFactory());
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<ConfigException>(() =>
            client.HandshakeAsync(plugin, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15)));

        Assert.Equal(DiagnosticCode.PluginExecutionFailed, exception.Code);
        Assert.Equal("plugin.timeout: Plugin process timed out.", exception.Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task AbnormalTermination_RealProcessIsRejectedWithinBoundedTime()
    {
        using var directory = TestDirectory.Create();
        var plugin = CreatePlugin(directory.Path, ["self-kill"]);
        var client = new PluginProtocolClient(new PluginProcessInvoker(new SystemProcessRunner()), new PluginRequestIdFactory());
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<ConfigException>(() =>
            client.HandshakeAsync(plugin, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20)));

        Assert.Equal(DiagnosticCode.PluginExecutionFailed, exception.Code);
        // SIGKILL/TerminateProcess: Unix 137 (128 + 9), Windows TerminateProcess(-1).
        int expectedExitCode = OperatingSystem.IsWindows() ? -1 : 137;
        Assert.Equal(
            $"plugin.executionFailed: Plugin process exited with code {expectedExitCode}.",
            exception.Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15));
    }

    [Theory]
    [InlineData("exit", "7", 4096, 4096, "plugin.executionFailed: Plugin process exited with code 7.")]
    [InlineData("garbage", "json", 4096, 4096, "plugin.invalidResponse: Plugin stdout was not valid protocol JSON.")]
    [InlineData("garbage", "invalid-utf8", 4096, 4096, "plugin.invalidResponse: Plugin stdout was not valid protocol JSON.")]
    [InlineData("stdout-bytes", "2048", 1024, 4096, "plugin.outputTooLarge: Plugin process output exceeded configured limits.")]
    [InlineData("stdout-bytes", "2048", 4096, 1024, "plugin.outputTooLarge: Plugin process output exceeded configured limits.")]
    public async Task FaultMode_RealProcessProducesHostRejection(
        string command,
        string argument,
        int stdoutMaxBytes,
        int responseMaxBytes,
        string diagnostic)
    {
        using var directory = TestDirectory.Create();
        var plugin = CreatePlugin(directory.Path, [command, argument], stdoutMaxBytes: stdoutMaxBytes, responseMaxBytes: responseMaxBytes);
        var client = new PluginProtocolClient(new PluginProcessInvoker(new SystemProcessRunner()), new PluginRequestIdFactory());

        var exception = await Assert.ThrowsAsync<ConfigException>(() =>
            client.HandshakeAsync(plugin, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20)));

        Assert.Equal(DiagnosticCode.PluginExecutionFailed, exception.Code);
        Assert.Equal(diagnostic, exception.Message);
    }

    internal static ResolvedPlugin CreatePlugin(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        int timeoutMs = 10000,
        int stdoutMaxBytes = 4096,
        int responseMaxBytes = 4096)
    {
        var platform = new PluginPlatformResolver().GetCurrentRid();
        return new ResolvedPlugin(
            Id: "process-probe",
            Version: "1.0.0",
            Platform: platform,
            ExecutablePath: ResolveDotnetHost(),
            WorkingDirectory: workingDirectory,
            Host: new PluginHostInfo("bukit", "test", platform),
            Arguments: [typeof(ProbeMarker).Assembly.Location, .. arguments],
            Timeout: new PluginTimeoutOptions(HandshakeMs: timeoutMs),
            Output: new PluginOutputLimitOptions(StdoutMaxBytes: stdoutMaxBytes, StderrMaxBytes: 4096, ResponseMaxBytes: responseMaxBytes));
    }

    private static string ResolveDotnetHost()
    {
        string hostName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var candidates = new List<string?> { Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") };
        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            candidates.Add(Path.Combine(dotnetRoot, hostName));
        }

        string? currentHost = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.Equals(Path.GetFileName(currentHost), hostName, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(currentHost);
        }

        string? searchPath = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(searchPath))
        {
            candidates.AddRange(searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory, hostName)));
        }

        string? resolved = candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate));
        return resolved is null
            ? throw new InvalidOperationException("The dotnet host executable could not be resolved for the process probe.")
            : Path.GetFullPath(resolved);
    }
}

internal sealed class ProbeRequestIdFactory : IPluginRequestIdFactory
{
    internal string? LastRequestId { get; private set; }

    public string Create() => LastRequestId = new PluginRequestIdFactory().Create();
}
