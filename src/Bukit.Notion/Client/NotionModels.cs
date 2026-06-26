namespace Bukit.Notion.Client;

public sealed record NotionQueryRequest(string Json);

public sealed record NotionCreatePageRequest(string Json);

public sealed record NotionUpdatePageRequest(string Json);

public sealed record NotionPageResult(string? Id, string Json);

public sealed record NotionQueryResult(IReadOnlyList<string> ResultIds, string Json);

public sealed record NotionBlock(string Json);

public sealed record NotionBlockResult(string? Id, string Json);
