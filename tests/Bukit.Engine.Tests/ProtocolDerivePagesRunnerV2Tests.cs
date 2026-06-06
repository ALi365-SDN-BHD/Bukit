using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.Protocol;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ProtocolDerivePagesRunnerV2Tests
{
    [Fact]
    public async Task RunAsync_WhenRoutedDocumentsExist_SendsV2RoutedPagesWithoutMeta()
    {
        var invoker = new CapturingProtocolInvoker();
        var runner = new ProtocolDerivePagesRunner(invoker);
        var context = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "site", Title = "Site", Language = "en" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/repo",
            OutputDir = "/repo/dist",
            BaseUrl = "/",
            LayoutsDir = "/repo/layouts",
            Routed = Array.Empty<(ContentItem Item, RouteInfo Route)>(),
            RoutedDocuments = new[] { (Document(), new RouteInfo("/hello/", "hello/index.html", "pages/post.html")) },
            Logger = new TestLogger()
        };

        await runner.RunAsync(
            context,
            new ExternalPluginConfig { Runtime = "node", Entry = "plugin.js" },
            "test-plugin",
            "1.0.0",
            CancellationToken.None);

        using var request = JsonDocument.Parse(invoker.RequestJson);
        var routedPage = request.RootElement.GetProperty("derivePages").GetProperty("routedPages")[0];
        Assert.Equal("2", request.RootElement.GetProperty("schemaVersion").GetString());
        Assert.True(routedPage.TryGetProperty("content", out var content));
        Assert.Equal("hello", content.GetProperty("id").GetString());
        Assert.True(routedPage.TryGetProperty("route", out _));
        Assert.True(routedPage.TryGetProperty("publish", out _));
        Assert.False(routedPage.TryGetProperty("meta", out _));
    }

    private static ContentDocument Document()
    {
        var record = new ContentRecord(
            new ContentIdentity("hello", "hello", "hello", "post", "published"),
            new ContentPresentation("Hello", "Summary", "<p>Hello</p>", "en", []),
            new ContentClassification("post", "post", [], ["bukit"]),
            new ContentOwnership("Ali", null, null, null),
            new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            new ProvenanceRecord("markdown", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [],
            [],
            []);

        return new ContentDocument(
            record,
            new ContentBodyRef("<p>Hello</p>", null, "# Hello", "Hello"),
            new ContentRoutePolicy(null, null, null, null, "post"),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());
    }

    private sealed class CapturingProtocolInvoker : IProtocolPluginInvoker
    {
        public string RequestJson { get; private set; } = string.Empty;

        public Task<ProtocolPluginInvocationResult> InvokeAsync(
            ExternalPluginConfig plugin,
            string requestJson,
            string? arguments,
            CancellationToken cancellationToken)
        {
            RequestJson = requestJson;
            return Task.FromResult(new ProtocolPluginInvocationResult(
                0,
                """{"ok":true,"derivedPages":[]}""",
                string.Empty,
                false,
                1));
        }
    }

    private sealed class TestLogger : ILogger
    {
        public void Info(string message) { }
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
