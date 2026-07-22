using System.Text.Json;
using Bukit.Notion.Write;

namespace Bukit.Importing;

internal static class NotionSchemaValidator
{
    private static readonly (string Name, string ExpectedType)[] RequiredFields =
    [
        ("Title", "title"),
        ("Slug", "rich_text"),
        ("Type", "select"),
        ("Summary", "rich_text"),
        ("Language", "select"),
        ("Published", "checkbox"),
        ("SeoTitle", "rich_text"),
        ("SeoDescription", "rich_text")
    ];

    internal static async Task<SchemaValidationReport> ValidateAsync(
        NotionWriteClient client,
        string databaseId,
        string? reportPath,
        IReadOnlyList<(string Name, string ExpectedType)>? additionalRequiredFields = null,
        CancellationToken ct = default)
    {
        var response = await client.InspectDatabaseSchemaAsync(databaseId, ct);
        if (!response.IsSuccess)
        {
            var report = new SchemaValidationReport
            {
                Success = false,
                DatabaseId = databaseId,
                Errors = [response.ErrorMessage ?? response.ReasonPhrase ?? "Notion request failed."],
                FieldResults = []
            };
            WriteReport(reportPath, databaseId, report);
            return report;
        }

        var properties = response.Payload!.Value.GetProperty("properties");
        var fieldResults = new List<SchemaFieldResult>();
        var errors = new List<string>();

        var requiredFields = additionalRequiredFields is { Count: > 0 }
            ? RequiredFields.Concat(additionalRequiredFields).ToArray()
            : RequiredFields;

        foreach (var (name, expectedType) in requiredFields)
        {
            if (properties.TryGetProperty(name, out var prop))
            {
                var actualType = prop.GetProperty("type").GetString() ?? "";
                if (actualType.Equals(expectedType, StringComparison.OrdinalIgnoreCase))
                {
                    fieldResults.Add(new SchemaFieldResult(name, expectedType, "OK", null));
                }
                else
                {
                    var msg = $"Field '{name}' expected type '{expectedType}' but found '{actualType}'";
                    errors.Add(msg);
                    fieldResults.Add(new SchemaFieldResult(name, expectedType, "Type Mismatch", msg));
                }
            }
            else
            {
                var msg = $"Field '{name}' (type: {expectedType}) is missing";
                errors.Add(msg);
                fieldResults.Add(new SchemaFieldResult(name, expectedType, "Missing", msg));
            }
        }

        var report2 = new SchemaValidationReport
        {
            Success = errors.Count == 0,
            DatabaseId = databaseId,
            Errors = errors,
            FieldResults = fieldResults
        };

        WriteReport(reportPath, databaseId, report2);
        return report2;
    }

    private static void WriteReport(string? reportPath, string databaseId, SchemaValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(reportPath)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteBoolean("success", report.Success);
        writer.WriteString("databaseId", databaseId);
        writer.WriteStartArray("fields");
        foreach (var f in report.FieldResults)
        {
            writer.WriteStartObject();
            writer.WriteString("name", f.Name);
            writer.WriteString("expectedType", f.ExpectedType);
            writer.WriteString("result", f.Result);
            if (f.Message != null)
                writer.WriteString("message", f.Message);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        if (report.Errors.Count > 0)
        {
            writer.WriteStartArray("errors");
            foreach (var e in report.Errors)
                writer.WriteStringValue(e);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
        writer.Flush();

        File.WriteAllText(reportPath, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }
}

internal sealed class SchemaValidationReport
{
    public bool Success { get; init; }
    public string DatabaseId { get; init; } = "";
    public List<string> Errors { get; init; } = [];
    public List<SchemaFieldResult> FieldResults { get; init; } = [];
}

internal sealed record SchemaFieldResult(
    string Name,
    string ExpectedType,
    string Result,
    string? Message);
