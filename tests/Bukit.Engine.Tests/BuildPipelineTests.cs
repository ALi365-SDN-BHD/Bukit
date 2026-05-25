using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_PassesContextToExecutorAndReturnsResult()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test"
            },
            Content = new ContentConfig
            {
                Provider = "markdown"
            }
        };
        var overrides = new ConfigOverrides { Incremental = false };
        var context = new BuildPipelineContext(config, "/tmp/site", overrides);
        BuildPipelineContext? observedContext = null;
        var expected = CreateResult();
        var pipeline = new BuildPipeline((ctx, _) =>
        {
            observedContext = ctx;
            return Task.FromResult(expected);
        });

        var actual = await pipeline.ExecuteAsync(context, CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.Same(context, observedContext);
        Assert.Same(config, observedContext!.Config);
        Assert.Equal("/tmp/site", observedContext.RootDir);
        Assert.Same(overrides, observedContext.Overrides);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var observed = CancellationToken.None;
        var pipeline = new BuildPipeline((_, token) =>
        {
            observed = token;
            return Task.FromResult(CreateResult());
        });

        await pipeline.ExecuteAsync(new BuildPipelineContext(CreateConfig(), "/tmp/site", new ConfigOverrides()), cts.Token);

        Assert.Equal(cts.Token, observed);
    }

    private static AppConfig CreateConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test"
            },
            Content = new ContentConfig
            {
                Provider = "markdown"
            }
        };
    }

    private static BuildResult CreateResult()
    {
        return new BuildResult(
            Version: "test",
            StartedAt: DateTimeOffset.UnixEpoch,
            EndedAt: DateTimeOffset.UnixEpoch,
            DurationMs: 0,
            Environment: new BuildEnvironmentInfo("test", "test", false),
            Project: new BuildProjectInfo("/tmp/site", "dist", "markdown", null, null),
            Summary: new BuildSummary(0, 0, 0, 0, 0, 0, 0, 0),
            Incremental: new BuildIncrementalSummary(false, 0, 0),
            Variants: Array.Empty<BuildVariantSummary>(),
            GeneratedFiles: Array.Empty<string>());
    }
}
