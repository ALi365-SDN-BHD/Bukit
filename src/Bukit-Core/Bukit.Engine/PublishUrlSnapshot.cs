using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record PublishUrlSnapshot(
    string Schema,
    string SiteUrl,
    IReadOnlyList<PublishUrlSnapshotRoute> Routes);

internal sealed record PublishUrlSnapshotRoute(
    string Url,
    bool Indexable,
    string SemanticHash);

internal sealed record PublishUrlChangeSet(IReadOnlyList<PublishUrlChange> Changes);

internal sealed record PublishUrlChange(string Type, string Url, string? SemanticHash);

internal static class PublishUrlSnapshotBuilder
{
    internal static PublishUrlSnapshot Build(AppConfig config, IReadOnlyList<BuildVariantResult> variants)
    {
        var routes = variants
            .SelectMany(BuildRoutes)
            .GroupBy(route => route.Url, StringComparer.Ordinal)
            .Select(ResolveDuplicate)
            .OrderBy(route => route.Url, StringComparer.Ordinal)
            .ToArray();

        return new PublishUrlSnapshot(
            BuildReporter.PublishUrlSnapshotSchema,
            ResolveSiteUrl(config.Site.Url, routes),
            routes);
    }

    private static IEnumerable<PublishUrlSnapshotRoute> BuildRoutes(BuildVariantResult variant)
    {
        var documents = variant.RoutedDocuments
            .Concat(variant.DerivedDocuments)
            .GroupBy(item => item.Route.OutputPath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Document, StringComparer.Ordinal);

        foreach (var pair in variant.SeoIndex)
        {
            var entry = pair.Value;
            if (!entry.Indexable)
            {
                continue;
            }

            if (!variant.SeoModels.TryGetValue(pair.Key, out var model))
            {
                throw new InvalidOperationException($"SEO model is required for publish URL '{entry.Canonical}'.");
            }

            documents.TryGetValue(entry.Route.OutputPath, out var document);
            var url = NormalizeAbsoluteUrl(entry.Canonical);
            yield return new PublishUrlSnapshotRoute(
                url,
                true,
                PublishUrlSemanticHasher.Compute(document, entry with { Canonical = url }, model));
        }
    }

    private static PublishUrlSnapshotRoute ResolveDuplicate(IGrouping<string, PublishUrlSnapshotRoute> duplicates)
    {
        var routes = duplicates
            .OrderBy(route => route.SemanticHash, StringComparer.Ordinal)
            .ThenBy(route => route.Indexable)
            .ToArray();
        if (routes.Length > 1 && routes.Any(route => !string.Equals(route.SemanticHash, routes[0].SemanticHash, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Conflicting publish URL snapshot routes share canonical URL '{duplicates.Key}'.");
        }

        return routes[0];
    }

    private static string ResolveSiteUrl(string? configuredSiteUrl, IReadOnlyList<PublishUrlSnapshotRoute> routes)
    {
        if (!string.IsNullOrWhiteSpace(configuredSiteUrl))
        {
            return NormalizeAbsoluteUrl(configuredSiteUrl);
        }

        if (routes.Count == 0)
        {
            return string.Empty;
        }

        var uri = new Uri(routes[0].Url, UriKind.Absolute);
        return uri.GetLeftPart(UriPartial.Authority) + "/";
    }

    internal static string NormalizeAbsoluteUrl(string value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Publish URL must be an absolute HTTP(S) URL: '{value}'.");
        }

        return uri.AbsoluteUri;
    }
}

internal static class PublishUrlSemanticHasher
{
    private static readonly Regex LocalPath = new(
        "(?:file://[^\\s\\\"'<>]+|/(?:Users|private|var|tmp|home)/[^\\s\\\"'<>]+|[A-Za-z]:\\\\[^\\s\\\"'<>]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NotionIdentifier = new(
        @"\b(?:[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}|[0-9a-fA-F]{32})\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> VolatileJsonProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "generatedAt",
        "generated_at",
        "buildTimestamp",
        "buildTime",
        "timestamp",
        "nonce"
    };

    internal static string Compute(ContentDocument? document, SeoIndexEntry entry, SeoModel model)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("body", NormalizeText(ResolveBody(document)));
            writer.WriteString("title", NormalizeText(model.Title));
            writer.WriteString("description", NormalizeText(model.Description));
            writer.WriteString("canonical", PublishUrlSnapshotBuilder.NormalizeAbsoluteUrl(entry.Canonical));
            writer.WriteString("robots", NormalizeText(entry.Robots));
            WriteAuthor(writer, model);
            writer.WritePropertyName("jsonLd");
            writer.WriteStartArray();
            foreach (var jsonLd in (model.JsonLd ?? Array.Empty<string>())
                         .Select(CanonicalizeJson)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                writer.WriteStringValue(jsonLd);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteAuthor(Utf8JsonWriter writer, SeoModel model)
    {
        writer.WritePropertyName("author");
        writer.WriteStartObject();
        var author = model.GeoAuthor;
        writer.WriteString("name", NormalizeText(author?.Name ?? model.Article.Author));
        writer.WriteString("type", NormalizeText(model.Article.AuthorType));
        writer.WriteString("url", NormalizeText(author?.Url));
        writer.WritePropertyName("sameAs");
        writer.WriteStartArray();
        foreach (var value in (author?.SameAs ?? Array.Empty<string>())
                     .Select(NormalizeText)
                     .Where(value => value.Length > 0)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string ResolveBody(ContentDocument? document)
    {
        if (document is null)
        {
            return string.Empty;
        }

        return document.Body.Html
            ?? document.Body.Markdown
            ?? document.Body.PlainText
            ?? document.Record.Presentation.Body
            ?? string.Empty;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return NotionIdentifier.Replace(LocalPath.Replace(value, "[local-path]"), "[notion-id]")
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static string CanonicalizeJson(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteCanonicalJson(writer, document.RootElement);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return NormalizeText(value);
        }
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .Where(property => !VolatileJsonProperties.Contains(property.Name))
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                return;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()
                             .Select(CanonicalizeElement)
                             .OrderBy(item => item, StringComparer.Ordinal))
                {
                    writer.WriteRawValue(item, skipInputValidation: true);
                }

                writer.WriteEndArray();
                return;
            default:
                element.WriteTo(writer);
                return;
        }
    }

    private static string CanonicalizeElement(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalJson(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

internal static class PublishUrlSnapshotJson
{
    internal static string Serialize(PublishUrlSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            Write(writer, snapshot);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static void Write(Utf8JsonWriter writer, PublishUrlSnapshot snapshot)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", snapshot.Schema);
        writer.WriteString("siteUrl", snapshot.SiteUrl);
        writer.WritePropertyName("routes");
        writer.WriteStartArray();
        foreach (var route in snapshot.Routes.OrderBy(route => route.Url, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("url", route.Url);
            writer.WriteBoolean("indexable", route.Indexable);
            writer.WriteString("semanticHash", route.SemanticHash);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

internal static class PublishUrlSnapshotDiff
{
    internal static PublishUrlChangeSet Create(PublishUrlSnapshot baseline, PublishUrlSnapshot current)
    {
        var before = ProjectIndexable(baseline);
        var after = ProjectIndexable(current);
        var urls = before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(url => url, StringComparer.Ordinal);
        var changes = new List<PublishUrlChange>();

        foreach (var url in urls)
        {
            var existsBefore = before.TryGetValue(url, out var oldRoute);
            var existsAfter = after.TryGetValue(url, out var newRoute);
            if (!existsBefore && existsAfter)
            {
                changes.Add(new PublishUrlChange("added", url, newRoute!.SemanticHash));
            }
            else if (existsBefore && !existsAfter)
            {
                changes.Add(new PublishUrlChange("deleted", url, oldRoute!.SemanticHash));
            }
            else if (!string.Equals(oldRoute!.SemanticHash, newRoute!.SemanticHash, StringComparison.Ordinal))
            {
                changes.Add(new PublishUrlChange("updated", url, newRoute.SemanticHash));
            }
        }

        return new PublishUrlChangeSet(changes);
    }

    private static IReadOnlyDictionary<string, PublishUrlSnapshotRoute> ProjectIndexable(PublishUrlSnapshot snapshot)
    {
        return snapshot.Routes
            .Where(route => route.Indexable)
            .OrderBy(route => route.Url, StringComparer.Ordinal)
            .ToDictionary(route => route.Url, route => route, StringComparer.Ordinal);
    }
}
