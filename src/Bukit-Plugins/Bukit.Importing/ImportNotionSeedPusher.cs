using System.Text.Json;
using Bukit.Notion.Conversion;
using Bukit.Notion.Write;

namespace Bukit.Importing;

internal sealed record NotionPushOptions(
    string DatabaseId,
    string ReportPath,
    bool DryRun,
    string Mode = "create",
    string UniqueField = "Slug",
    string UpdateContent = "",
    bool WriteReport = true,
    IReadOnlyDictionary<string, string>? Schema = null);

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

internal sealed record QueryExistingPageResult(
    bool QuerySucceeded,
    string? PageId,
    string? Error);

internal static partial class NotionSeedPusher
{
    internal static async Task<NotionPushResult> PushAsync(
        NotionWriteClient client,
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
            if (options.WriteReport)
                WriteReport(options.ReportPath, options.DatabaseId, dryRun: true, dryResult);
            return dryResult;
        }

        var isUpsert = options.Mode.Equals("upsert", StringComparison.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            string? existingPageId = null;
            if (isUpsert)
            {
                var queryResult = await QueryExistingPageAsync(client, options, record, cancellationToken);
                if (!queryResult.QuerySucceeded)
                {
                    items.Add(new NotionPushItemResult(record, "query-failed", false, null,
                        $"Schema query failed: {queryResult.Error}"));
                    continue;
                }
                existingPageId = queryResult.PageId;
            }

            if (existingPageId != null)
            {
                var (success, pageId, error) = await UpdatePageAsync(client, options, existingPageId, record, cancellationToken);
                if (success && isUpsert && !string.IsNullOrWhiteSpace(record.Content) &&
                    (options.UpdateContent == "append" || options.UpdateContent == "replace"))
                {
                    if (options.UpdateContent == "replace")
                    {
                        var (readSuccess, existingBlockIds) = await GetBlockChildrenIdsAsync(client, pageId!, cancellationToken);
                        if (!readSuccess)
                        {
                            items.Add(new NotionPushItemResult(record, "replace-failed", false, pageId, "Failed to read existing block children."));
                            continue;
                        }
                        var allDeleted = true;
                        foreach (var blockId in existingBlockIds)
                        {
                            if (!await DeleteBlockAsync(client, blockId, cancellationToken))
                                allDeleted = false;
                        }
                        if (!allDeleted)
                        {
                            items.Add(new NotionPushItemResult(record, "replace-failed", false, pageId, "Failed to delete one or more existing blocks."));
                            continue;
                        }
                    }
                    var blocksJson = HtmlToNotionBlockConverter.ToBlocksJson(record.Content);
                    if (blocksJson != "[]")
                    {
                        var (appendSuccess, appendError) = await AppendBlockChildrenAsync(client, pageId!, blocksJson, cancellationToken);
                        if (!appendSuccess)
                        {
                            items.Add(new NotionPushItemResult(record, "append-failed", false, pageId, appendError));
                            continue;
                        }
                    }
                }
                items.Add(new NotionPushItemResult(record, "updated", success, pageId, error));
            }
            else
            {
                var (success, pageId, error) = await CreatePageAsync(client, options, record, cancellationToken);
                items.Add(new NotionPushItemResult(record, "created", success, pageId, error));
            }
        }

        var result = BuildResult(items);
        if (options.WriteReport)
            WriteReport(options.ReportPath, options.DatabaseId, dryRun: false, result);
        return result;
    }

    private static async Task<QueryExistingPageResult> QueryExistingPageAsync(
        NotionWriteClient client,
        NotionPushOptions options,
        ImportSeedRecord record,
        CancellationToken ct)
    {
        var uniqueValue = GetUniqueFieldValue(record, options.UniqueField);
        if (string.IsNullOrWhiteSpace(uniqueValue)) return new QueryExistingPageResult(true, null, null);

        var filterJson = BuildSlugFilterJson(options.UniqueField, uniqueValue);
        var response = await client.QueryDatabaseAsync(options.DatabaseId, filterJson, ct);
        if (!response.IsSuccess)
            return new QueryExistingPageResult(false, null, SafeError(response));

        var results = response.Payload!.Value.GetProperty("results");
        if (results.GetArrayLength() > 0)
            return new QueryExistingPageResult(true, results[0].GetProperty("id").GetString(), null);
        return new QueryExistingPageResult(true, null, null);
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
        NotionWriteClient client, NotionPushOptions options, ImportSeedRecord record, CancellationToken ct)
    {
        var response = await client.CreatePageAsync(
            BuildCreatePagePayload(options.DatabaseId, record, options.Schema),
            ct);
        if (response.IsSuccess)
            return (true, ExtractPageId(response.Payload), null);
        return (false, null, SafeError(response));
    }

    private static async Task<(bool Success, string? PageId, string? Error)> UpdatePageAsync(
        NotionWriteClient client, NotionPushOptions options, string pageId,
        ImportSeedRecord record, CancellationToken ct)
    {
        var response = await client.UpdatePageAsync(
            pageId,
            BuildUpdatePagePayload(record, options.Schema),
            ct);
        if (response.IsSuccess)
            return (true, pageId, null);
        return (false, pageId, SafeError(response));
    }

    private static async Task<(bool Success, string? Error)> AppendBlockChildrenAsync(
        NotionWriteClient client, string pageId,
        string blocksJson, CancellationToken ct)
    {
        var response = await client.AppendBlockChildrenAsync(
            pageId,
            $"{{\"children\":{blocksJson}}}",
            ct);
        if (response.IsSuccess)
            return (true, null);
        return (false, SafeError(response));
    }

    private static async Task<(bool Success, List<string> Ids)> GetBlockChildrenIdsAsync(
        NotionWriteClient client, string pageId, CancellationToken ct)
    {
        var ids = new List<string>();
        string? startCursor = null;
        var hasMore = true;
        var allSucceeded = true;

        while (hasMore)
        {
            var response = await client.ListBlockChildrenAsync(pageId, startCursor, ct);
            if (!response.IsSuccess)
            {
                allSucceeded = false;
                break;
            }

            var payload = response.Payload!.Value;
            foreach (var block in payload.GetProperty("results").EnumerateArray())
            {
                var id = block.GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id);
            }

            hasMore = payload.TryGetProperty("has_more", out var hm) && hm.GetBoolean();
            startCursor = hasMore && payload.TryGetProperty("next_cursor", out var nc)
                ? nc.GetString() : null;
        }
        return (allSucceeded, ids);
    }

    private static async Task<bool> DeleteBlockAsync(
        NotionWriteClient client, string blockId, CancellationToken ct)
    {
        var response = await client.ArchiveBlockAsync(blockId, ct);
        return response.IsSuccess;
    }

    private static NotionPushResult BuildResult(IReadOnlyList<NotionPushItemResult> items)
        => new(
            Total: items.Count,
            Created: items.Count(i => i.Success && i.Action == "created"),
            Updated: items.Count(i => i.Success && i.Action == "updated"),
            Skipped: items.Count(i => i.Success && i.Action == "skipped"),
            Failed: items.Count(i => !i.Success),
            Items: items);

    private static string BuildCreatePagePayload(
        string databaseId,
        ImportSeedRecord record,
        IReadOnlyDictionary<string, string>? schema)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteStartObject("parent");
        writer.WriteString("database_id", databaseId);
        writer.WriteEndObject();

        WriteProperties(writer, record, schema);

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

    private static string BuildUpdatePagePayload(
        ImportSeedRecord record,
        IReadOnlyDictionary<string, string>? schema)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        WriteProperties(writer, record, schema);
        writer.WriteEndObject();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteProperties(
        Utf8JsonWriter writer,
        ImportSeedRecord record,
        IReadOnlyDictionary<string, string>? schema)
    {
        writer.WriteStartObject("properties");
        WriteTitleProperty(writer, "Title", record.Title);
        WriteRichTextProperty(writer, "Slug", record.Slug);
        WriteSelectProperty(writer, "Type", record.Collection);
        WriteRichTextProperty(writer, "Summary", record.Summary);
        WriteSelectProperty(writer, "Language", record.Language);
        WriteCheckboxProperty(writer, "Published", record.Published);
        WriteRichTextProperty(writer, "SeoTitle", record.SeoTitle);
        WriteRichTextProperty(writer, "SeoDescription", record.SeoDescription);
        WriteExtraProperties(writer, record, schema);
        writer.WriteEndObject();
    }

    private static void WriteExtraProperties(
        Utf8JsonWriter writer,
        ImportSeedRecord record,
        IReadOnlyDictionary<string, string>? schema)
    {
        if (record.ExtraFields is null)
            return;

        foreach (var (name, value) in record.ExtraFields)
        {
            var propertyName = NotionPropertyNaming.Canonicalize(name);
            if (string.IsNullOrWhiteSpace(propertyName) || NotionPropertyNaming.IsCore(propertyName) || value is null)
                continue;

            if (schema is not null && schema.TryGetValue(propertyName, out var declaredType))
            {
                WriteTypedProperty(writer, propertyName, declaredType, value);
                continue;
            }

            if (value is bool b)
            {
                WriteCheckboxProperty(writer, propertyName, b);
                continue;
            }

            if (value is int or long or float or double or decimal)
            {
                WriteNumberProperty(writer, propertyName, Convert.ToDouble(value));
                continue;
            }

            if (value is IReadOnlyList<object?> values)
            {
                WriteMultiSelectProperty(writer, propertyName, values.Select(v => v?.ToString() ?? ""));
                continue;
            }

            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (propertyName is "Link" or "Url" or "Href")
                WriteUrlProperty(writer, propertyName, text);
            else
                WriteRichTextProperty(writer, propertyName, text);
        }
    }

    private static void WriteTypedProperty(Utf8JsonWriter writer, string name, string type, object value)
    {
        switch (type)
        {
            case "rich_text": WriteRichTextProperty(writer, name, (string)value); break;
            case "select": WriteSelectProperty(writer, name, (string)value); break;
            case "multi_select": WriteMultiSelectProperty(writer, name, ((IReadOnlyList<object?>)value).Select(v => (string)v!)); break;
            case "url": WriteUrlProperty(writer, name, (string)value); break;
            case "date": WriteDateProperty(writer, name, (string)value); break;
            case "number": WriteNumberProperty(writer, name, Convert.ToDouble(value)); break;
            case "checkbox": WriteCheckboxProperty(writer, name, (bool)value); break;
        }
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

    private static void WriteNumberProperty(Utf8JsonWriter writer, string name, double value)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("number", value);
        writer.WriteEndObject();
    }

    private static void WriteUrlProperty(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteStartObject(name);
        writer.WriteString("url", value);
        writer.WriteEndObject();
    }

    private static void WriteDateProperty(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteStartObject(name);
        writer.WriteStartObject("date");
        writer.WriteString("start", value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteMultiSelectProperty(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartObject(name);
        writer.WriteStartArray("multi_select");
        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            writer.WriteStartObject();
            writer.WriteString("name", value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
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

    private static string? ExtractPageId(JsonElement? payload)
    {
        return payload is { } value && value.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    private static string SafeError(NotionWriteResult result)
        => result.ErrorMessage ?? result.ReasonPhrase ?? "Notion request failed.";

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
