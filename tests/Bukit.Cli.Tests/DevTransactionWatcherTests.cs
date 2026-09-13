using Bukit.Cli.Commands.Dev;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DevTransactionWatcherTests
{
    [Theory]
    [InlineData("stage")]
    [InlineData("backup")]
    public void TransactionChildrenAreIgnoredInsideAndOutsideProject(string suffix)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-watcher-project");
        using var watcher = new DevFileWatcher([], root, new ConsoleLogger(LogLevel.Error), static (_, _) => Task.CompletedTask);
        foreach (var parent in new[] { root, Path.Combine(Path.GetTempPath(), "external-theme") })
        {
            Assert.True(watcher.ShouldIgnore(Path.Combine(parent, ".bukit-txn-abc-" + suffix, "asset.css"), "asset.css"));
            Assert.False(watcher.ShouldIgnore(Path.Combine(parent, "assets", "asset.css"), "asset.css"));
        }
    }
}
