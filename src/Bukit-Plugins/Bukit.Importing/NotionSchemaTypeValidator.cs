namespace Bukit.Importing;

/// <summary>
/// Validates Notion seed record values against declared schema types.
/// Extracted from ImportNotionPushWorkflow for single-responsibility.
/// </summary>
internal static class NotionSchemaTypeValidator
{
    internal static void ValidateTypedValues(
        string databaseKey,
        IReadOnlyList<ImportSeedRecord> records,
        IReadOnlyDictionary<string, string> effectiveSchema)
    {
        foreach (var record in records)
        {
            if (record.ExtraFields is null)
                continue;
            foreach (var (rawName, value) in record.ExtraFields)
            {
                var field = NotionPropertyNaming.Canonicalize(rawName);
                if (value is null || !effectiveSchema.TryGetValue(field, out var type))
                    continue;
                var error = GetCompatibilityError(type, value);
                if (error is not null)
                    throw new FormatException(
                        $"Invalid typed Notion value in database '{databaseKey}', field '{field}', record '{record.Slug}': {error}");
            }
        }
    }

    internal static string? GetCompatibilityError(string type, object value)
    {
        if (type == "multi_select" && value is IReadOnlyList<object?> items)
        {
            if (items.Any(item => item is not string))
                return "expected multi_select with string options.";
            var options = items.Cast<string>().ToArray();
            if (options.Any(string.IsNullOrWhiteSpace))
                return "multi_select contains a blank option.";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in options)
            {
                if (!seen.Add(option))
                    return $"multi_select contains duplicate option '{option}'.";
            }
            return null;
        }

        if (type == "number" && IsNumeric(value))
        {
            var number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            return double.IsFinite(number) ? null : "expected a finite number.";
        }

        var compatible = type switch
        {
            "rich_text" or "select" => value is string,
            "multi_select" => false,
            "url" => value is string text && IsAbsoluteHttpUrl(text),
            "date" => value is string text && IsIsoDate(text),
            "number" => false,
            "checkbox" => value is bool,
            _ => false
        };
        return compatible ? null : $"expected {type}.";
    }

    internal static IReadOnlyList<(string Name, string ExpectedType)> BuildAdditionalSchemaFields(
        string collection,
        IReadOnlyList<ImportSeedRecord> records)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (collection.Equals("navigation", StringComparison.OrdinalIgnoreCase))
        {
            fields["Link"] = "url";
            fields["Order"] = "number";
            fields["Enabled"] = "checkbox";
        }

        foreach (var record in records)
        {
            if (record.ExtraFields is null)
                continue;

            foreach (var (name, value) in record.ExtraFields)
            {
                var propertyName = NotionPropertyNaming.Canonicalize(name);
                if (string.IsNullOrWhiteSpace(propertyName) || NotionPropertyNaming.IsCore(propertyName) || value is null)
                    continue;

                fields.TryAdd(propertyName, ToNotionPropertyType(propertyName, value));
            }
        }

        // Imported links and page routes are site paths ("/about/"). Notion url properties
        // are not documented to accept relative values, so such fields are text instead.
        foreach (var name in fields.Keys.Where(name => fields[name] == "url").ToList())
        {
            if (records.Any(record => HasNonHttpValue(record, name)))
                fields[name] = "rich_text";
        }

        return fields
            .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .Select(f => (f.Key, f.Value))
            .ToArray();
    }

    private static bool HasNonHttpValue(ImportSeedRecord record, string propertyName)
        => record.ExtraFields?.Any(field =>
               NotionPropertyNaming.Canonicalize(field.Key).Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
               field.Value is string text && !IsAbsoluteHttpUrl(text)) == true;

    private static string ToNotionPropertyType(string propertyName, object value)
    {
        if (value is bool)
            return "checkbox";
        if (value is int or long or float or double or decimal)
            return "number";
        if (value is IReadOnlyList<object?>)
            return "multi_select";
        if (propertyName is "Link" or "Url" or "Href")
            return "url";
        return "rich_text";
    }

    internal static bool IsAbsoluteHttpUrl(string text)
        => Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static bool IsNumeric(object value)
        => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static bool IsIsoDate(string value)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _))
            return true;

        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm:ssK",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"
        };
        return DateTimeOffset.TryParseExact(value, formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _);
    }
}
