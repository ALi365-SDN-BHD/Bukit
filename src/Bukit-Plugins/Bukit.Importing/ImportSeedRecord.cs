namespace Bukit.Importing;

public sealed record ImportSeedRecord(
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
