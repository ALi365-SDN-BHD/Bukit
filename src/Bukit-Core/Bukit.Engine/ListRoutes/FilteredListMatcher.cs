using System.Collections;
using System.Globalization;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class FilteredListMatcher
{
    internal static bool Matches(IReadOnlyDictionary<string, ContentField>? fields, FilteredListConfig filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!ContentFieldReader.TryGetField(fields, filter.Field, out var field) || field.Value is null)
        {
            return false;
        }

        var actualValues = EnumerateValues(field.Value).ToArray();
        if (actualValues.Length == 0)
        {
            return false;
        }

        var expectedValues = ResolveExpectedValues(filter)
            .Select(CreateMatchValue)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
        if (expectedValues.Length == 0)
        {
            return false;
        }

        return NormalizeOperator(filter.Operator) switch
        {
            "contains" => actualValues.Any(actual => expectedValues.Any(expected => Contains(actual, expected))),
            "in" => actualValues.Any(actual => expectedValues.Any(expected => EqualsValue(actual, expected))),
            _ => actualValues.Any(actual => expectedValues.Any(expected => EqualsValue(actual, expected)))
        };
    }

    internal static string NormalizeOperator(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "equals" : normalized!;
    }

    internal static IReadOnlyList<string> ResolveExpectedValues(FilteredListConfig filter)
    {
        if (filter.Values is { Count: > 0 })
        {
            var values = filter.Values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();
            if (values.Length > 0)
            {
                return values;
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Value))
        {
            return new[] { filter.Value.Trim() };
        }

        return Array.Empty<string>();
    }

    private static IEnumerable<MatchValue> EnumerateValues(object? value)
    {
        switch (value)
        {
            case null:
                yield break;
            case string:
            case DateTimeOffset:
            case DateTime:
                if (CreateMatchValue(value) is { } scalar)
                {
                    yield return scalar;
                }
                yield break;
            case IEnumerable enumerable:
                foreach (var item in enumerable)
                {
                    if (IsScalarMatchValue(item) && CreateMatchValue(item) is { } entry)
                    {
                        yield return entry;
                    }
                }
                yield break;
            default:
                if (IsScalarMatchValue(value) && CreateMatchValue(value) is { } fallback)
                {
                    yield return fallback;
                }
                yield break;
        }
    }

    private static MatchValue? CreateMatchValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var date = TryReadDate(value);
        var text = value switch
        {
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            _ => value.ToString()?.Trim()
        };

        return string.IsNullOrWhiteSpace(text) ? null : new MatchValue(text, date);
    }

    private static DateTimeOffset? TryReadDate(object value)
    {
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto) => dto,
            _ => null
        };
    }

    private static bool EqualsValue(MatchValue actual, MatchValue expected)
    {
        if (actual.Date is not null && expected.Date is not null)
        {
            return actual.Date.Value.Date == expected.Date.Value.Date;
        }

        return TextEquals(actual.Text, expected.Text) ||
               SlugEquals(actual.Text, expected.Text);
    }

    private static bool Contains(MatchValue actual, MatchValue expected)
    {
        if (actual.Date is not null && expected.Date is not null)
        {
            return actual.Date.Value.Date == expected.Date.Value.Date;
        }

        return actual.Text.Contains(expected.Text, StringComparison.OrdinalIgnoreCase) ||
               SlugContains(actual.Text, expected.Text);
    }

    private static bool TextEquals(string actual, string expected)
        => string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool SlugEquals(string actual, string expected)
    {
        var actualSlug = SlugHelper.Slugify(actual);
        var expectedSlug = SlugHelper.Slugify(expected);
        return !string.IsNullOrWhiteSpace(actualSlug) &&
               !string.IsNullOrWhiteSpace(expectedSlug) &&
               string.Equals(actualSlug, expectedSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SlugContains(string actual, string expected)
    {
        var actualSlug = SlugHelper.Slugify(actual);
        var expectedSlug = SlugHelper.Slugify(expected);
        return !string.IsNullOrWhiteSpace(actualSlug) &&
               !string.IsNullOrWhiteSpace(expectedSlug) &&
               actualSlug.Contains(expectedSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScalarMatchValue(object? value)
        => value is string or char or bool or
            byte or sbyte or short or ushort or int or uint or long or ulong or
            float or double or decimal or DateTimeOffset or DateTime;

    private readonly record struct MatchValue(string Text, DateTimeOffset? Date);
}
