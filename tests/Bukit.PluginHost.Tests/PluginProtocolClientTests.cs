using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.PluginHost;
using Bukit.Shared;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginProtocolClientTests
{
    [Fact]
    public async Task HandshakeAsync_SendsHandshakeRequestAndReturnsResponse()
    {
        var invoker = new StubPluginProcessInvoker(
            """
            {"type":"handshakeResponse","protocol":"bukit-plugin-v1","requestId":"req-1","success":true,"plugin":{"id":"echo","name":"Echo","version":"0.1.0","platform":"osx-arm64","capabilities":["echo"]}}
            """);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-1"));

        PluginHandshakeResponse response = await client.HandshakeAsync(CreatePlugin(), CancellationToken.None);

        Assert.Equal("echo", response.Plugin?.Id);
        Assert.Equal("0.1.0", response.Plugin?.Version);
        Assert.Contains("\"type\":\"handshake\"", invoker.Request?.StandardInputJson);
        Assert.Contains("\"requestId\":\"req-1\"", invoker.Request?.StandardInputJson);
        Assert.Equal(TimeSpan.FromMilliseconds(5000), invoker.Request?.Timeout);
    }

    [Theory]
    [InlineData("bad-protocol", PluginHostErrorCodes.UnsupportedProtocol)]
    [InlineData("bukit-plugin-v1", PluginHostErrorCodes.InvalidResponse)]
    public async Task HandshakeAsync_RejectsInvalidProtocolOrMismatchedRequestId(string protocol, string expectedCode)
    {
        string requestId = expectedCode == PluginHostErrorCodes.UnsupportedProtocol ? "req-1" : "other";
        var invoker = new StubPluginProcessInvoker(
            "{\"type\":\"handshakeResponse\",\"protocol\":\"" + protocol +
            "\",\"requestId\":\"" + requestId +
            "\",\"success\":true,\"plugin\":{\"id\":\"echo\",\"name\":\"Echo\",\"version\":\"0.1.0\",\"platform\":\"osx-arm64\"}}");
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-1"));

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => client.HandshakeAsync(CreatePlugin(), CancellationToken.None));

        Assert.Contains(expectedCode, exception.Message);
    }

    [Fact]
    public async Task HandshakeAsync_RejectsMismatchedPluginId()
    {
        var invoker = new StubPluginProcessInvoker(
            """
            {"type":"handshakeResponse","protocol":"bukit-plugin-v1","requestId":"req-1","success":true,"plugin":{"id":"wrong","name":"Wrong","version":"0.1.0","platform":"osx-arm64"}}
            """);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-1"));

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => client.HandshakeAsync(CreatePlugin(), CancellationToken.None));

        Assert.Contains(PluginHostErrorCodes.InvalidResponse, exception.Message);
    }

    [Fact]
    public async Task GetManifestAsync_ReturnsCommandsAndPermissions()
    {
        var invoker = new StubPluginProcessInvoker(
            """
            {"type":"manifestResponse","protocol":"bukit-plugin-v1","requestId":"req-2","success":true,"capabilities":["echo"],"commands":[{"name":"echo","description":"Echo text"}],"requiredPermissions":{"network":false}}
            """);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-2"));

        PluginManifestResponse response = await client.GetManifestAsync(CreatePlugin(), CancellationToken.None);

        Assert.Equal("echo", Assert.Single(response.Commands).Name);
        Assert.Equal("echo", Assert.Single(response.Capabilities));
        Assert.Contains("\"type\":\"manifest\"", invoker.Request?.StandardInputJson);
        Assert.Equal(TimeSpan.FromMilliseconds(5000), invoker.Request?.Timeout);
    }

    [Fact]
    public async Task GetManifestAsync_RejectsInvalidJson()
    {
        var invoker = new StubPluginProcessInvoker("not json");
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-2"));

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => client.GetManifestAsync(CreatePlugin(), CancellationToken.None));

        Assert.Contains(PluginHostErrorCodes.InvalidResponse, exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsResponseAndUsesInvokeTimeout()
    {
        var invoker = new StubPluginProcessInvoker(
            """
            {"type":"invokeResponse","protocol":"bukit-plugin-v1","requestId":"req-3","success":true,"exitCode":0,"artifacts":[{"type":"file","path":"out/result.json"}]}
            """);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-3"));

        PluginInvokeResponse response = await client.InvokeAsync(CreatePlugin(), CreateInvokeRequest(), CancellationToken.None);

        Assert.Equal(0, response.ExitCode);
        Assert.Equal("out/result.json", Assert.Single(response.Artifacts).Path);
        Assert.Contains("\"type\":\"invoke\"", invoker.Request?.StandardInputJson);
        Assert.Equal(TimeSpan.FromMilliseconds(120000), invoker.Request?.Timeout);
    }

    [Fact]
    public async Task InvokeAsync_RejectsNonZeroProcessExitWithInvalidResponse()
    {
        var invoker = new StubPluginProcessInvoker("{}", exitCode: 7);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-3"));

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => client.InvokeAsync(CreatePlugin(), CreateInvokeRequest(), CancellationToken.None));

        Assert.Contains(PluginHostErrorCodes.InvalidResponse, exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_RejectsTimeout()
    {
        var invoker = new StubPluginProcessInvoker("{}", timedOut: true);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-3"));

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => client.InvokeAsync(CreatePlugin(), CreateInvokeRequest(), CancellationToken.None));

        Assert.Contains(PluginHostErrorCodes.Timeout, exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_RejectsOutputTooLarge()
    {
        var invoker = new StubPluginProcessInvoker("{}", outputLimitExceeded: true);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-3"));

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => client.InvokeAsync(CreatePlugin(), CreateInvokeRequest(), CancellationToken.None));

        Assert.Contains(PluginHostErrorCodes.OutputTooLarge, exception.Message);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsBusinessFailureResponseWithDiagnostics()
    {
        var invoker = new StubPluginProcessInvoker(
            """
            {"type":"invokeResponse","protocol":"bukit-plugin-v1","requestId":"req-3","success":false,"exitCode":2,"diagnostics":[{"code":"plugin.input.invalid","severity":"error","message":"Invalid input"}]}
            """);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-3"));

        PluginInvokeResponse response = await client.InvokeAsync(CreatePlugin(), CreateInvokeRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(2, response.ExitCode);
        Assert.Equal("plugin.input.invalid", Assert.Single(response.Diagnostics).Code);
    }

    [Fact]
    public async Task InvokeAsync_WritesDetailedExecutionReport()
    {
        using var directory = TestDirectory.Create();
        var invoker = new StubPluginProcessInvoker(
            """
            {"type":"invokeResponse","protocol":"bukit-plugin-v1","requestId":"req-3","success":false,"exitCode":2,"diagnostics":[{"code":"plugin.input.invalid","severity":"error","message":"Invalid input","path":"content/index.md"}],"artifacts":[{"type":"file","path":"out/result.json","description":"Result"}]}
            """,
            exitCode: 2);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-3"));

        await client.InvokeAsync(
            CreatePlugin(directory.Path),
            CreateInvokeRequest(
                directory.Path,
                new PluginPermissionSet(
                    FileSystem: new PluginFileSystemPermission(Read: ["content"], Write: ["public"]),
                    Network: true,
                    Environment: new PluginEnvironmentPermission(Read: ["NOTION_TOKEN"]))),
            CancellationToken.None);

        string reportDirectory = Path.Combine(directory.Path, ".bukit", "reports", "plugin-executions");
        string reportPath = Assert.Single(Directory.EnumerateFiles(reportDirectory, "*.json"));
        string json = File.ReadAllText(reportPath);
        Assert.Contains("\"pluginVersion\": \"0.1.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"protocol\": \"bukit-plugin-v1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"platform\": \"osx-arm64\"", json, StringComparison.Ordinal);
        Assert.Contains("\"command\": \"echo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"entry\": \"plugins/echo/bin/osx-arm64/bukit-plugin-echo\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.Path, json, StringComparison.Ordinal);
        Assert.Contains("\"durationMs\": ", json, StringComparison.Ordinal);
        Assert.Contains("\"responseExitCode\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"permissions\"", json, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics\"", json, StringComparison.Ordinal);
        Assert.Contains("\"plugin.input.invalid\"", json, StringComparison.Ordinal);
        Assert.Contains("\"artifacts\"", json, StringComparison.Ordinal);
        Assert.Contains("\"out/result.json\"", json, StringComparison.Ordinal);
        Assert.Contains("\"responseSummary\"", json, StringComparison.Ordinal);
        Assert.Contains("\"success\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"diagnosticCodes\"", json, StringComparison.Ordinal);
        Assert.Contains("\"artifactCount\": 1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"stdout\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsValidResponseWhenProcessExitIsNonZero()
    {
        var invoker = new StubPluginProcessInvoker(
            """
            {"type":"invokeResponse","protocol":"bukit-plugin-v1","requestId":"req-3","success":false,"exitCode":2,"diagnostics":[{"code":"plugin.input.invalid","severity":"error","message":"Invalid input"}]}
            """,
            exitCode: 7);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-3"));

        PluginInvokeResponse response = await client.InvokeAsync(CreatePlugin(), CreateInvokeRequest(), CancellationToken.None);

        Assert.Equal(2, response.ExitCode);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Code == "plugin.input.invalid");
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Code == "plugin.processExitMismatch");
    }

    [Fact]
    public async Task InvokeAsync_RejectsArtifactPathTraversal()
    {
        var invoker = new StubPluginProcessInvoker(
            """
            {"type":"invokeResponse","protocol":"bukit-plugin-v1","requestId":"req-3","success":true,"exitCode":0,"artifacts":[{"type":"file","path":"../evil.txt"}]}
            """);
        var client = new PluginProtocolClient(invoker, new FixedRequestIdFactory("req-3"));

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => client.InvokeAsync(CreatePlugin(), CreateInvokeRequest(), CancellationToken.None));

        Assert.Contains(PluginHostErrorCodes.InvalidResponse, exception.Message);
    }

    private static ResolvedPlugin CreatePlugin(string? projectRoot = null)
    {
        string executablePath = projectRoot is null
            ? "/site/plugins/echo/bin/osx-arm64/bukit-plugin-echo"
            : Path.Combine(projectRoot, "plugins", "echo", "bin", "osx-arm64", "bukit-plugin-echo");
        string workingDirectory = projectRoot is null
            ? "/site/plugins/echo"
            : Path.Combine(projectRoot, "plugins", "echo");
        return new(
            Id: "echo",
            Version: "0.1.0",
            Platform: "osx-arm64",
            ExecutablePath: executablePath,
            WorkingDirectory: workingDirectory,
            Host: new PluginHostInfo("Bukit", "1.0.0", "osx-arm64"),
            ProjectRoot: projectRoot);
    }

    private static PluginInvokeRequest CreateInvokeRequest(
        string rootDir = "/site",
        PluginPermissionSet? permissions = null)
        => new(
            Type: "placeholder",
            Protocol: "placeholder",
            RequestId: "placeholder",
            Host: new PluginHostInfo("placeholder", "0", "placeholder"),
            Command: new PluginInvokeCommand("echo", Arguments: ["hello"]),
            Context: new PluginInvokeContext(rootDir, rootDir),
            Permissions: permissions ?? new PluginPermissionSet());

    private sealed class StubPluginProcessInvoker : IPluginProcessInvoker
    {
        private readonly string _stdout;
        private readonly int _exitCode;
        private readonly bool _timedOut;
        private readonly bool _outputLimitExceeded;

        public StubPluginProcessInvoker(
            string stdout,
            int exitCode = 0,
            bool timedOut = false,
            bool outputLimitExceeded = false)
        {
            _stdout = stdout;
            _exitCode = exitCode;
            _timedOut = timedOut;
            _outputLimitExceeded = outputLimitExceeded;
        }

        public PluginProcessRequest? Request { get; private set; }

        public Task<PluginProcessResult> InvokeAsync(PluginProcessRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new PluginProcessResult(
                ExitCode: _exitCode,
                StdoutJson: _stdout,
                Stderr: "stderr",
                TimedOut: _timedOut,
                OutputLimitExceeded: _outputLimitExceeded));
        }
    }

    private sealed class FixedRequestIdFactory : IPluginRequestIdFactory
    {
        private readonly string _requestId;

        public FixedRequestIdFactory(string requestId)
        {
            _requestId = requestId;
        }

        public string Create() => _requestId;
    }
}
