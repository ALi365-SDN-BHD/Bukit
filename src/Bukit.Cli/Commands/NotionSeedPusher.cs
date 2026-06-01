using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Shared.Notion;

namespace Bukit.Cli.Commands;

internal sealed record NotionPushOptions(
    string DatabaseId,
    string Token,
    string ReportPath,
    bool DryRun);

internal sealed record NotionPushItemResult(
    ImportSeedRecord Record,
    string Action,
    bool Success,
    string? NotionPageId,
    string? Error);

internal sealed record NotionPushResult(
    int Total,
    int Pushed,
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

        foreach (var record in records)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{NotionApiUrls.Base}/{NotionApiUrls.ApiVersion}/pages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
            request.Headers.TryAddWithoutValidation("Notion-Version", NotionApiUrls.NotionVersion);
            request.Content = new StringContent(BuildCreatePagePayload(options.DatabaseId, record));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                items.Add(new NotionPushItemResult(record, "created", true, ExtractPageId(body), null));
            }
            else
            {
                items.Add(new NotionPushItemResult(record, "failed", false, null,
                    string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body));
            }
        }

        var result = BuildResult(items);
        WriteReport(options.ReportPath, options.DatabaseId, dryRun: false, result);
        return result;
    }

    private static NotionPushResult BuildResult(IReadOnlyList<NotionPushItemResult> items)
        => new(
            Total: items.Count,
            Pushed: items.Count(i => i.Success && i.Action == "created"),
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

        var blocks = BuildParagraphBlocks(record.Content).ToList();
        if (blocks.Count > 0)
        {
            writer.WriteStartArray("children");
            foreach (var block in blocks)
                WriteParagraphBlock(writer, block);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
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

    private static void WriteParagraphBlock(Utf8JsonWriter writer, string text)
    {
        writer.WriteStartObject();
        writer.WriteString("object", "block");
        writer.WriteString("type", "paragraph");
        writer.WriteStartObject("paragraph");
        writer.WriteStartArray("rich_text");
        WriteTextObject(writer, text);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteTextObject(Utf8JsonWriter writer, string value)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteStartObject("text");
        writer.WriteString("content", value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static IEnumerable<string> BuildParagraphBlocks(string? html)
    {
        var plain = StripHtml(html ?? "");
        if (string.IsNullOrWhiteSpace(plain)) yield break;

        const int max = 1900;
        for (var i = 0; i < plain.Length; i += max)
            yield return plain.Substring(i, Math.Min(max, plain.Length - i));
    }

    private static string StripHtml(string html)
        => WhitespacePattern().Replace(HtmlTagPattern().Replace(html, " "), " ").Trim();

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

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
        writer.WriteNumber("pushed", result.Pushed);
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

    [GeneratedRegex("<[^>]*>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
