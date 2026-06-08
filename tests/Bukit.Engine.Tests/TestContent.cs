using Bukit.Config;

namespace Bukit.Engine.Tests;

internal static class TestContent
{
    internal static ContentConfig Markdown(string dir = "content", string collection = "page")
        => ContentConfigFactory.FromSources([MarkdownSource(dir, collection)]);

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
        => ContentConfigFactory.FromSources([NotionSource(databaseId)]);

    internal static ContentSourceConfig NotionSource(
        string databaseId = "db",
        string name = "page",
        string collection = "page")
        => new()
        {
            Type = "notion",
            Name = name,
            Collection = collection,
            Notion = new NotionConfig { DatabaseId = databaseId }
        };
}
