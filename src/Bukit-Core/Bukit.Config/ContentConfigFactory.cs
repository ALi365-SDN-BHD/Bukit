namespace Bukit.Config;

public static class ContentConfigFactory
{
    public static ContentConfig FromSources(
        IReadOnlyList<ContentSourceConfig> sources,
        MediaConfig? media = null,
        ContentModelSchemaConfig? modelSchema = null)
        => FromSources(sources, media, modelSchema, routeMetadata: null);

    public static ContentConfig FromSources(
        IReadOnlyList<ContentSourceConfig> sources,
        MediaConfig? media,
        ContentModelSchemaConfig? modelSchema,
        RouteMetadataConfig? routeMetadata)
    {
        return new ContentConfig
        {
            Sources = sources,
            Media = media ?? new MediaConfig(),
            ModelSchema = modelSchema,
            RouteMetadata = routeMetadata
        };
    }

    public static ContentConfig SingleMarkdown(
        string dir = "content",
        string collection = "page",
        IReadOnlyList<string>? includePaths = null,
        MediaConfig? media = null,
        ContentModelSchemaConfig? modelSchema = null)
    {
        return FromSources(
            [
                new ContentSourceConfig
                {
                    Type = "markdown",
                    Name = collection,
                    Collection = collection,
                    Markdown = new MarkdownConfig { Dir = dir, IncludePaths = includePaths }
                }
            ],
            media,
            modelSchema);
    }

    public static ContentConfig SingleNotion(
        string databaseId = "db",
        string name = "page",
        string collection = "page",
        MediaConfig? media = null,
        ContentModelSchemaConfig? modelSchema = null)
    {
        return FromSources(
            [
                new ContentSourceConfig
                {
                    Type = "notion",
                    Name = name,
                    Collection = collection,
                    Notion = new NotionConfig { DatabaseId = databaseId }
                }
            ],
            media,
            modelSchema);
    }
}
