namespace Bukit.Notion.Push;

public sealed class EnvironmentNotionTokenProvider : INotionTokenProvider
{
    public string? GetToken(string environmentVariable)
        => Environment.GetEnvironmentVariable(environmentVariable);
}
