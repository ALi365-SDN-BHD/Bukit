using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static partial class CanonicalContentGraphBuilder
{
    private static IReadOnlyList<string> MergeLists(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
    {
        var result = new List<string>();
        if (first is not null)
        {
            result.AddRange(first);
        }

        if (second is not null)
        {
            foreach (var value in second)
            {
                if (!result.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(value);
                }
            }
        }

        return result;
    }

    private static string? ReadMapString(IReadOnlyDictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    private static IReadOnlyList<string>? ReadMapList(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value))
        {
            return null;
        }

        return ContentFieldReader.ToTextList(value);
    }

    private static string? FirstText(ContentRecordSource source, string key)
        => ContentFieldReader.GetText(source.Fields, key);

    private static IReadOnlyList<string>? FirstList(ContentRecordSource source, string key)
        => ContentFieldReader.GetTextList(source.Fields, key);

    private static DateTimeOffset? FirstDate(ContentRecordSource source, string key)
        => ContentFieldReader.GetDate(source.Fields, key);

    private static double? FirstDouble(ContentRecordSource source, string key)
        => ContentFieldReader.GetNumber(source.Fields, key);

    private static SeoGeoMetaParser.ParsedGeoMeta ParseGeoMeta(ContentRecordSource source)
        => SeoGeoMetaParser.ParseGeoMeta(source.Fields);

    private static string InferEntityTypeFromKey(string key)
    {
        var normalized = key[..^"_links".Length];
        return normalized switch
        {
            "people" or "authors" or "reviewers" or "owners" => "person",
            "companies" or "organization" or "organizations" => "company",
            "places" => "place",
            "products" => "product",
            "services" => "service",
            _ => "thing"
        };
    }

    private static string? InferMediaKind(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (normalized.Contains("image", StringComparison.Ordinal) ||
            normalized.Contains("cover", StringComparison.Ordinal) ||
            normalized.Contains("icon", StringComparison.Ordinal) ||
            normalized.Contains("gallery", StringComparison.Ordinal))
        {
            return "image";
        }

        if (normalized.Contains("video", StringComparison.Ordinal))
        {
            return "video";
        }

        if (normalized.Contains("file", StringComparison.Ordinal) ||
            normalized.Contains("attachment", StringComparison.Ordinal) ||
            normalized.Contains("document", StringComparison.Ordinal))
        {
            return "file";
        }

        return null;
    }

    private static string? ReadObjectString(IReadOnlyDictionary<string, object?> map, string key)
    {
        return map.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    private static string? ReadMappedValue(MappedValue value, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || value.Map is null)
        {
            return null;
        }

        return value.Map.TryGetValue(key, out var raw) && raw is not null
            ? raw.ToString()
            : null;
    }

    private static IReadOnlyList<string>? ReadMappedList(MappedValue value, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            value.Map is null ||
            !value.Map.TryGetValue(key, out var raw))
        {
            return null;
        }

        return ContentFieldReader.ToTextList(raw);
    }

    private sealed record ContentRecordSource(
        string Id,
        string Title,
        string Slug,
        DateTimeOffset PublishAt,
        string? ContentHtml,
        IReadOnlyDictionary<string, ContentField>? Fields,
        ContentModelSchema? Schema);

    private sealed record MappedValue(string? Scalar, IReadOnlyDictionary<string, object?>? Map);
}
