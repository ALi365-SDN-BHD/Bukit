namespace Bukit.Notion.Push;

public interface INotionTokenProvider
{
    string? GetToken(string environmentVariable);
}
