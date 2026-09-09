using System.Net;
using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Cli.Commands.Dev;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DefaultPageSecurityTests
{
    [Theory]
    [InlineData(false, "", false)]
    [InlineData(false, "nested/", false)]
    [InlineData(false, "nested", false)]
    [InlineData(false, "", true)]
    [InlineData(false, "nested/", true)]
    [InlineData(false, "nested", true)]
    [InlineData(true, "", false)]
    [InlineData(true, "nested/", false)]
    [InlineData(true, "nested", false)]
    [InlineData(true, "", true)]
    [InlineData(true, "nested/", true)]
    [InlineData(true, "nested", true)]
    public async Task DefaultIndexSymlinkCannotExposeExternalOrInternalFile(bool preview, string path, bool internalTarget)
    {
        var temp = Path.Combine(Path.GetTempPath(), "bukit-default-page-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(temp, "output");
        Directory.CreateDirectory(Path.Combine(root, ".bukit"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        var target = Path.Combine(internalTarget ? Path.Combine(root, ".bukit") : temp, "private.html");
        File.WriteAllText(target, "private-body");
        try
        {
            File.CreateSymbolicLink(Path.Combine(root, path.Length == 0 ? "" : "nested", "index.html"), target);
            using var host = DevServerHost.Start("localhost", 0, new ConsoleLogger(LogLevel.Error));
            using var cancellation = new CancellationTokenSource();
            var handler = new DevRequestHandler(root, false, new ConsoleLogger(LogLevel.Error));
            var method = typeof(PreviewCommand).GetMethod("HandleRequestAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
            var loop = host.RunAcceptLoopAsync(context => preview
                ? (Task)method.Invoke(null, [root, context, false, cancellation.Token])!
                : handler.HandleAsync(context, cancellation.Token), cancellation.Token);
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync(host.Prefix + path);
                Assert.Equal(internalTarget ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden, response.StatusCode);
                Assert.DoesNotContain("private-body", await response.Content.ReadAsStringAsync());
            }
            finally
            {
                cancellation.Cancel();
                await loop.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }
}
