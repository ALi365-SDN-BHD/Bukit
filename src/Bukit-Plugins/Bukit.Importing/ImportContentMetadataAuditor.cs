namespace Bukit.Importing;

internal static class ImportContentMetadataAuditor
{
    private static readonly (string Field, string Code, string DownstreamCode)[] RequiredFields =
    [
        ("author", "import.content.author_missing", "publish.author_missing"),
        ("source", "import.content.source_missing", "publish.source_missing"),
        ("original_url", "import.content.original_url_missing", "publish.source_missing"),
        ("cover_alt", "import.content.cover_alt_missing", "publish.image_alt_missing"),
        ("entities", "import.content.entities_missing", "publish.entity_missing")
    ];

    internal static void AddDiagnostics(
        HtmlDemoImportOptions options,
        ExtractedContent content,
        List<ImportDiagnostic> diagnostics)
    {
        var recordCount = CountPublishableRecords(content);
        if (recordCount == 0)
        {
            return;
        }

        var contentPath = Path.Combine(HtmlDemoImporter.GetSiteDir(options), "content");
        foreach (var (field, code, downstreamCode) in RequiredFields)
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Warning,
                code,
                $"Imported content records do not include '{field}' metadata; provide a source mapping to avoid {downstreamCode} audit warnings. Affected records: {recordCount}.",
                contentPath));
        }
    }

    private static int CountPublishableRecords(ExtractedContent content)
        => content.Pages.Count + content.Posts.Count + content.Companies.Count + content.Services.Count;
}
