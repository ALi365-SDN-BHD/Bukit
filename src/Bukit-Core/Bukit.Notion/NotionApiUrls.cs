namespace Bukit.Notion;

public static class NotionApiUrls
{
    public const string Base = "https://api.notion.com";
    public const string ApiVersion = "v1";
    public const string NotionVersion = "2022-06-28";
    public const int DefaultPageSize = 100;

    public static string Databases() => $"{Base}/{ApiVersion}/databases";
    public static string Pages() => $"{Base}/{ApiVersion}/pages";
    public static string Pages(string pageId) => $"{Base}/{ApiVersion}/pages/{pageId}";
    public static string UsersMe() => $"{Base}/{ApiVersion}/users/me";
    public static string DatabaseQuery(string databaseId) => $"{Base}/{ApiVersion}/databases/{databaseId}/query";
    public static string Database(string databaseId) => $"{Base}/{ApiVersion}/databases/{databaseId}";
    public static string Blocks(string blockId) => $"{Base}/{ApiVersion}/blocks/{blockId}";
    public static string BlockChildrenEndpoint(string blockId) => $"{Blocks(blockId)}/children";
    public static string BlockChildren(string blockId, int pageSize = DefaultPageSize) => $"{Base}/{ApiVersion}/blocks/{blockId}/children?page_size={pageSize}";
}
