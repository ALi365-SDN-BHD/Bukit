using System.Reflection;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ContentConfigFactoryCompatibilityTests
{
    [Fact]
    public void FromSources_PublicThreeParameterOverload_RemainsAvailable()
    {
        var overload = typeof(ContentConfigFactory).GetMethod(
            nameof(ContentConfigFactory.FromSources),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(IReadOnlyList<ContentSourceConfig>),
                typeof(MediaConfig),
                typeof(ContentModelSchemaConfig)
            ],
            modifiers: null);

        Assert.NotNull(overload);
    }

    [Fact]
    public void SingleMarkdown_PreservesSourceAndMetadataBehavior()
    {
        var media = new MediaConfig { DownloadToLocal = true };
        var modelSchema = new ContentModelSchemaConfig { RequireSummary = true };

        var content = ContentConfigFactory.SingleMarkdown(
            dir: "articles",
            collection: "posts",
            includePaths: ["published"],
            media: media,
            modelSchema: modelSchema);

        var source = Assert.Single(content.Sources!);
        Assert.Equal("markdown", source.Type);
        Assert.Equal("posts", source.Name);
        Assert.Equal("posts", source.Collection);
        Assert.Equal("articles", source.Markdown!.Dir);
        Assert.Equal(["published"], source.Markdown.IncludePaths);
        Assert.Same(media, content.Media);
        Assert.Same(modelSchema, content.ModelSchema);
        Assert.Null(content.RouteMetadata);
    }

    [Fact]
    public void SingleNotion_PreservesSourceAndMetadataBehavior()
    {
        var media = new MediaConfig { DownloadToLocal = true };
        var modelSchema = new ContentModelSchemaConfig { RequireSummary = true };

        var content = ContentConfigFactory.SingleNotion(
            databaseId: "database-id",
            name: "articles",
            collection: "posts",
            media: media,
            modelSchema: modelSchema);

        var source = Assert.Single(content.Sources!);
        Assert.Equal("notion", source.Type);
        Assert.Equal("articles", source.Name);
        Assert.Equal("posts", source.Collection);
        Assert.Equal("database-id", source.Notion!.DatabaseId);
        Assert.Same(media, content.Media);
        Assert.Same(modelSchema, content.ModelSchema);
        Assert.Null(content.RouteMetadata);
    }
}
