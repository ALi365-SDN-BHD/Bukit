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

        try
        {
            foreach (var record in records)
            {
                try
                {
                    items.Add(await PushRecordAsync(client, options, isUpsert, record, cancellationToken));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad record must not abort the batch or skip the report.
                    items.Add(new NotionPushItemResult(record, "error", false, null, ex.Message));
                }
            }
        }
        finally
        {
            if (options.WriteReport)
                WriteReport(options.ReportPath, options.DatabaseId, dryRun: false, BuildResult(items));
        }

        return BuildResult(items);
    }

    private static async Task<NotionPushItemResult> PushRecordAsync(
        NotionWriteClient client,
        NotionPushOptions options,
        bool isUpsert,
        ImportSeedRecord record,
        CancellationToken cancellationToken)
    {
        string? existingPageId = null;
        if (isUpsert)
        {
            var queryResult = await QueryExistingPageAsync(client, options, record, cancellationToken);
            if (!queryResult.QuerySucceeded)
                return new NotionPushItemResult(record, "query-failed", false, null,
                    $"Schema query failed: {queryResult.Error}");
            existingPageId = queryResult.PageId;
        }

        if (existingPageId is null)
        {
            var (success, pageId, error) = await CreatePageAsync(client, options, record, cancellationToken);
            return new NotionPushItemResult(record, "created", success, pageId, error);
        }

        var (updated, updatedPageId, updateError) =
            await UpdatePageAsync(client, options, existingPageId, record, cancellationToken);
        // null means "content was not provided"; an explicit "" clears the page body.
        if (!updated || !IsContentUpdate(options.UpdateContent) || record.Content is null)
            return new NotionPushItemResult(record, "updated", updated, updatedPageId, updateError);

        return await UpdateContentAsync(client, options, record, updatedPageId!, cancellationToken);
    }

    private static bool IsContentUpdate(string updateContent)
        => updateContent is "append" or "replace";

    private static async Task<NotionPushItemResult> UpdateContentAsync(
        NotionWriteClient client,
        NotionPushOptions options,
        ImportSeedRecord record,
        string pageId,
        CancellationToken cancellationToken)
    {
        var blocksJson = HtmlToNotionBlockConverter.ToBlocksJson(record.Content!);

        // Replace appends the new body first: a failed write must never leave the
        // page empty, which is what deleting the old blocks up front caused.
        var (readSuccess, existingBlockIds) = options.UpdateContent == "replace"
            ? await GetBlockChildrenIdsAsync(client, pageId, cancellationToken)
            : (true, []);
        if (!readSuccess)
            return new NotionPushItemResult(record, "replace-failed", false, pageId,
                "Failed to read existing block children.");

        if (blocksJson != "[]")
        {
            var (appendSuccess, appendError) = await AppendBlocksAsync(client, pageId, blocksJson, cancellationToken);
            if (!appendSuccess)
                return new NotionPushItemResult(record, "append-failed", false, pageId, appendError);
        }

        foreach (var blockId in existingBlockIds)
        {
            if (!await DeleteBlockAsync(client, blockId, cancellationToken))
                return new NotionPushItemResult(record, "replace-partial", false, pageId,
                    "New content was appended but one or more old blocks could not be deleted.");
        }

        return new NotionPushItemResult(record, "updated", true, pageId, null);
    }

    private const int MaxBlocksPerRequest = 100;

    private static async Task<(bool Success, string? Error)> AppendBlocksAsync(
        NotionWriteClient client, string pageId, string blocksJson, CancellationToken ct)
    {
        foreach (var chunk in ChunkBlockJson(blocksJson, MaxBlocksPerRequest))
        {
            var (success, error) = await AppendBlockChildrenAsync(client, pageId, chunk, ct);
            if (!success)
                return (false, error);
        }

        return (true, null);
    }

    private static IEnumerable<string> ChunkBlockJson(string blocksJson, int size)
    {
        using var document = JsonDocument.Parse(blocksJson);
        var chunk = new List<string>(size);
        foreach (var block in document.RootElement.EnumerateArray())
        {
            chunk.Add(block.GetRawText());
            if (chunk.Count < size)
                continue;

            yield return "[" + string.Join(",", chunk) + "]";
            chunk.Clear();
        }

        if (chunk.Count > 0)
            yield return "[" + string.Join(",", chunk) + "]";
    }

    private static async Task<QueryExistingPageResult> QueryExistingPageAsync(
        NotionWriteClient client,
        NotionPushOptions options,
        ImportSeedRecord record,
        CancellationToken ct)
    {
        var filter = BuildUniqueFilter(record, options);
        if (!filter.Success)
            return new QueryExistingPageResult(false, null, filter.Error);

        var response = await client.QueryDatabaseAsync(options.DatabaseId, filter.Json!, ct);
        if (!response.IsSuccess)
            return new QueryExistingPageResult(false, null, SafeError(response));

        var results = response.Payload!.Value.GetProperty("results");
        var matchCount = results.GetArrayLength();
        if (matchCount > 1)
        {
            return new QueryExistingPageResult(
                false,
                null,
                $"Unique field '{NotionPropertyNaming.Canonicalize(options.UniqueField ?? string.Empty)}' matched {matchCount} pages.");
        }

        if (matchCount == 1)
            return new QueryExistingPageResult(true, results[0].GetProperty("id").GetString(), null);
        return new QueryExistingPageResult(true, null, null);
    }

    private static (bool Success, string? Json, string? Error) BuildUniqueFilter(
        ImportSeedRecord record,
        NotionPushOptions options)
    {
        var propertyName = NotionPropertyNaming.Canonicalize(options.UniqueField ?? string.Empty);
        if (string.IsNullOrWhiteSpace(propertyName))
            return (false, null, "Unique field name is empty.");

        object? value;
        string filterKind;
        switch (propertyName)
        {
            case "Title":
                value = record.Title;
                filterKind = "title";
                break;
            case "Slug":
                value = record.Slug;
                filterKind = "rich_text";
                break;
            case "Type":
                value = record.Collection;
                filterKind = "select";
                break;
            case "Summary":
                value = record.Summary;
                filterKind = "rich_text";
                break;
            case "Language":
                value = record.Language;
                filterKind = "select";
                break;
            case "SeoTitle":
                value = record.SeoTitle;
                filterKind = "rich_text";
                break;
            case "SeoDescription":
                value = record.SeoDescription;
                filterKind = "rich_text";
                break;
            case "Published":
                value = record.Published;
                filterKind = "checkbox";
                break;
            case "Content":
                return (false, null, "Unique field 'Content' is not a Notion property.");
            default:
                if (!TryGetExtraField(record, propertyName, out value))
                    return (false, null, $"Unique field '{propertyName}' has no value for record '{record.Slug}'.");
                if (!TryResolveFilterKind(value, propertyName, options.Schema, out filterKind))
                    return (false, null, $"Unique field '{propertyName}' uses an unsupported value type.");
                break;
        }

        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
            return (false, null, $"Unique field '{propertyName}' has no value for record '{record.Slug}'.");

        return (true, BuildUniqueFilterJson(propertyName, filterKind, value), null);
    }

    private static bool TryGetExtraField(ImportSeedRecord record, string propertyName, out object? value)
    {
        value = null;
        if (record.ExtraFields is null)
            return false;

        foreach (var (name, candidate) in record.ExtraFields)
        {
            if (string.Equals(NotionPropertyNaming.Canonicalize(name), propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveFilterKind(
        object? value,
        string propertyName,
        IReadOnlyDictionary<string, string>? schema,
        out string filterKind)
    {
        if (schema is not null && schema.TryGetValue(propertyName, out var declaredType))
        {
            filterKind = declaredType switch
            {
                "rich_text" => "rich_text",
                "select" => "select",
                "url" => "url",
                "date" => "date",
                "number" => "number",
                "checkbox" => "checkbox",
                _ => string.Empty
            };
            return filterKind.Length > 0;
        }

        switch (value)
        {
            case bool:
                filterKind = "checkbox";
                return true;
            case int or long or float or double or decimal:
                filterKind = "number";
                return true;
            case string:
                filterKind = propertyName is "Link" or "Url" or "Href" ? "url" : "rich_text";
                return true;
            default:
                filterKind = string.Empty;
                return false;
        }
    }

    private static string BuildUniqueFilterJson(string propertyName, string filterKind, object value)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteStartObject("filter");
        writer.WriteString("property", propertyName);
        writer.WriteStartObject(filterKind);
        switch (value)
        {
            case bool b:
                writer.WriteBoolean("equals", b);
                break;
            case int i:
                writer.WriteNumber("equals", i);
                break;
            case long l:
                writer.WriteNumber("equals", l);
                break;
            case float f:
                writer.WriteNumber("equals", f);
                break;
            case double d:
                writer.WriteNumber("equals", d);
                break;
            case decimal m:
                writer.WriteNumber("equals", m);
                break;
            default:
                writer.WriteString("equals", value.ToString());
                break;
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task<(bool Success, string? PageId, string? Error)> CreatePageAsync(
        NotionWriteClient client, NotionPushOptions options, ImportSeedRecord record, CancellationToken ct)
    {
        var blocksJson = string.IsNullOrWhiteSpace(record.Content)
            ? "[]"
            : HtmlToNotionBlockConverter.ToBlocksJson(record.Content);
        var chunks = ChunkBlockJson(blocksJson, MaxBlocksPerRequest).ToList();

        var response = await client.CreatePageAsync(
            BuildCreatePagePayload(options.DatabaseId, record, options.Schema, chunks.FirstOrDefault()),
            ct);
        if (!response.IsSuccess)
            return (false, null, SafeError(response));

        var pageId = ExtractPageId(response.Payload);
        if (pageId is null || chunks.Count <= 1)
            return (true, pageId, null);

        foreach (var chunk in chunks.Skip(1))
        {
            var (success, error) = await AppendBlockChildrenAsync(client, pageId, chunk, ct);
            if (!success)
                return (false, pageId, $"Page created but remaining content failed to append: {error}");
        }

        return (true, pageId, null);
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
        IReadOnlyDictionary<string, string>? schema,
        string? childrenJson)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteStartObject("parent");
        writer.WriteString("database_id", databaseId);
        writer.WriteEndObject();

        WriteProperties(writer, record, schema);

        if (!string.IsNullOrWhiteSpace(childrenJson) && childrenJson != "[]")
        {
            writer.WritePropertyName("children");
            writer.WriteRawValue(childrenJson);
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
        // Slug is the upsert identity; a missing slug must not wipe the remote value.
        if (!string.IsNullOrEmpty(record.Slug))
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
            if (text is null)
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
        // null leaves the remote value untouched; an explicit empty string clears it.
        if (value is null) return;
        writer.WriteStartObject(name);
        writer.WriteStartArray("rich_text");
        if (value.Length > 0)
            WriteTextObject(writer, Truncate(value, 2000));
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSelectProperty(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) return;
        writer.WriteStartObject(name);
        if (value.Length == 0)
        {
            writer.WriteNull("select");
        }
        else
        {
            writer.WriteStartObject("select");
            writer.WriteString("name", value);
            writer.WriteEndObject();
        }
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

    private static void WriteUrlProperty(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) return;
        writer.WriteStartObject(name);
        if (value.Length == 0)
            writer.WriteNull("url");
        else
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
