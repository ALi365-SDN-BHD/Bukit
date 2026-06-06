using System.Net;
using Bukit.Cli.Commands.Dev;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests.Dev;

public class DevRequestHandlerTests
{
    [Fact]
    public void InjectLivereload_HasHeadTag_InjectsBeforeClosingHead()
    {
        var html = "<html><head><title>Test</title></head><body></body></html>";
        var result = DevRequestHandler.InjectLivereload(html, 35729);

        Assert.Contains("35729", result);
        var headCloseIdx = result.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        Assert.True(headCloseIdx > 0);
        Assert.Contains("<script>", result[..headCloseIdx]);
    }

    [Fact]
    public void InjectLivereload_NoHeadTag_AppendsToEnd()
    {
        var html = "<html><body>no head</body></html>";
        var result = DevRequestHandler.InjectLivereload(html, 12345);

        Assert.Contains("12345", result);
        Assert.Contains("<script>", result);
        Assert.EndsWith("</script>", result);
    }

    [Theory]
    [InlineData(".html", "text/html; charset=utf-8")]
    [InlineData(".css", "text/css; charset=utf-8")]
    [InlineData(".js", "application/javascript; charset=utf-8")]
    [InlineData(".png", "image/png")]
    [InlineData(".unknown", "application/octet-stream")]
    public async Task InjectLivereload_MimeMapping_Correct(string ext, string expectedMime)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "bukit_test_mime_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var filePath = Path.Combine(tmpDir, "test" + ext);
            File.WriteAllText(filePath, ext == ".html" ? "<html></html>" : "data");

            var logger = new TestLogger();
            var handler = new DevRequestHandler(tmpDir, 12345, false, logger);

            using var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:18921/");
            listener.Start();

            _ = Task.Run(async () =>
            {
                var ctx = await listener.GetContextAsync();
                await handler.HandleAsync(ctx, CancellationToken.None);
            });

            using var client = new HttpClient();
            var response = await client.GetAsync("http://localhost:18921/test" + ext);

            Assert.True(response.IsSuccessStatusCode);
            Assert.StartsWith(expectedMime, response.Content.Headers.ContentType?.ToString());
        }
        finally
        {
            TestCleanup.DeleteDirectory(tmpDir, recursive: true);
        }
    }

    [Fact]
    public async Task HandleAsync_NotFound_Returns404()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "bukit_test_404_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);
        try
        {
            var logger = new TestLogger();
            var handler = new DevRequestHandler(tmpDir, 12345, false, logger);

            using var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:18923/");
            listener.Start();

            _ = Task.Run(async () =>
            {
                var ctx = await listener.GetContextAsync();
                await handler.HandleAsync(ctx, CancellationToken.None);
            });

            using var client = new HttpClient();
            var response = await client.GetAsync("http://localhost:18923/nonexistent.html");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            TestCleanup.DeleteDirectory(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void InjectLivereload_PortNumber_PresentInScript()
    {
        var html = "<html><head></head><body></body></html>";
        var result = DevRequestHandler.InjectLivereload(html, 35729);

        Assert.Contains("35729", result);
    }
}
