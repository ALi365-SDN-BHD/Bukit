using System.Net.Http.Headers;
using System.Text.Json;
using Bukit.Shared.Notion;

namespace Bukit.Cli.Commands;

internal sealed record NotionPushOptions(
    string DatabaseId,
    string Token,
    string ReportPath,
    bool DryRun,
    string Mode = "create",
    string UniqueField = "Slug");

internal sealed record NotionPushItemResult(
    ImportSeedRecord Record,
    string Action,
    bool Success,
    string? NotionPageId,
    string? Error);

internal sealed record NotionPushResult(
    int Total,
    int Created,
    int Updated,
    int Skipped,
    int Failed,
    IReadOnlyList<NotionPushItemResult> Items);

internal static partial class NotionSeedPusher
{
    internal static async Task<NotionPushResult> PushAsync(
        HttpClient http,
        IReadOnlyList<ImportSeedRecord> records,
        NotionPushOptions options,
        CancellationToken cancellationToken = default)
    {
        var items = new List<NotionPushItemResult>();

        if (options.DryRun)
        {
            items.AddRange(records.Select(record =>
                new NotionPushItemResult(record, "review", true, null, null)));
            var dryResult = BuildResult(items);
            WriteReport(options.ReportPath, options.DatabaseId, dryRun: true, dryResult);
            return dryResult;
        }

        var isUpsert = options.Mode.Equals("upsert", StringComparison.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            string? existingPageId = null;
            if (isUpsert)
            {
                existingPageId = await QueryExistingPageAsync(http, options, record, cancellationToken);
            }

            if (existingPageId != null)
            {
                var (success, pageId, error) = await UpdatePageAsync(http, options, existingPageId, record, cancellationToken);
                items.Add(new NotionPushItemResult(record, "updated", success, pageId, error));
            }
            else
            {
                var (success, pageId, error) = await CreatePageAsync(http, options, record, cancellationToken);
                items.Add(new NotionPushItemResult(record, "created", success, pageId, error));
            }
        }

        var result = BuildResult(items);
        WriteReport(options.ReportPath, options.DatabaseId, dryRun: false, result);
        return result;
    }

    private static async Task<string?> QueryExistingPageAsync(
        HttpClient http,
        NotionPushOptions options,
        ImportSeedRecord record,
        CancellationToken ct)
    {
        var uniqueValue = GetUniqueFieldValue(record, options.UniqueField);
        if (string.IsNullOrWhiteSpace(uniqueValue)) return null;

        var filterJson = BuildSlugFilterJson(options.UniqueField, uniqueValue);
        var queryUrl = $"{NotionApiUrls.Base}/{NotionApiUrls.ApiVersion}/databases/{options.DatabaseId}/query";

        using var request = new HttpRequestMessage(HttpMethod.Post, queryUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        request.Headers.TryAddWithoutValidation("Notion-Version", NotionApiUrls.NotionVersion);
        request.Content = new StringContent(filterJson);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var results = doc.RootElement.GetProperty("results");
        if (results.GetArrayLength() > 0)
            return results[0].GetProperty("id").GetString();
        return null;
    }

    private static string GetUniqueFieldValue(ImportSeedRecord record, string uniqueField)
    {
        return uniqueField switch
        {
            "Slug" => record.Slug,
            "Title" => record.Title,
            _ => record.Slug
        };
    }

    private static string BuildSlugFilterJson(string propertyName, string value)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteStartObject("filter");
        writer.WriteString("property", propertyName);
        writer.WriteStartObject("rich_text");
        writer.WriteString("equals", value);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task<(bool Success, string? PageId, string? Error)> CreatePageAsync(
        HttpClient http, NotionPushOptions options, ImportSeedRecord record, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{NotionApiUrls.Base}/{NotionApiUrls.ApiVersion}/pages");
        BuildCommonRequestHeaders(request, options.Token);
        request.Content = new StringContent(BuildCreatePagePayload(options.DatabaseId, record));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.IsSuccessStatusCode)
            return (true, ExtractPageId(body), null);
        return (false, null, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
    }

    private static async Task<(bool Success, string? PageId, string? Error)> UpdatePageAsync(
        HttpClient http, NotionPushOptions options, string pageId,
        ImportSeedRecord record, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"{NotionApiUrls.Base}/{NotionApiUrls.ApiVersion}/pages/{pageId}");
        BuildCommonRequestHeaders(request, options.Token);
        request.Content = new StringContent(BuildUpdatePagePayload(record));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.IsSuccessStatusCode)
            return (true, pageId, null);
        return (false, pageId, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
    }

    private static void BuildCommonRequestHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Notion-Version", NotionApiUrls.NotionVersion);
    }

    private static NotionPushResult BuildResult(IReadOnlyList<NotionPushItemResult> items)
        => new(
            Total: items.Count,
            Created: items.Count(i => i.Success && i.Action == "created"),
            Updated: items.Count(i => i.Success && i.Action == "updated"),
            Skipped: items.Count(i => i.Success && i.Action == "skipped"),
            Failed: items.Count(i => !i.Success),
            Items: items);

    private static string BuildCreatePagePayload(string databaseId, ImportSeedRecord record)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteStartObject("parent");
        writer.WriteString("database_id", databaseId);
        writer.WriteEndObject();

        WriteProperties(writer, record);

        if (!string.IsNullOrWhiteSpace(record.Content))
        {
            var blocksJson = HtmlToNotionBlockConverter.ToBlocksJson(record.Content);
            if (blocksJson != "[]")
            {
                writer.WritePropertyName("children");
                writer.WriteRawValue(blocksJson);
            }
        }

        writer.WriteEndObject();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string BuildUpdatePagePayload(ImportSeedRecord record)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        WriteProperties(writer, record);
        writer.WriteEndObject();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteProperties(Utf8JsonWriter writer, ImportSeedRecord record)
    {
        writer.WriteStartObject("properties");
        WriteTitleProperty(writer, "Title", record.Title);
        WriteRichTextProperty(writer, "Slug", record.Slug);
        WriteSelectProperty(writer, "Type", record.Collection);
        WriteRichTextProperty(writer, "Summary", record.Summary);
        WriteRichTextProperty(writer, "Content", record.Content);
        WriteSelectProperty(writer, "Language", record.Language);
        WriteCheckboxProperty(writer, "Published", record.Published);
        WriteRichTextProperty(writer, "SeoTitle", record.SeoTitle);
        WriteRichTextProperty(writer, "SeoDescription", record.SeoDescription);
        writer.WriteEndObject();
    }

    private static void WriteTitleProperty(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteStartObject(name);
        writer.WriteStartArray("title");
        WriteTextObject(writer, value);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteRichTextProperty(Utf8JsonWriter writer, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        writer.WriteStartObject(name);
        writer.WriteStartArray("rich_text");
        WriteTextObject(writer, Truncate(value, 2000));
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSelectProperty(Utf8JsonWriter writer, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        writer.WriteStartObject(name);
        writer.WriteStartObject("select");
        writer.WriteString("name", value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteCheckboxProperty(Utf8JsonWriter writer, string name, bool value)
    {
        writer.WriteStartObject(name);
        writer.WriteBoolean("checkbox", value);
        writer.WriteEndObject();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static void WriteTextObject(Utf8JsonWriter writer, string value)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteStartObject("text");
        writer.WriteString("content", value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static string? ExtractPageId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteReport(string reportPath, string databaseId, bool dryRun, NotionPushResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteBoolean("dryRun", dryRun);
        writer.WriteString("databaseId", databaseId);
        writer.WriteNumber("recordCount", result.Total);
        writer.WriteNumber("created", result.Created);
        writer.WriteNumber("updated", result.Updated);
        writer.WriteNumber("skipped", result.Skipped);
        writer.WriteNumber("failed", result.Failed);
        writer.WriteStartArray("records");
        foreach (var item in result.Items)
        {
            writer.WriteStartObject();
            writer.WriteString("collection", item.Record.Collection);
            writer.WriteString("title", item.Record.Title);
            writer.WriteString("slug", item.Record.Slug);
            writer.WriteString("language", item.Record.Language);
            writer.WriteBoolean("published", item.Record.Published);
            writer.WriteString("action", item.Action);
            if (!string.IsNullOrWhiteSpace(item.NotionPageId))
                writer.WriteString("notionPageId", item.NotionPageId);
            if (!string.IsNullOrWhiteSpace(item.Error))
                writer.WriteString("error", item.Error);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        File.WriteAllText(reportPath, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }
}
