using System.Text.Json;
using Bukit.Config;
using Bukit.Content.Notion.Snapshot;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class NotionPinTests
{
    private static NotionPin Pin(string json)
    {
        using var document = JsonDocument.Parse(json);
        return NotionPin.Parse(document.RootElement);
    }

    private static AppConfig Config(params ContentSourceConfig[] sources)
        => new()
        {
            Site = new SiteConfig { Name = "s", Title = "S" },
            Content = new ContentConfig { Sources = sources }
        };

    private static ContentSourceConfig Notion(string? name, NotionConfig notion)
        => new() { Type = "notion", Name = name, Notion = notion };

    [Fact]
    public void Apply_ReplacesThePinnedKeysAndReportsWhatDiffered()
    {
        var unreviewed = new NotionConfig
        {
            DatabaseId = "db-ai",
            FieldPolicy = new NotionFieldPolicyConfig { Mode = "all" },
            FilterType = "none",
            PropertyMap = new NotionPropertyMapConfig { Title = "Secret" },
            MaxItems = 500,
            RenderContent = false
        };
        var pin = Pin("""
            {"format":1,"sources":{"posts":{
              "databaseId":"db-ai",
              "fieldPolicy":{"mode":"whitelist","allowed":[" Tags ",""]},
              "propertyMap":{"Title":"Name","PublishAt":"Date"},
              "filterProperty":"Published"
            }}}
            """);

        var (config, changed) = pin.Apply(Config(Notion("posts", unreviewed)));

        var notion = config.Content.Sources![0].Notion!;
        Assert.Equal("whitelist", notion.FieldPolicy.Mode);
        Assert.Equal(["Tags"], notion.FieldPolicy.Allowed);
        Assert.Equal(new NotionPropertyMapConfig { Title = "Name", PublishAt = "Date" }, notion.PropertyMap);
        // Omitted keys take Bukit's defaults, not the unreviewed values.
        Assert.Equal("checkbox_true", notion.FilterType);
        Assert.Null(notion.MaxItems);
        Assert.Equal("db-ai", notion.DatabaseId);
        Assert.Null(notion.RenderContent);
        var (source, keys) = Assert.Single(changed);
        Assert.Equal("posts", source);
        Assert.Equal(["fieldPolicy", "propertyMap", "filterType", "renderContent", "maxItems"], keys);
    }

    [Fact]
    public void Apply_KeysUnnamedSourcesLikeTheBuild()
    {
        var pin = Pin("""{"format":1,"sources":{"notion1":{"databaseId":"a","filterType":"none"},"notion2":{"databaseId":"b"}}}""");
        var markdown = new ContentSourceConfig { Type = "markdown" };

        var (config, changed) = pin.Apply(Config(
            Notion(null, new NotionConfig { DatabaseId = "a" }),
            markdown,
            Notion(" ", new NotionConfig { DatabaseId = "b", FilterType = "none" })));

        Assert.Equal("none", config.Content.Sources![0].Notion!.FilterType);
        Assert.Equal("checkbox_true", config.Content.Sources[2].Notion!.FilterType);
        Assert.Same(markdown, config.Content.Sources[1]);
        Assert.Equal([("notion1", (IReadOnlyList<string>)["filterType"]), ("notion2", ["filterType"])],
            changed.Select(c => (c.Source, c.Keys)).ToArray(), new PinChangeComparer());
    }

    [Fact]
    public void Apply_SourceMissingFromThePin_Fails()
    {
        var error = Assert.Throws<NotionSnapshotException>(() => Pin("""{"format":1,"sources":{}}""")
            .Apply(Config(Notion("posts", new NotionConfig { DatabaseId = "a" }))));

        Assert.Equal(DiagnosticCode.NotionPinSourceMissing, error.Code);
        Assert.Contains("'posts'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_SourceWithAnotherDatabase_Fails()
    {
        // Swapping source names must not give one database another source's field policy.
        var pin = Pin("""{"format":1,"sources":{"blog":{"databaseId":"A","fieldPolicy":{"mode":"all"}},"internal":{"databaseId":"B"}}}""");

        var error = Assert.Throws<NotionSnapshotException>(() => pin.Apply(Config(
            Notion("blog", new NotionConfig { DatabaseId = "B" }),
            Notion("internal", new NotionConfig { DatabaseId = "A" }))));

        Assert.Equal(DiagnosticCode.NotionPinSourceMissing, error.Code);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{"sources":{}}""")]
    [InlineData("""{"format":"1","sources":{}}""")]
    [InlineData("""{"format":1}""")]
    [InlineData("""{"format":1,"sources":{"a":[]}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"filterType":"none"}}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"databaseId":"x","filterType":1}}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"databaseId":"x","filtertype":"none"}}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"databaseId":"x","fieldPolicy":{"Mode":"all"}}}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"databaseId":"x","renderContent":"yes"}}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"databaseId":"x","includeSlugs":"x"}}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"databaseId":"x","includeSlugs":[1]}}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"databaseId":"x","maxItems":1.5}}}""")]
    [InlineData("""{"format":1,"sources":{"a":{"databaseId":"x","propertyMap":{"title":"x"}}}}""")]
    public void Parse_RejectsMalformedPins(string json)
    {
        var error = Assert.Throws<NotionSnapshotException>(() => Pin(json));

        Assert.Equal(DiagnosticCode.NotionPinInvalid, error.Code);
    }

    private sealed class PinChangeComparer : IEqualityComparer<(string, IReadOnlyList<string>)>
    {
        public bool Equals((string, IReadOnlyList<string>) x, (string, IReadOnlyList<string>) y)
            => x.Item1 == y.Item1 && x.Item2.SequenceEqual(y.Item2);

        public int GetHashCode((string, IReadOnlyList<string>) obj) => obj.Item1.GetHashCode(StringComparison.Ordinal);
    }
}
