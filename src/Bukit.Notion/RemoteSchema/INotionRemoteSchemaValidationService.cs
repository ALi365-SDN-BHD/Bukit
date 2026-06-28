namespace Bukit.Notion.RemoteSchema;

public interface INotionRemoteSchemaValidationService
{
    NotionRemoteSchemaValidationResult Validate(NotionRemoteSchemaOptions options);
}
