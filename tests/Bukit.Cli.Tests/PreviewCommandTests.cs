using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Commands;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class PreviewCommandTests
{
    [Fact]
    public void ApplyPreviewAnalyticsPolicy_WhenRemovalEnabled_RemovesManagedHeadAndBodyBlocks()
    {
        var html = """
            <html><head>
              <meta charset="utf-8">
              <!-- bukit:analytics:google-analytics:G-ABC123:head:start -->
              <script async src="https://www.googletagmanager.com/gtag/js?id=G-ABC123"></script>
              <!-- bukit:analytics:google-analytics:G-ABC123:head:end -->
            </head><body>
              <!-- bukit:analytics:google-tag-manager:GTM-ABC123:body:start -->
              <noscript>managed body</noscript>
              <!-- bukit:analytics:google-tag-manager:GTM-ABC123:body:end -->
              ok
            </body></html>
            """;

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: true);

        Assert.DoesNotContain("googletagmanager.com/gtag/js", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("managed body", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("bukit:analytics", filtered, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", filtered, StringComparison.Ordinal);
        Assert.Contains("ok", filtered, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_PreservesUnmarkedProviderScripts()
    {
        var html = """
            <script async src="https://www.googletagmanager.com/gtag/js?id=G-ABC123"></script>
            <script>gtag('config', 'G-ABC123');</script>
            <script src="https://www.googletagmanager.com/gtm.js?id=GTM-ABC123"></script>
            <script defer data-domain="example.com" src="https://plausible.io/js/script.js"></script>
            <script async data-website-id="site-id" src="https://cloud.umami.is/script.js"></script>
            """;

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: true);

        Assert.Equal(html, filtered);
    }

    [Theory]
    [InlineData("<script>const marker = \"<!-- bukit:analytics:google-analytics:G-ABC123:head:start -->user<!-- bukit:analytics:google-analytics:G-ABC123:head:end -->\";</script>")]
    [InlineData("<style>/* <!-- bukit:analytics:google-analytics:G-ABC123:head:start -->user<!-- bukit:analytics:google-analytics:G-ABC123:head:end --> */</style>")]
    [InlineData("<div data-marker=\"<!-- bukit:analytics:google-analytics:G-ABC123:head:start -->user<!-- bukit:analytics:google-analytics:G-ABC123:head:end -->\"></div>")]
    public void ApplyPreviewAnalyticsPolicy_PreservesMarkerTextOutsideHtmlComments(string html)
    {
        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: true);

        Assert.Equal(html, filtered);
    }

    [Theory]
    [InlineData("google-analytics:G-ABC123:extra")]
    [InlineData("google-analytics:G@ABC123")]
    [InlineData("google-analytics:G-ABC123/path")]
    public void ApplyPreviewAnalyticsPolicy_PreservesMarkersWithInvalidProviderKeys(string providerKey)
    {
        var html = $"<!-- bukit:analytics:{providerKey}:head:start -->user<!-- bukit:analytics:{providerKey}:head:end -->";

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: true);

        Assert.Equal(html, filtered);
    }

    [Theory]
    [InlineData("<!-- bukit:analytics:google-analytics:G-ABC123:head:start --><script>managed</script>")]
    [InlineData("<script>managed</script><!-- bukit:analytics:google-analytics:G-ABC123:head:end -->")]
    [InlineData("<!-- bukit:analytics:google-analytics:G-ABC123:head:start --><script>managed</script><!-- bukit:analytics:google-analytics:G-ABC123:body:end -->")]
    [InlineData("<!--bukit:analytics:google-analytics:G-ABC123:head:start--><script>managed</script><!--bukit:analytics:google-analytics:G-ABC123:head:end-->")]
    public void ApplyPreviewAnalyticsPolicy_PreservesMalformedOrUnpairedManagedBlocks(string html)
    {
        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: true);

        Assert.Equal(html, filtered);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_RemovesValidManagedBlockAfterUnpairedStart()
    {
        const string html = """
            <html><head>
            <!-- bukit:analytics:google-analytics:G-ORPHAN:head:start -->
            <!-- bukit:analytics:google-analytics:G-ACTIVE:head:start -->
            <script>managed()</script>
            <!-- bukit:analytics:google-analytics:G-ACTIVE:head:end -->
            </head><body></body></html>
            """;

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: true);

        Assert.Contains("bukit:analytics:google-analytics:G-ORPHAN:head:start", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("bukit:analytics:google-analytics:G-ACTIVE", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("managed()", filtered, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPreviewAnalyticsPolicy_WhenRemovalDisabled_LeavesManagedBlockUnchanged()
    {
        var html = "<!-- bukit:analytics:plausible:example.com:head:start --><script>managed</script><!-- bukit:analytics:plausible:example.com:head:end -->";

        var filtered = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: false);

        Assert.Equal(html, filtered);
    }

    [Fact]
    public async Task RunAsync_MissingDir_ReturnsExitCode2()
    {
        var command = BuildCommand("--dir", Path.Combine(Path.GetTempPath(), "bukit-preview-nonexistent"));
        var exitCode = await RunWithTimeoutAsync(command, TimeSpan.FromSeconds(5));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_InvalidPort_ReturnsExitCode2()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-preview");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<html></html>");

        try
        {
            var command = BuildCommand("--dir", dir, "--port", "99999");
            var exitCode = await RunWithTimeoutAsync(command, TimeSpan.FromSeconds(5));
            Assert.Equal(2, exitCode);
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_NegativePort_ReturnsExitCode2()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-preview-neg");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<html></html>");

        try
        {
            var command = BuildCommand("--dir", dir, "--port", "-1");
            var exitCode = await RunWithTimeoutAsync(command, TimeSpan.FromSeconds(5));
            Assert.Equal(2, exitCode);
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_StrictPortUnavailable_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-preview-strict");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<html></html>");

        try
        {
            using var occupiedPort = new TcpListener(IPAddress.Loopback, 0);
            occupiedPort.Start();
            var port = ((IPEndPoint)occupiedPort.LocalEndpoint).Port;

            var command = BuildCommand("--dir", dir, "--host", IPAddress.Loopback.ToString(), "--port", port.ToString(), "--strict-port", "true");
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => PreviewCommand.RunAsync(command));
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_NonLoopbackHostWithoutAllowLan_ReturnsExitCode2()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-preview-lan");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<html></html>");

        try
        {
            // 192.0.2.0/24 is TEST-NET-1 (RFC 5737) and never bound; the refusal must
            // happen before any listener is created.
            var command = BuildCommand("--dir", dir, "--host", "192.0.2.10", "--port", "4173");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var exitCode = await PreviewCommand.RunAsync(command, cts.Token);
            Assert.Equal(2, exitCode);
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public void Preview_RefusesLanExposureUnlessAllowLan()
    {
        Assert.True(PreviewCommand.ShouldRefuseNonLoopbackHost("0.0.0.0", allowLan: false));
        Assert.True(PreviewCommand.ShouldRefuseNonLoopbackHost("192.168.1.10", allowLan: false));
        Assert.False(PreviewCommand.ShouldRefuseNonLoopbackHost("0.0.0.0", allowLan: true));
        Assert.False(PreviewCommand.ShouldRefuseNonLoopbackHost("localhost", allowLan: false));
        Assert.False(PreviewCommand.ShouldRefuseNonLoopbackHost("127.0.0.1", allowLan: false));
    }

    private static CliBoundCommand BuildCommand(params string[] keyValues)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < keyValues.Length; i += 2)
        {
            dict[keyValues[i]] = keyValues[i + 1];
        }
        return new CliBoundCommand(dict, Array.Empty<string>());
    }

    private static async Task<int> RunWithTimeoutAsync(CliBoundCommand command, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var task = PreviewCommand.RunAsync(command, cts.Token);
            var completed = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
            if (completed == task)
            {
                return await task;
            }

            throw new TimeoutException($"PreviewCommand.RunAsync exceeded timeout: {timeout}.");
        }
        catch (Exception)
        {
            return 2;
        }
    }
}
