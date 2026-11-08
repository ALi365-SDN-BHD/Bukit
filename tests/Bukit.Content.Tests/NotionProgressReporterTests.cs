using System.Collections.Concurrent;
using Bukit.Content.Notion;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionProgressReporterTests
{
    [Fact]
    public async Task Increment_LogsEveryIntervalAndOnceAtEnd()
    {
        var logger = new CaptureLogger();
        const int total = NotionProgressReporter.Interval * 2 + 50;
        var reporter = new NotionProgressReporter(logger, "summary_prefetch", total);

        await Parallel.ForEachAsync(Enumerable.Range(0, total), (_, _) =>
        {
            reporter.Increment();
            return ValueTask.CompletedTask;
        });
        reporter.Complete();

        var lines = logger.Infos.ToArray();
        Assert.Equal(3, lines.Length);
        Assert.Contains(lines, l => l.Contains($"done={NotionProgressReporter.Interval} total={total}", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains($"done={total} total={total}", StringComparison.Ordinal));
        Assert.All(lines, l => Assert.StartsWith("event=notion.progress phase=summary_prefetch ", l, StringComparison.Ordinal));
    }

    [Fact]
    public void Complete_WithNoItems_LogsNothing()
    {
        var logger = new CaptureLogger();
        new NotionProgressReporter(logger, "summary_prefetch", 0).Complete();
        Assert.Empty(logger.Infos);
    }

    private sealed class CaptureLogger : ILogger
    {
        public ConcurrentQueue<string> Infos { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) => Infos.Enqueue(message);
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
