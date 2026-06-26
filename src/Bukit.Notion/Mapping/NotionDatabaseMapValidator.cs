using Bukit.Notion;
using Bukit.Notion.Security;

namespace Bukit.Notion.Mapping;

public static class NotionDatabaseMapValidator
{
    private static readonly HashSet<string> SupportedPropertyTypes = new(StringComparer.Ordinal)
    {
        "title",
        "rich_text",
        "checkbox",
        "number",
        "select",
        "multi_select",
        "url",
        "email",
        "phone_number",
        "date"
    };

    public static NotionDatabaseMapValidationResult Validate(string projectRoot, string databaseMapPath)
    {
        string resolvedPath = NotionPathGuard.ResolvePath(projectRoot, databaseMapPath);
        if (!File.Exists(resolvedPath))
        {
            return NotionDatabaseMapValidationResult.Failed(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapNotFound",
                NotionDiagnosticSeverity.Error,
                "Database map file was not found.",
                resolvedPath));
        }

        string finalPath = NotionPathGuard.ResolveFinalFilePath(resolvedPath);
        if (!NotionPathGuard.IsWithinRoot(projectRoot, resolvedPath)
            || !NotionPathGuard.IsWithinRoot(projectRoot, finalPath))
        {
            return NotionDatabaseMapValidationResult.Failed(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapOutsideProject",
                NotionDiagnosticSeverity.Error,
                "Database map file must be inside the project root.",
                resolvedPath));
        }

        NotionDatabaseMap? databaseMap = NotionDatabaseMapLoader.Load(resolvedPath, out IReadOnlyList<NotionDatabaseMapDiagnostic> loadDiagnostics);
        if (databaseMap is null)
        {
            return new NotionDatabaseMapValidationResult(false, 2, Diagnostics: loadDiagnostics, Artifacts: []);
        }

        var diagnostics = new List<NotionDatabaseMapDiagnostic>(loadDiagnostics);
        foreach (NotionDatabaseMapEntry entry in databaseMap.Databases.Values)
        {
            ValidateEntry(databaseMap.Path, entry, diagnostics);
        }

        if (diagnostics.Any(static diagnostic => string.Equals(diagnostic.Severity, NotionDiagnosticSeverity.Error, StringComparison.Ordinal)))
        {
            return new NotionDatabaseMapValidationResult(false, 2, databaseMap, diagnostics, []);
        }

        return NotionDatabaseMapValidationResult.Succeeded(databaseMap);
    }

    private static void ValidateEntry(
        string databaseMapPath,
        NotionDatabaseMapEntry entry,
        List<NotionDatabaseMapDiagnostic> diagnostics)
    {
        string path = $"{databaseMapPath}#databases.{entry.Name}";
        if (string.IsNullOrWhiteSpace(entry.Seed) || Path.IsPathRooted(entry.Seed) || EscapesRelativeScope(entry.Seed))
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapMissingSeed",
                NotionDiagnosticSeverity.Error,
                "Database map entry must contain a relative seed file path.",
                path));
        }

        if (string.IsNullOrWhiteSpace(entry.Collection))
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapMissingCollection",
                NotionDiagnosticSeverity.Error,
                "Database map entry must contain collection.",
                path));
        }

        if (string.IsNullOrWhiteSpace(entry.UniqueField))
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapMissingUniqueField",
                NotionDiagnosticSeverity.Error,
                "Database map entry must contain uniqueField.",
                path));
        }

        if (string.IsNullOrWhiteSpace(entry.EffectiveDataSourceId))
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapMissingDataSource",
                NotionDiagnosticSeverity.Error,
                "Database map entry must contain dataSourceId or legacy databaseId.",
                path));
        }

        if (entry.Properties.Count == 0)
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapMissingProperties",
                NotionDiagnosticSeverity.Error,
                "Database map entry must contain at least one property mapping.",
                $"{path}.properties"));
            return;
        }

        if (!entry.Properties.Values.Any(static property => string.Equals(property.Type, "title", StringComparison.Ordinal)))
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapMissingTitleProperty",
                NotionDiagnosticSeverity.Error,
                "Database map entry must contain at least one title property mapping.",
                $"{path}.properties"));
        }

        if (!string.IsNullOrWhiteSpace(entry.UniqueField)
            && !entry.Properties.ContainsKey(entry.UniqueField))
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapUniqueFieldNotMapped",
                NotionDiagnosticSeverity.Error,
                "Database map uniqueField must resolve to a property mapping.",
                $"{path}.uniqueField"));
        }

        foreach (NotionPropertyMapping property in entry.Properties.Values)
        {
            ValidateProperty(path, property, diagnostics);
        }
    }

    private static void ValidateProperty(
        string entryPath,
        NotionPropertyMapping property,
        List<NotionDatabaseMapDiagnostic> diagnostics)
    {
        string path = $"{entryPath}.properties.{property.Name}";
        if (string.IsNullOrWhiteSpace(property.Source)
            || string.IsNullOrWhiteSpace(property.Type)
            || !SupportedPropertyTypes.Contains(property.Type))
        {
            diagnostics.Add(new NotionDatabaseMapDiagnostic(
                "notion.databaseMapInvalidProperty",
                NotionDiagnosticSeverity.Error,
                "Property mapping must contain non-empty source and a supported type.",
                path));
        }
    }

    private static bool EscapesRelativeScope(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(static segment => segment == "..");
    }
}
