using Bukit.Engine.Abstractions.Content;
using System.Globalization;

namespace Bukit.Content.Markdown;

internal static class MarkdownFieldBuilder
{
    internal static IReadOnlyDictionary<string, ContentField> BuildFields(IReadOnlyDictionary<string, object> frontMatterValues)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in frontMatterValues)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value is null)
            {
                continue;
            }

            var key = kv.Key.Trim();
            if (IsReservedMetaKey(key))
            {
                continue;
            }

            if (TryConvertToField(kv.Value, out var field))
            {
                fields[key] = field;
            }
        }

        if (frontMatterValues.TryGetValue("tags", out var tagsObj) && tagsObj is not null && TryConvertToList(tagsObj, out var tags))
        {
            fields["tags"] = new ContentField("list", tags);
        }

        if (frontMatterValues.TryGetValue("categories", out var catsObj) && catsObj is not null && TryConvertToList(catsObj, out var cats))
        {
            fields["categories"] = new ContentField("list", cats);
        }

        if (frontMatterValues.TryGetValue("summary", out var summaryObj) && summaryObj is not null)
        {
            fields["summary"] = new ContentField("text", summaryObj.ToString() ?? string.Empty);
        }

        return fields;
    }

    private static bool IsReservedMetaKey(string key)
    {
        return key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("slug", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("type", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("publishAt", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("language", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("categories", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("summary", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("route", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("url", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("outputPath", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("template", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertToField(object value, out ContentField field)
    {
        if (TryConvertToList(value, out var list))
        {
            field = new ContentField("list", list);
            return true;
        }

        if (value is System.Collections.IEnumerable and not string)
        {
            field = new ContentField("list", value);
            return true;
        }

        if (value is bool b)
        {
            field = new ContentField("bool", b);
            return true;
        }

        if (value is int or long or float or double or decimal)
        {
            field = new ContentField("number", value);
            return true;
        }

        if (value is DateTime dt)
        {
            field = new ContentField("date", dt);
            return true;
        }

        if (value is DateTimeOffset dto)
        {
            field = new ContentField("date", dto);
            return true;
        }

        var text = value.ToString() ?? string.Empty;

        // Number parsing is explicitly invariant and runs before any date
        // heuristic so numeric-looking text never depends on the thread culture
        // or on culture-dependent date inference such as "1.25".
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
        {
            field = new ContentField("number", parsedLong);
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
        {
            field = new ContentField("number", parsedDouble);
            return true;
        }

        if (TryParseDateTimeOffset(text, out var parsed))
        {
            field = new ContentField("date", parsed);
            return true;
        }

        if (bool.TryParse(text, out var parsedBool))
        {
            field = new ContentField("bool", parsedBool);
            return true;
        }

        field = new ContentField("text", text);
        return true;
    }

    private static bool TryConvertToList(object value, out IReadOnlyList<string> list)
    {
        if (value is IEnumerable<object> seq && seq.All(IsScalarValue))
        {
            var items = seq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            list = items;
            return items.Count > 0;
        }

        list = Array.Empty<string>();
        return false;
    }

    private static bool IsScalarValue(object? value)
        => value is null or string or bool or int or long or double or float or decimal or DateTime or DateTimeOffset;

    internal static bool TryParseDateTimeOffset(string text, out DateTimeOffset value)
    {
        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);
    }
}
