using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentGraphValidateStageCollectionModeTests
{
    [Fact]
    public async Task ExecuteAsync_NewsStrictOverridesGlobalWarnWithoutUsingArticleType()
    {
        var logger = new RecordingLogger();
        var input = Input("warn", "strict", "off", logger);

        var exception = await Assert.ThrowsAsync<ConfigException>(() =>
            new ContentGraphValidateStage().ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(DiagnosticCode.SchemaStrictModeBlocked, exception.Code);
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_NewsOffSuppressesIssuesLogsAndBlocking()
    {
        var logger = new RecordingLogger();
        var input = Input("strict", "off", "strict", logger);

        var output = await new ContentGraphValidateStage().ExecuteAsync(input, CancellationToken.None);

        Assert.Empty(output.SchemaErrors!);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_NewsWarnOverridesGlobalStrictWithoutBlocking()
    {
        var logger = new RecordingLogger();
        var input = Input("strict", "warn", "strict", logger);

        var output = await new ContentGraphValidateStage().ExecuteAsync(input, CancellationToken.None);

        Assert.NotEmpty(output.SchemaErrors!);
        Assert.NotEmpty(logger.Warnings);
    }

    private static ContentStageInput Input(
        string globalMode,
        string newsMode,
        string articleMode,
        ILogger logger)
    {
        var document = ContentDocument.Create(
            "news-1",
            "News",
            "news-1",
            DateTimeOffset.UnixEpoch,
            string.Empty,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "article",
                ["collection"] = "news",
                ["status"] = "invalid-status"
            }));
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["news"] = new() { Permalink = "/news/{slug}/", SchemaFailMode = newsMode },
                    ["article"] = new() { Permalink = "/articles/{slug}/", SchemaFailMode = articleMode }
                }
            },
            Content = TestContent.Markdown(),
            Build = new BuildConfig { SchemaFailMode = globalMode }
        };
        return new ContentStageInput(
            [document],
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
