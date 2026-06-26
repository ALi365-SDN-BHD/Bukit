using System.Text.Json;
using Bukit.Notion;
using Bukit.Notion.Security;

namespace Bukit.Notion.Seed;

public static class NotionSeedValidator
{
    public static NotionSeedValidationResult Validate(string projectRoot, string seedDirectory)
    {
        string resolvedSeedDirectory = NotionPathGuard.ResolvePath(projectRoot, seedDirectory);
        if (!Directory.Exists(resolvedSeedDirectory))
        {
            return NotionSeedValidationResult.Failed(new NotionSeedDiagnostic(
                "notion.seedDirNotFound",
                NotionDiagnosticSeverity.Error,
                "Seed directory was not found.",
                resolvedSeedDirectory));
        }

        string finalSeedDirectory = NotionPathGuard.ResolveFinalDirectoryPath(resolvedSeedDirectory);
        if (!NotionPathGuard.IsWithinRoot(projectRoot, resolvedSeedDirectory)
            || !NotionPathGuard.IsWithinRoot(projectRoot, finalSeedDirectory))
        {
            return NotionSeedValidationResult.Failed(new NotionSeedDiagnostic(
                "notion.seedDirOutsideProject",
                NotionDiagnosticSeverity.Error,
                "Seed directory must be inside the project root.",
                resolvedSeedDirectory));
        }

        if (!NotionSeedLoader.SupportedSeedFiles.Any(file => File.Exists(Path.Combine(resolvedSeedDirectory, file))))
        {
            return NotionSeedValidationResult.Failed(new NotionSeedDiagnostic(
                "notion.seedNoFiles",
                NotionDiagnosticSeverity.Error,
                "Seed directory does not contain any supported Notion seed JSON files.",
                resolvedSeedDirectory));
        }

        NotionSeedSet seedSet = NotionSeedLoader.Load(resolvedSeedDirectory, out IReadOnlyList<NotionSeedDiagnostic> loadDiagnostics);
        var diagnostics = new List<NotionSeedDiagnostic>(loadDiagnostics);
        foreach (NotionSeedCollection collection in seedSet.Collections)
        {
            foreach (NotionSeedRecord record in collection.Records)
            {
                ValidateRecord(collection.Path, record, diagnostics);
            }
        }

        if (diagnostics.Any(static diagnostic => string.Equals(diagnostic.Severity, NotionDiagnosticSeverity.Error, StringComparison.Ordinal)))
        {
            return new NotionSeedValidationResult(false, 2, seedSet, diagnostics, []);
        }

        return NotionSeedValidationResult.Succeeded(seedSet);
    }

    private static void ValidateRecord(
        string collectionPath,
        NotionSeedRecord record,
        List<NotionSeedDiagnostic> diagnostics)
    {
        string recordPath = $"{collectionPath}#{record.Index}";
        if (!CanGenerateSlug(record))
        {
            diagnostics.Add(new NotionSeedDiagnostic(
                "notion.seedMissingTitle",
                NotionDiagnosticSeverity.Error,
                "Seed record must contain a non-empty title or name.",
                recordPath));
        }

        if (record.Fields.TryGetValue("published", out JsonElement published)
            && published.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            diagnostics.Add(new NotionSeedDiagnostic(
                "notion.seedInvalidPublished",
                NotionDiagnosticSeverity.Error,
                "Seed record field 'published' must be a boolean when present.",
                recordPath));
        }

        if (record.Fields.TryGetValue("tags", out JsonElement tags)
            && tags.ValueKind is not (JsonValueKind.Array or JsonValueKind.String))
        {
            diagnostics.Add(new NotionSeedDiagnostic(
                "notion.seedInvalidTags",
                NotionDiagnosticSeverity.Error,
                "Seed record field 'tags' must be an array or string when present.",
                recordPath));
        }
    }

    private static bool CanGenerateSlug(NotionSeedRecord record)
        => HasNonEmptyString(record, "title")
           || HasNonEmptyString(record, "name");

    private static bool HasNonEmptyString(NotionSeedRecord record, string key)
        => record.Fields.TryGetValue(key, out JsonElement value)
           && value.ValueKind == JsonValueKind.String
           && !string.IsNullOrWhiteSpace(value.GetString());
}
