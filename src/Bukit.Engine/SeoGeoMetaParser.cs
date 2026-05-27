using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
namespace Bukit.Engine;

internal static class SeoGeoMetaParser
{
    internal sealed record ParsedGeoMeta(
        string? SchemaType,
        IReadOnlyList<GeoFaqModel>? FaqItems,
        IReadOnlyList<GeoHowToStepModel>? HowToSteps,
        IReadOnlyList<GeoCitationModel>? Citations,
        GeoAuthorModel? GeoAuthor,
        string? SpeakableXPath,
        IReadOnlyList<string>? SameAs,
        string? About,
        DateTimeOffset? DateReviewed)
    {
        public static readonly ParsedGeoMeta Empty = new(null, null, null, null, null, null, null, null, null);
    }

    internal static ParsedGeoMeta ParseGeoMeta(ContentItem item)
    {
        if (!item.Meta.TryGetValue("geo", out var geoValue) || geoValue is not IReadOnlyDictionary<string, object> geo)
        {
            return ParsedGeoMeta.Empty;
        }

        var schemaType = ReadGeoString(geo, "schema_type");
        var speakableXPath = ReadGeoString(geo, "speakable_xpath")
            ?? (geo.TryGetValue("speakable", out var sp) && sp is IReadOnlyDictionary<string, object> spMap
                ? ReadGeoString(spMap, "xpath")
                : null);

        var sameAs = ReadGeoStringList(geo, "same_as");
        var citations = ReadGeoCitations(geo);
        var faqItems = ReadGeoFaqItems(geo);
        var howToSteps = ReadGeoHowToSteps(geo);
        var geoAuthor = ReadGeoAuthor(geo);
        var about = ReadGeoString(geo, "about");
        var dateReviewed = ReadGeoDateTime(geo, "date_reviewed");

        return new ParsedGeoMeta(schemaType, faqItems, howToSteps, citations, geoAuthor, speakableXPath, sameAs, about, dateReviewed);
    }

    private static DateTimeOffset? ReadGeoDateTime(IReadOnlyDictionary<string, object> map, string key)
    {
        var value = ReadGeoString(map, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var dt) ? dt : null;
    }

    private static string? ReadGeoString(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        var s = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static IReadOnlyList<string>? ReadGeoStringList(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is IEnumerable<object> seq)
        {
            var list = seq
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();
            return list.Count == 0 ? null : list;
        }

        if (value is string s)
        {
            var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        return null;
    }

    private static IReadOnlyList<GeoFaqModel>? ReadGeoFaqItems(IReadOnlyDictionary<string, object> geo)
    {
        if (!geo.TryGetValue("faq", out var value) || value is not IEnumerable<object> items)
        {
            return null;
        }

        var result = new List<GeoFaqModel>();
        foreach (var item in items)
        {
            if (item is IReadOnlyDictionary<string, object> entry)
            {
                var question = ReadGeoString(entry, "question");
                var answer = ReadGeoString(entry, "answer");
                if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
                {
                    result.Add(new GeoFaqModel { Question = question, Answer = answer });
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<GeoHowToStepModel>? ReadGeoHowToSteps(IReadOnlyDictionary<string, object> geo)
    {
        if (!geo.TryGetValue("steps", out var value) || value is not IEnumerable<object> items)
        {
            return null;
        }

        var result = new List<GeoHowToStepModel>();
        foreach (var item in items)
        {
            if (item is IReadOnlyDictionary<string, object> entry)
            {
                var name = ReadGeoString(entry, "name");
                var text = ReadGeoString(entry, "text");
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new GeoHowToStepModel
                    {
                        Name = name,
                        Text = text,
                        Image = ReadGeoString(entry, "image"),
                        Url = ReadGeoString(entry, "url")
                    });
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<GeoCitationModel>? ReadGeoCitations(IReadOnlyDictionary<string, object> geo)
    {
        if (!geo.TryGetValue("citations", out var value) || value is not IEnumerable<object> items)
        {
            return null;
        }

        var result = new List<GeoCitationModel>();
        foreach (var item in items)
        {
            if (item is IReadOnlyDictionary<string, object> entry)
            {
                var title = ReadGeoString(entry, "title");
                var url = ReadGeoString(entry, "url");
                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(url))
                {
                    result.Add(new GeoCitationModel { Title = title, Url = url });
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static GeoAuthorModel? ReadGeoAuthor(IReadOnlyDictionary<string, object> geo)
    {
        if (!geo.TryGetValue("author", out var value) || value is not IReadOnlyDictionary<string, object> author)
        {
            return null;
        }

        var name = ReadGeoString(author, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new GeoAuthorModel
        {
            Name = name,
            Url = ReadGeoString(author, "url"),
            SameAs = ReadGeoStringList(author, "same_as") ?? Array.Empty<string>()
        };
    }
}
