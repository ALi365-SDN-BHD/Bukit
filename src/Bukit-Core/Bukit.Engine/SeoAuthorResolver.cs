using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record ResolvedSeoAuthor(
    string? Name,
    string? SchemaType,
    string? Url,
    IReadOnlyList<string> SameAs,
    bool HasMatchingCanonicalGeoAuthor);

internal static class SeoAuthorResolver
{
    internal static ResolvedSeoAuthor Resolve(
        ContentRecord? record,
        IReadOnlyDictionary<string, ContentField>? fields,
        GeoAuthorModel? geoAuthor)
    {
        var canonicalName = Clean(record?.Ownership.Author)
            ?? SeoModelBuilder.FirstTextField(fields, "author");
        if (canonicalName is not null)
        {
            var declaredType = Clean(record?.Ownership.AuthorType)
                ?? SeoModelBuilder.FirstTextField(fields, "authorType");
            var schemaType = AuthorSchemaType.Resolve(canonicalName, declaredType);
            var matchingGeoAuthor = string.Equals(
                canonicalName,
                Clean(geoAuthor?.Name),
                StringComparison.OrdinalIgnoreCase)
                ? geoAuthor
                : null;

            return new ResolvedSeoAuthor(
                canonicalName,
                schemaType,
                Clean(matchingGeoAuthor?.Url),
                matchingGeoAuthor?.SameAs ?? Array.Empty<string>(),
                matchingGeoAuthor is not null);
        }

        var geoName = Clean(geoAuthor?.Name);
        return geoName is null
            ? new ResolvedSeoAuthor(null, null, null, Array.Empty<string>(), false)
            : new ResolvedSeoAuthor(
                geoName,
                AuthorSchemaType.Person,
                Clean(geoAuthor?.Url),
                geoAuthor?.SameAs ?? Array.Empty<string>(),
                false);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
