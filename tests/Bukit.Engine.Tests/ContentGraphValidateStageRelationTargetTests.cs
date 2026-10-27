using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

[Trait("Category", "Contract")]
public sealed class ContentGraphValidateStageRelationTargetTests
{
    [Fact]
    public async Task ExecuteAsync_UnresolvedRelationTarget_WarnModeCollectsIssue()
    {
        var logger = new RecordingLogger();
        var input = Input([Post("post-1", "missing-post")], "warn", logger);

        var output = await new ContentGraphValidateStage().ExecuteAsync(input, CancellationToken.None);

        Assert.Contains(output.SchemaErrors!, error => error.Code == "canonical_relation_target_unresolved");
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedRelationTarget_StrictModeThrows()
    {
        var input = Input([Post("post-1", "missing-post")], "strict", new RecordingLogger());

        await Assert.ThrowsAsync<ConfigException>(() =>
            new ContentGraphValidateStage().ExecuteAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateContentIds_WarnModeCollectsIssue()
    {
        var logger = new RecordingLogger();
        var input = Input([Post("duplicate"), Post("duplicate")], "warn", logger);

        var output = await new ContentGraphValidateStage().ExecuteAsync(input, CancellationToken.None);

        Assert.Contains(output.SchemaErrors!, error => error.Code == "canonical_duplicate_id");
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateContentIds_StrictModeThrows()
    {
        var input = Input([Post("duplicate"), Post("duplicate")], "strict", new RecordingLogger());

        await Assert.ThrowsAsync<ConfigException>(() =>
            new ContentGraphValidateStage().ExecuteAsync(input, CancellationToken.None));
    }

    private static ContentDocument Post(string id, string? relatedTo = null)
    {
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "news"
        };
        if (relatedTo is not null)
        {
            fields["related_to"] = new[] { relatedTo };
        }

        return ContentDocument.Create(
            id,
            "Post",
            id,
            DateTimeOffset.UnixEpoch,
            string.Empty,
            ContentFieldReader.ToFieldMap(fields));
    }

    private static ContentStageInput Input(
        ContentDocument[] documents,
        string mode,
        ILogger logger)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["news"] = new() { Permalink = "/news/{slug}/", SchemaFailMode = mode }
                }
            },
            Content = TestContent.Markdown(),
            Build = new BuildConfig { SchemaFailMode = mode }
        };
        return new ContentStageInput(
            documents,
            EmptyContentBodyStore.Instance,
            config,
            new ConfigOverrides(),
            "/root",
            "/cache",
            logger);
    }

    private sealed class RecordingLogger : ILogger
    {
        internal List<string> Warnings { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }
}
