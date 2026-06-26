namespace Bukit.Notion.Client;

public interface INotionClientFactory
{
    INotionClient Create(NotionRequestOptions options);
}
