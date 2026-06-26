using Bukit.Notion.Push;
using Bukit.Notion.Report;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionPushReportWriterTests : IDisposable
{
    private readonly string _root;

    public NotionPushReportWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "bukit-notion-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void WriteMarkdown_WritesSummaryWithoutSecret()
    {
        string path = Path.Combine(_root, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.md");
        var report = new NotionPushReport(
            DryRun: false,
            Mode: "replace",
            PlannedCreate: 0,
            PlannedUpdate: 0,
            PlannedReplace: 1,
            Records:
            [
                new NotionPushRecordResult(
                    Collection: "page",
                    SeedFile: "pages.json",
                    Operation: "replace",
                    Title: "Home",
                    UniqueField: "Slug",
                    UniqueValue: "home",
                    DataSourceId: "ds-pages")
            ]);

        NotionPushReportWriter.WriteMarkdown(path, report);

        string markdown = File.ReadAllText(path);
        Assert.Contains("# Notion Push Report", markdown, StringComparison.Ordinal);
        Assert.Contains("- Mode: replace", markdown, StringComparison.Ordinal);
        Assert.Contains("- Planned replace: 1", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", markdown, StringComparison.OrdinalIgnoreCase);
    }
}
