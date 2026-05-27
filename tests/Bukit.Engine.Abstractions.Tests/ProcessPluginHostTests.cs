using Bukit.Engine.Abstractions.Plugins.Protocol;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public class ProcessPluginHostTests
{
    private sealed class TestablePluginHost : ProcessPluginHost
    {
        public StringWriter StdOut { get; } = new();
        public bool AfterBuildCalled { get; private set; }

        protected override string PluginName => "test";
        protected override string PluginVersion => "1.0";
        protected override IReadOnlyList<string> SupportedHooks => new[] { "after-build" };

        public async Task RunAndCaptureOutput(string stdin)
        {
            var originalIn = Console.In;
            var originalOut = Console.Out;
            try
            {
                Console.SetIn(new StringReader(stdin));
                Console.SetOut(StdOut);
                await RunAsync();
            }
            finally
            {
                Console.SetIn(originalIn);
                Console.SetOut(originalOut);
            }
        }

        protected override Task AfterBuildAsync(AfterBuildRequestPayload payload, IReadOnlyDictionary<string, object>? pluginOptions, CancellationToken ct)
        {
            AfterBuildCalled = true;
            return Task.CompletedTask;
        }
    }

    private static string BaseRequest(string hook, string? extra = null)
    {
        var extraJson = extra is null ? "" : "," + extra;
        return $"{{\"hook\":\"{hook}\",\"plugin\":{{\"name\":\"test\",\"version\":\"1.0\"}},\"site\":{{\"baseUrl\":\"/\",\"language\":\"en\",\"title\":\"Test\"}}{extraJson}}}";
    }

    [Fact]
    public async Task Handshake_ReturnsOk()
    {
        var host = new TestablePluginHost();
        var stdin = BaseRequest("handshake");

        await host.RunAndCaptureOutput(stdin);

        var output = host.StdOut.ToString();
        Assert.Contains("\"ok\":true", output);
    }

    [Fact]
    public async Task AfterBuild_SucceedsAndCallsHook()
    {
        var host = new TestablePluginHost();
        var stdin = BaseRequest("after-build", "\"afterBuild\":{\"outputDir\":\"/tmp\"}");

        await host.RunAndCaptureOutput(stdin);

        var output = host.StdOut.ToString();
        Assert.Contains("\"ok\":true", output);
        Assert.True(host.AfterBuildCalled);
    }

    [Fact]
    public async Task EmptyStdin_ReturnsError()
    {
        var host = new TestablePluginHost();

        await host.RunAndCaptureOutput("");

        var output = host.StdOut.ToString();
        Assert.Contains("\"ok\":false", output);
        Assert.Contains("Empty stdin", output);
    }

    [Fact]
    public async Task InvalidJson_ReturnsError()
    {
        var host = new TestablePluginHost();

        await host.RunAndCaptureOutput("not valid json");

        var output = host.StdOut.ToString();
        Assert.Contains("\"ok\":false", output);
        Assert.Contains("Failed to parse request", output);
    }

    [Fact]
    public async Task UnsupportedHook_ReturnsError()
    {
        var host = new TestablePluginHost();
        var stdin = BaseRequest("unsupported-hook");

        await host.RunAndCaptureOutput(stdin);

        var output = host.StdOut.ToString();
        Assert.Contains("\"ok\":false", output);
        Assert.Contains("Unsupported hook", output);
    }
}
