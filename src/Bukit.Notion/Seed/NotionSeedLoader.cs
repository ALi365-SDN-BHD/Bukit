using System.Text.Json;
using Bukit.Notion;

namespace Bukit.Notion.Seed;

public static class NotionSeedLoader
{
    public static readonly IReadOnlyList<string> SupportedSeedFiles =
    [
        "pages.json",
        "navigation.json",
        "posts.json",
        "companies.json",
        "services.json"
    ];

    public static readonly IReadOnlyList<string> ImportGeneratedSeedFiles =
    [
        "pages.json",
        "navigation.json",
        "sections.json",
        "posts.json",
        "companies.json",
        "services.json",
        "faqs.json",
        "media.json",
        "components.json"
    ];

    public static NotionSeedSet Load(string seedDirectory, out IReadOnlyList<NotionSeedDiagnostic> diagnostics)
    {
        var collections = new List<NotionSeedCollection>();
        var errors = new List<NotionSeedDiagnostic>();

        foreach (string fileName in SupportedSeedFiles)
        {
            string path = Path.Combine(seedDirectory, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            NotionSeedCollection? collection = LoadCollection(path, Path.GetFileNameWithoutExtension(fileName), errors);
            if (collection is not null)
            {
                collections.Add(collection);
            }
        }

        diagnostics = errors;
        return new NotionSeedSet(seedDirectory, collections);
    }

    private static NotionSeedCollection? LoadCollection(
        string path,
        string collectionName,
        List<NotionSeedDiagnostic> diagnostics)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(new NotionSeedDiagnostic(
                    "notion.seedInvalidJson",
                    NotionDiagnosticSeverity.Error,
                    "Seed file must contain a JSON array.",
                    path));
                return null;
            }

            var records = new List<NotionSeedRecord>();
            int index = 0;
            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(new NotionSeedDiagnostic(
                        "notion.seedInvalidRecord",
                        NotionDiagnosticSeverity.Error,
                        "Seed record must be a JSON object.",
                        BuildRecordPath(path, index)));
                    index++;
                    continue;
                }

                records.Add(new NotionSeedRecord(collectionName, index, CloneFields(item)));
                index++;
            }

            return new NotionSeedCollection(collectionName, path, records);
        }
        catch (JsonException ex)
        {
            diagnostics.Add(new NotionSeedDiagnostic(
                "notion.seedInvalidJson",
                NotionDiagnosticSeverity.Error,
                $"Seed file contains invalid JSON: {ex.Message}",
                path));
            return null;
        }
        catch (IOException ex)
        {
            diagnostics.Add(new NotionSeedDiagnostic(
                "notion.seedInvalidJson",
                NotionDiagnosticSeverity.Error,
                $"Seed file could not be read: {ex.Message}",
                path));
            return null;
        }
    }

    private static Dictionary<string, JsonElement> CloneFields(JsonElement item)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in item.EnumerateObject())
        {
            fields[property.Name] = property.Value.Clone();
        }

        return fields;
    }

    private static string BuildRecordPath(string path, int index)
        => $"{path}#{index}";
}
