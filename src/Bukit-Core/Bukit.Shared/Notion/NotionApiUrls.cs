namespace Bukit.Shared.Notion;

public static class NotionApiUrls
{
    public const string Base = Bukit.Notion.NotionApiUrls.Base;
    public const string ApiVersion = Bukit.Notion.NotionApiUrls.ApiVersion;
    public const string NotionVersion = Bukit.Notion.NotionApiUrls.NotionVersion;
    public const int DefaultPageSize = Bukit.Notion.NotionApiUrls.DefaultPageSize;

    public static string Pages(string pageId) => Bukit.Notion.NotionApiUrls.Pages(pageId);
    public static string DatabaseQuery(string databaseId) => Bukit.Notion.NotionApiUrls.DatabaseQuery(databaseId);
    public static string Database(string databaseId) => Bukit.Notion.NotionApiUrls.Database(databaseId);
    public static string BlockChildren(string blockId, int pageSize = DefaultPageSize)
        => Bukit.Notion.NotionApiUrls.BlockChildren(blockId, pageSize);
}
