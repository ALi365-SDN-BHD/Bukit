using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record ResolvedSeoAuthor(
    string Name,
    string SchemaType,
    string? Url,
    string? Image,
    IReadOnlyList<string> SameAs);

internal sealed record ResolvedSeoAuthors(
    IReadOnlyList<ResolvedSeoAuthor> Authors,
    bool SuppressStandaloneGeoAuthor)
{
    internal ResolvedSeoAuthor? Primary => Authors.FirstOrDefault();
    internal bool UsesAuthorRelation { get; init; }
}

internal static class SeoAuthorResolver
{
    internal static ResolvedSeoAuthors Resolve(
        ContentRecord? record,
        IReadOnlyDictionary<string, ContentField>? fields,
        GeoAuthorModel? geoAuthor)
    {
        var projection = record?.Ownership.UsesAuthorRelation is true
            ? new ContentAuthorProjection(true, record.Ownership.AuthorProfiles)
            : ContentAuthorProfileProjectionReader.Read(fields);
        if (projection.UsesAuthorRelation)
        {
            return ResolveProfiles(projection.Profiles);
        }

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

            return schemaType is null
                ? new ResolvedSeoAuthors(Array.Empty<ResolvedSeoAuthor>(), matchingGeoAuthor is not null)
                : new ResolvedSeoAuthors(
                    [
                        new ResolvedSeoAuthor(
                            canonicalName,
                            schemaType,
                            Clean(matchingGeoAuthor?.Url),
                            null,
                            CleanList(matchingGeoAuthor?.SameAs))
                    ],
                    matchingGeoAuthor is not null);
        }

        var geoName = Clean(geoAuthor?.Name);
        return geoName is null
            ? new ResolvedSeoAuthors(Array.Empty<ResolvedSeoAuthor>(), false)
            : new ResolvedSeoAuthors(
                [
                    new ResolvedSeoAuthor(
                        geoName,
                        AuthorSchemaType.Person,
                        Clean(geoAuthor?.Url),
                        null,
                        CleanList(geoAuthor?.SameAs))
                ],
                false);
    }

    private static ResolvedSeoAuthors ResolveProfiles(IReadOnlyList<ContentAuthorProfile> profiles)
    {
        if (profiles.Count == 0)
        {
            throw Block("authoredBy relation must contain at least one resolved author profile.");
        }

        var authors = new List<ResolvedSeoAuthor>(profiles.Count);
        foreach (var profile in profiles)
        {
            var id = Clean(profile.Id);
            var name = Clean(profile.Title);
            var slug = Clean(profile.Slug);
            var declaredType = Clean(profile.Type);
            if (id is null || name is null || slug is null || declaredType is null)
            {
                throw Block($"authoredBy target '{id ?? "<unknown>"}' is not resolved.");
            }

            if (!SeoSchemaValidator.IsSupportedProfileAuthorType(declaredType))
            {
                throw Block(
                    $"authoredBy target '{id}' has invalid author type '{declaredType}'; expected Person or Organization.");
            }

            var schemaType = string.Equals(declaredType, AuthorSchemaType.Organization, StringComparison.OrdinalIgnoreCase)
                ? AuthorSchemaType.Organization
                : AuthorSchemaType.Person;
            authors.Add(new ResolvedSeoAuthor(
                name,
                schemaType,
                $"/authors/{Uri.EscapeDataString(slug)}/",
                Clean(profile.Image),
                CleanList(profile.SameAs)));
        }

        return new ResolvedSeoAuthors(authors, true)
        {
            UsesAuthorRelation = true
        };
    }

    private static InvalidOperationException Block(string message)
        => new($"SEO author profile validation blocked publication: {message}");

    private static IReadOnlyList<string> CleanList(IReadOnlyList<string>? values)
        => values?
            .Select(Clean)
            .Where(static value => value is not null)
            .Cast<string>()
            .ToArray()
            ?? Array.Empty<string>();

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
