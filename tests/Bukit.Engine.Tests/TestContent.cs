using Bukit.Config;

namespace Bukit.Engine.Tests;

internal static class TestContent
{
    internal static ContentConfig Markdown(string dir = "content", string collection = "page")
        => new()
        {
            Provider = "sources",
            Sources = new[]
            {
                MarkdownSource(dir, collection)
            }
        };

    internal static ContentSourceConfig MarkdownSource(
        string dir = "content",
        string collection = "page",
        IReadOnlyList<string>? includePaths = null)
        => new()
        {
            Type = "markdown",
            Name = collection,
            Collection = collection,
            Markdown = new MarkdownConfig { Dir = dir, IncludePaths = includePaths }
        };

    internal static ContentConfig Notion(string databaseId = "db")
        => new()
        {
            Provider = "sources",
            Sources = new[]
            {
                new ContentSourceConfig
                {
                    Type = "notion",
                    Name = "page",
                    Collection = "page",
                    Notion = new NotionConfig { DatabaseId = databaseId }
                }
            }
        };
}
