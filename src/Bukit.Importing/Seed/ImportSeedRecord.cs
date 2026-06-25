namespace Bukit.Importing.Seed;

internal sealed record ImportSeedRecord(
    string Collection,
    string Title,
    string Slug,
    string? Summary,
    string? Content,
    string? Language,
    bool Published,
    string? SeoTitle,
    string? SeoDescription,
    IReadOnlyDictionary<string, object?>? ExtraFields = null);
