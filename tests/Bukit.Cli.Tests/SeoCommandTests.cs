using Bukit.Cli;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-seo-command-tests-" + Guid.NewGuid().ToString("N"));

    public SeoCommandTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsOneWhenReportHasErrors()
    {
        File.WriteAllText(Path.Combine(_root, "seo-report.json"), """
            {
              "summary": { "errorCount": 1, "warningCount": 0 },
              "issues": [
                { "severity": "error", "code": "seo.head_missing", "route": "/", "message": "missing head" }
              ]
            }
            """);

        var exitCode = await SeoCommand.RunAsync(new ArgReader(new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditStrictReturnsOneWhenReportHasWarnings()
    {
        File.WriteAllText(Path.Combine(_root, "seo-report.json"), """
            {
              "summary": { "errorCount": 0, "warningCount": 1 },
              "issues": [
                { "severity": "warning", "code": "seo.title_too_long", "route": "/", "message": "title too long" }
              ]
            }
            """);

        var exitCode = await SeoCommand.RunAsync(new ArgReader(new[] { "seo", "audit", "--dir", _root, "--strict" }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_AuditReturnsZeroWhenReportHasNoBlockingIssues()
    {
        File.WriteAllText(Path.Combine(_root, "seo-report.json"), """
            {
              "summary": { "errorCount": 0, "warningCount": 1 },
              "issues": [
                { "severity": "warning", "code": "seo.title_too_long", "route": "/", "message": "title too long" }
              ]
            }
            """);

        var exitCode = await SeoCommand.RunAsync(new ArgReader(new[] { "seo", "audit", "--dir", _root }));

        Assert.Equal(0, exitCode);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
