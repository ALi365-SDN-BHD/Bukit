namespace Bukit.Notion.Mapping;

public sealed record NotionPropertyMapping(
    string Name,
    string? Source,
    string? Type);
