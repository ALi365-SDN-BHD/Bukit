using Bukit.Config;
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
    internal ResolvedSeoAuthor? Primary => Authors.Count == 0 ? null : Authors[0];
    internal string? TextByline { get; init; }
    internal bool UsesAuthorRelation { get; init; }
}

internal static class SeoAuthorResolver
{
    internal static ResolvedSeoAuthors Resolve(
        ContentRecord? record,
        IReadOnlyDictionary<string, ContentField>? fields,
        GeoAuthorModel? geoAuthor,
        SeoConfig? seo = null)
    {
        var projection = record?.Ownership.UsesAuthorRelation is true
            ? new ContentAuthorProjection(true, record.Ownership.AuthorProfiles)
            : ContentAuthorProfileProjectionReader.Read(fields);
        if (projection.UsesAuthorRelation)
        {
            return ResolveProfiles(projection.Profiles, seo);
        }

        var canonicalName = Clean(record?.Ownership.Author)
            ?? SeoModelBuilder.FirstTextField(fields, "author");
        if (canonicalName is not null)
        {
            return ResolveTextAuthor(canonicalName, record, fields, geoAuthor, seo);
        }

        var geoName = Clean(geoAuthor?.Name);
        if (geoName is null)
        {
            return DefaultAuthor(seo);
        }

        var geoProfile = ConfiguredProfile(seo, geoName);
        return new ResolvedSeoAuthors(
            [
                new ResolvedSeoAuthor(
                    geoName,
                    AuthorSchemaType.Normalize(geoProfile?.Type) ?? AuthorSchemaType.Person,
                    Clean(geoAuthor?.Url) ?? Clean(geoProfile?.Url),
                    Clean(geoProfile?.Image),
                    SameAsOrConfigured(geoAuthor?.SameAs, geoProfile))
            ],
            false);
    }

    private static ResolvedSeoAuthors ResolveTextAuthor(
        string canonicalName,
        ContentRecord? record,
        IReadOnlyDictionary<string, ContentField>? fields,
        GeoAuthorModel? geoAuthor,
        SeoConfig? seo)
    {
        var configured = ConfiguredProfile(seo, canonicalName);
        var declaredType = Clean(record?.Ownership.AuthorType)
            ?? SeoModelBuilder.FirstTextField(fields, "authorType")
            ?? Clean(configured?.Type);
        var schemaType = AuthorSchemaType.Resolve(canonicalName, declaredType);
        var matchingGeoAuthor = string.Equals(
            canonicalName,
            Clean(geoAuthor?.Name),
            StringComparison.OrdinalIgnoreCase)
            ? geoAuthor
            : null;

        return schemaType is null
            ? new ResolvedSeoAuthors(Array.Empty<ResolvedSeoAuthor>(), matchingGeoAuthor is not null)
            {
                TextByline = canonicalName
            }
            : new ResolvedSeoAuthors(
                [
                    new ResolvedSeoAuthor(
                        canonicalName,
                        schemaType,
                        Clean(matchingGeoAuthor?.Url) ?? Clean(configured?.Url),
                        Clean(configured?.Image),
                        SameAsOrConfigured(matchingGeoAuthor?.SameAs, configured))
                ],
                matchingGeoAuthor is not null);
    }

    private static ResolvedSeoAuthors ResolveProfiles(IReadOnlyList<ContentAuthorProfile> profiles, SeoConfig? seo)
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
            var configured = ConfiguredProfile(seo, id);
            if (id is not null && configured is not null && (name is null || slug is null || declaredType is null))
            {
                // An authoredBy id that no relation resolver expanded (a Markdown front-matter id)
                // takes its whole profile from site.seo.authors.
                authors.Add(new ResolvedSeoAuthor(
                    Clean(configured.Name) ?? id,
                    AuthorSchemaType.Normalize(configured.Type) ?? AuthorSchemaType.Person,
                    Clean(configured.Url),
                    Clean(configured.Image),
                    CleanList(configured.SameAs)));
                continue;
            }

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
                Clean(configured?.Url) ?? $"/authors/{Uri.EscapeDataString(slug)}/",
                Clean(profile.Image) ?? Clean(configured?.Image),
                SameAsOrConfigured(profile.SameAs, configured)));
        }

        return new ResolvedSeoAuthors(authors, true)
        {
            UsesAuthorRelation = true
        };
    }

    // site.seo.defaultAuthor is the last fallback, used only when content, author
    // relations and GEO metadata name no author.
    private static ResolvedSeoAuthors DefaultAuthor(SeoConfig? seo)
    {
        var name = Clean(seo?.DefaultAuthor);
        if (name is null)
        {
            return new ResolvedSeoAuthors(Array.Empty<ResolvedSeoAuthor>(), false);
        }

        var configured = ConfiguredProfile(seo, name);
        var schemaType = AuthorSchemaType.Normalize(seo!.DefaultAuthorType)
            ?? AuthorSchemaType.Normalize(configured?.Type)
            ?? AuthorSchemaType.Person;
        return new ResolvedSeoAuthors(
            [new ResolvedSeoAuthor(name, schemaType, Clean(configured?.Url), Clean(configured?.Image), CleanList(configured?.SameAs))],
            false);
    }

    // site.seo.authors entry for an author name or authoredBy id; keys match case-insensitively.
    private static SeoAuthorProfileConfig? ConfiguredProfile(SeoConfig? seo, string? key)
        => key is not null && seo?.Authors is { } authors && authors.TryGetValue(key, out var profile) ? profile : null;

    private static IReadOnlyList<string> SameAsOrConfigured(IReadOnlyList<string>? sameAs, SeoAuthorProfileConfig? configured)
        => CleanList(sameAs) is { Count: > 0 } own ? own : CleanList(configured?.SameAs);

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
