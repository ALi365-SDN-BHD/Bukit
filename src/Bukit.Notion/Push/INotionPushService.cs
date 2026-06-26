namespace Bukit.Notion.Push;

public interface INotionPushService
{
    NotionPushResult Push(NotionPushOptions options);
}
