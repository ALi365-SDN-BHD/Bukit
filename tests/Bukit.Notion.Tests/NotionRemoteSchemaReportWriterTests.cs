using Bukit.Notion.RemoteSchema;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionRemoteSchemaReportWriterTests : IDisposable
{
    private readonly string _projectRoot;

    public NotionRemoteSchemaReportWriterTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-schema-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public void WriteJsonAndMarkdown_IncludeStableSchemaTablesAndEscapedDiagnostics()
    {
        string jsonPath = Path.Combine(_projectRoot, "schema-report.json");
        var diagnostic = new NotionRemoteSchemaDiagnostic(
            "notion.remoteSchemaPropertyTypeMismatch",
            "error",
            "Expected rich_text | got url\nCheck map.",
            "pages.properties.Slug");
        var dataSource = new NotionRemoteSchemaDataSourceResult(
            "pages",
            "page",
            "ds-pages",
            "dataSourceId",
            false,
            "Title",
            "Slug",
            [new NotionRemoteSchemaPropertyResult("Slug", "rich_text", "url", "type-mismatch")],
            [diagnostic]);
        var report = new NotionRemoteSchemaReport(
            "bukit.notion.schema.validation.report.v1",
            false,
            "sites/demo/notion-seed/notion-database-map.yaml",
            [dataSource],
            [diagnostic]);

        NotionRemoteSchemaReportWriter.WriteJson(jsonPath, report);
        NotionRemoteSchemaReportWriter.WriteMarkdown(Path.ChangeExtension(jsonPath, ".md"), report);

        string json = File.ReadAllText(jsonPath);
        string markdown = File.ReadAllText(Path.ChangeExtension(jsonPath, ".md"));
        Assert.Contains("\"schema\": \"bukit.notion.schema.validation.report.v1\"", json, StringComparison.Ordinal);
        Assert.Contains("- Database map: sites/demo/notion-seed/notion-database-map.yaml", markdown, StringComparison.Ordinal);
        Assert.Contains("| pages | page | ds-pages | dataSourceId | false | Title | Slug |", markdown, StringComparison.Ordinal);
        Assert.Contains("## Properties: pages", markdown, StringComparison.Ordinal);
        Assert.Contains("| Slug | rich_text | url | type-mismatch |", markdown, StringComparison.Ordinal);
        Assert.Contains("## Diagnostics", markdown, StringComparison.Ordinal);
        Assert.Contains("Expected rich_text \\| got url Check map.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", markdown, StringComparison.Ordinal);
    }
}
