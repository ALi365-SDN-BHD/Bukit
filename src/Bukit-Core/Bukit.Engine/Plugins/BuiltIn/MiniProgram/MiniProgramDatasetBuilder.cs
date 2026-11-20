using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn.MiniProgram;

/// <summary>One public source record of a dataset, with its id, its absolute URL for content pages, and its sanitized body when needed.</summary>
internal sealed record MiniProgramRecord(string Id, ContentDocument Document, string? PageUrl, string? BodyHtml);

/// <summary>Projects records into JSON using only the declared fields, types and sort order.</summary>
internal sealed partial class MiniProgramDatasetBuilder
{
    // Built-in fields win over record properties of the same name, even when their value is null.
    // The exception is url: data records have no page, so their own url property is used.
    private static readonly Dictionary<string, Func<MiniProgramRecord, object?>> BuiltInFields = new(StringComparer.Ordinal)
    {
        ["id"] = record => record.Id,
        ["slug"] = record => record.Document.Slug,
        ["title"] = record => record.Document.Title,
        ["summary"] = record => record.Document.Record.Presentation.Summary,
        ["body"] = record => record.BodyHtml,
        ["url"] = record => record.PageUrl ?? CustomValue(record, "url"),
        ["publishedAt"] = record => record.Document.Record.Lifecycle.PublishedAt,
        ["updatedAt"] = record => record.Document.Record.Lifecycle.UpdatedAt,
        ["author"] = record => record.Document.Record.Ownership.Author,
        ["tags"] = record => record.Document.Record.Classification.Tags,
        ["categories"] = record => record.Document.Record.Classification.Sections,
        ["collection"] = record => record.Document.Record.Classification.Collection,
        ["language"] = record => record.Document.Record.Presentation.Language
    };

    private readonly MiniProgramUrls _urls;
    private readonly MiniProgramHtmlSanitizer _sanitizer;

    internal MiniProgramDatasetBuilder(MiniProgramUrls urls, MiniProgramHtmlSanitizer sanitizer)
    {
        _urls = urls;
        _sanitizer = sanitizer;
    }

    /// <summary>Sanitizes the body once per record, so list and detail files share the result.</summary>
    internal MiniProgramRecord WithBody(MiniProgramDatasetConfig dataset, MiniProgramRecord record, string? html)
        => record with { BodyHtml = SanitizeBody(dataset, record.Id, html, record.PageUrl) };

    private string SanitizeBody(MiniProgramDatasetConfig dataset, string recordId, string? html, string? pageUrl)
    {
        try
        {
            return _sanitizer.Sanitize(html, pageUrl ?? _urls.SiteRoot);
        }
        catch (MiniProgramSignedUrlException ex)
        {
            throw Invalid(dataset, recordId, "body", ex.Message, ex);
        }
    }

    internal static bool IsPublic(ContentDocument document)
        => ContentFieldReader.GetBool(document.CustomFields, "enabled") is not false && !document.Publish.Draft;

    internal static bool IsFromSource(ContentDocument document, string source)
        => string.Equals(ContentFieldReader.GetText(document.CustomFields, "sourceKey"), source, StringComparison.OrdinalIgnoreCase);

    internal JsonArray BuildItems(MiniProgramDatasetConfig dataset, IReadOnlyList<MiniProgramRecord> records, IReadOnlyList<string> fields)
    {
        var items = new JsonArray();
        foreach (var record in Sort(dataset, records))
        {
            items.Add(BuildItem(dataset, record, fields));
        }

        return items;
    }

    internal JsonObject BuildItem(MiniProgramDatasetConfig dataset, MiniProgramRecord record, IReadOnlyList<string> fields)
    {
        var item = new JsonObject { ["id"] = record.Id };
        foreach (var field in fields)
        {
            if (field == "id")
            {
                continue;
            }

            item[field] = Convert(dataset, record.Id, field, Resolve(record, field), record.PageUrl);
        }

        return item;
    }

    /// <summary>Converts a key/value map, such as one data index scope, applying the same required and type rules.</summary>
    internal JsonObject BuildMap(MiniProgramDatasetConfig dataset, IReadOnlyDictionary<string, object> values)
    {
        foreach (var required in dataset.Required ?? [])
        {
            if (!values.ContainsKey(required))
            {
                throw Invalid(dataset, dataset.Scope ?? dataset.Name, required, "is required but missing");
            }
        }

        var map = new JsonObject();
        var recordId = dataset.Scope ?? dataset.Name;
        foreach (var key in values.Keys.Order(StringComparer.Ordinal))
        {
            // Convert treats body as sanitized HTML, so a key of that name is sanitized here first.
            var value = values[key];
            if (key == "body")
            {
                value = value is null or string
                    ? SanitizeBody(dataset, recordId, (string?)value, pageUrl: null)
                    : throw Invalid(dataset, recordId, key, "is not an HTML string");
            }

            map[key] = Convert(dataset, recordId, key, value, pageUrl: null);
        }

        return map;
    }

    internal static IReadOnlyList<MiniProgramRecord> Sort(MiniProgramDatasetConfig dataset, IReadOnlyList<MiniProgramRecord> records)
    {
        var sort = dataset.Sort ?? (dataset.Kind == MiniProgramKinds.Collection ? new MiniProgramSortConfig("publishedAt", true) : null);
        if (sort is null)
        {
            return [.. records.OrderBy(record => record.Id, StringComparer.Ordinal)];
        }

        var keyed = records.Select(record => (Record: record, Key: SortKey(Resolve(record, sort.Field)))).ToList();
        keyed.Sort((left, right) =>
        {
            var compared = CompareSortKeys(left.Key, right.Key, sort.Descending);
            return compared != 0 ? compared : StringComparer.Ordinal.Compare(left.Record.Id, right.Record.Id);
        });
        return [.. keyed.Select(pair => pair.Record)];
    }

    internal static object? Resolve(MiniProgramRecord record, string field)
    {
        if (BuiltInFields.TryGetValue(field, out var accessor))
        {
            return accessor(record);
        }

        return CustomValue(record, field);
    }

    private static object? CustomValue(MiniProgramRecord record, string field)
        => record.Document.CustomFields is { } fields && fields.TryGetValue(field, out var custom) ? custom.Value : null;

    private JsonNode? Convert(MiniProgramDatasetConfig dataset, string recordId, string field, object? raw, string? pageUrl)
    {
        string? type = null;
        dataset.Types?.TryGetValue(field, out type);
        var required = dataset.Required?.Contains(field, StringComparer.Ordinal) == true;
        if (IsEmpty(raw))
        {
            return required ? throw Invalid(dataset, recordId, field, "is required but empty") : null;
        }

        if (field == "body")
        {
            // Already sanitized by WithBody or BuildMap.
            return (string)raw!;
        }

        // JSON has no NaN or Infinity, and the writer would fail later without naming the record.
        if (raw is double doubleValue && !double.IsFinite(doubleValue) || raw is float floatValue && !float.IsFinite(floatValue))
        {
            throw Invalid(dataset, recordId, field, "is not a finite number");
        }

        return type switch
        {
            null => Infer(raw!) ?? throw Invalid(dataset, recordId, field, "has a value that is not a scalar or a list of strings"),
            "string" => ToScalarText(raw!) ?? throw Invalid(dataset, recordId, field, "is not a string"),
            "number" => ToJsonNumber(raw!) ?? throw Invalid(dataset, recordId, field, "is not a number"),
            "boolean" => ToBoolean(raw!) is { } flag ? JsonValue.Create(flag) : throw Invalid(dataset, recordId, field, "is not a boolean"),
            "date" => ToDate(raw!) is { } date ? MiniProgramJson.FormatDate(date) : throw Invalid(dataset, recordId, field, "is not a date"),
            "string[]" => ToTextList(raw!) is { } values
                ? ToStringArray(values)
                : ToScalarText(raw!) is { } single ? ToStringArray([single]) : throw Invalid(dataset, recordId, field, "is not a list of strings"),
            "image" or "url" => ToUrl(dataset, recordId, field, raw!, type == "image", pageUrl),
            _ => throw new InvalidOperationException($"Unsupported mini-program field type '{type}'.")
        };
    }

    private string ToUrl(MiniProgramDatasetConfig dataset, string recordId, string field, object raw, bool image, string? pageUrl)
    {
        if (raw is not string text)
        {
            text = (ToTextList(raw) is { Count: > 0 } list ? list[0] : null) ?? throw Invalid(dataset, recordId, field, "is not a URL");
        }

        var absolute = _urls.Absolute(text, pageUrl ?? _urls.SiteRoot);
        if (!Uri.TryCreate(absolute, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            throw Invalid(dataset, recordId, field, $"is not an http(s) URL: {text}");
        }

        if (image && MiniProgramJson.IsSignedUrl(absolute))
        {
            throw Invalid(dataset, recordId, field, $"signed URL was not localized: {MiniProgramJson.RedactQuery(absolute)}");
        }

        return absolute;
    }

    /// <summary>Infers the JSON value of an untyped field; null when the value has no portable JSON form.</summary>
    private static JsonNode? Infer(object raw) => raw switch
    {
        string text => text,
        bool flag => flag,
        int number => number,
        long number => number,
        double number => number,
        float number => number,
        decimal number => number,
        DateTimeOffset date => MiniProgramJson.FormatDate(date),
        DateTime date => MiniProgramJson.FormatDate(new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc))),
        _ when ToTextList(raw) is { } list => ToStringArray(list),
        _ => null
    };

    /// <summary>A string or a culture-invariant scalar; null for lists and other objects.</summary>
    private static string? ToScalarText(object raw) => raw switch
    {
        string text => text,
        bool flag => flag ? "true" : "false",
        DateTimeOffset date => MiniProgramJson.FormatDate(date),
        DateTime date => MiniProgramJson.FormatDate(new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc))),
        IFormattable formattable and (int or long or double or float or decimal) => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => null
    };

    private static JsonArray ToStringArray(IReadOnlyList<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static bool IsEmpty(object? raw) => raw switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        System.Collections.ICollection collection => collection.Count == 0,
        IEnumerable<object?> sequence => !sequence.Any(),
        _ => false
    };

    /// <summary>A list whose items are all strings or invariant scalars; null for scalars, maps and mixed lists.</summary>
    private static List<string>? ToTextList(object raw)
    {
        if (raw is string || raw is System.Collections.IDictionary || raw is not System.Collections.IEnumerable sequence)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in sequence)
        {
            if (item is null || ToScalarText(item) is not { } text)
            {
                return null;
            }

            values.Add(text);
        }

        return values;
    }

    private static double? ToNumber(object raw) => raw switch
    {
        double value => value,
        float value => value,
        int value => value,
        long value => value,
        decimal value => (double)value,
        string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) => value,
        _ => null
    };

    /// <summary>The value as a JSON number; integers and decimals keep every digit instead of rounding through double.</summary>
    private static JsonNode? ToJsonNumber(object raw) => raw switch
    {
        int value => value,
        long value => value,
        decimal value => value,
        // Text already in JSON number form is written as is, so no digit is rounded away.
        string text when JsonNumber().IsMatch(text.Trim()) && ToNumber(text) is not null => JsonNode.Parse(text.Trim()),
        string text when long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
        _ => ToNumber(raw) is { } value ? JsonValue.Create(value) : null
    };

    private static bool? ToBoolean(object raw) => raw switch
    {
        bool value => value,
        string text when bool.TryParse(text.Trim(), out var value) => value,
        _ => null
    };

    private static DateTimeOffset? ToDate(object raw) => raw switch
    {
        DateTimeOffset value => value,
        DateTime value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
        // Only ISO 8601 text: "May" or "3/4/2026" would otherwise become a guessed date.
        string text when IsoDate().IsMatch(text) &&
                         DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) => value,
        _ => null
    };

    /// <summary>
    /// Dates arrive as DateTimeOffset, DateTime or ISO text depending on the source; giving them one
    /// form keeps a sort chronological instead of grouping records by how their date was stored.
    /// </summary>
    private static object? SortKey(object? value) => value switch
    {
        DateTime date => new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)),
        string text when IsoDate().IsMatch(text) &&
                         DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) => date,
        _ => value
    };

    [GeneratedRegex(@"^\s*\d{4}-\d{2}-\d{2}([Tt ]|\s*$)", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex JsonNumber();

    private static int CompareSortKeys(object? left, object? right, bool descending)
    {
        if (left is null || right is null)
        {
            // Records without a sort value always come last.
            return left is null ? (right is null ? 0 : 1) : -1;
        }

        // Rank by kind first (number, date, text) so mixed keys still compare transitively. The kind
        // order does not flip with the direction, so text placeholders stay after real values, as nulls do.
        var leftNumber = ToNumber(left);
        var rightNumber = ToNumber(right);
        var rank = SortRank(left, leftNumber).CompareTo(SortRank(right, rightNumber));
        if (rank != 0)
        {
            return rank;
        }

        // Integers and decimals compare exactly; double alone would tie integers above 2^53.
        var compared = leftNumber is { } l && rightNumber is { } r
                ? (ToExactNumber(left) is { } exactLeft && ToExactNumber(right) is { } exactRight ? exactLeft.CompareTo(exactRight) : l.CompareTo(r))
            : left is DateTimeOffset leftDate && right is DateTimeOffset rightDate ? leftDate.CompareTo(rightDate)
            : StringComparer.Ordinal.Compare(SortText(left), SortText(right));
        return descending ? -compared : compared;
    }

    private static decimal? ToExactNumber(object raw) => raw switch
    {
        int value => value,
        long value => value,
        decimal value => value,
        string text when decimal.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => value,
        _ => null
    };

    private static int SortRank(object value, double? number)
        => number is not null ? 0 : value is DateTimeOffset ? 1 : 2;

    private static string SortText(object value)
        => ToScalarText(value) ?? (ToTextList(value) is { } list ? string.Join('\n', list) : string.Empty);

    internal static BukitException Invalid(MiniProgramDatasetConfig dataset, string recordId, string field, string problem, Exception? inner = null)
    {
        var message = $"miniprogram dataset '{dataset.Name}' record '{recordId}' field '{field}' {problem}.";
        return inner is null
            ? new BukitException(message, DiagnosticCode.SchemaValidationFailed)
            : new BukitException(message, inner, DiagnosticCode.SchemaValidationFailed);
    }
}
