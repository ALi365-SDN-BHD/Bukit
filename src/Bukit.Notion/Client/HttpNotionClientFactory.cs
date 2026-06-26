namespace Bukit.Notion.Client;

public sealed class HttpNotionClientFactory : INotionClientFactory
{
    public INotionClient Create(NotionRequestOptions options)
        => new NotionHttpClient(new HttpClient(), options);
}
