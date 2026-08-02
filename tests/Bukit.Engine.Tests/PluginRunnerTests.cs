using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginRunnerTests
{
    [Fact]
    public void CollectHtmlTransforms_UsesRegistryOrderHookAndCreatesFreshTransformPerVariant()
    {
        var (context, config) = CreateContext(analytics: Analytics(Provider("google-analytics", measurementId: "G-ORDER")));

        var first = PluginRunner.CollectHtmlTransforms(context, config, BuildExecutionMode.Production);
        var second = PluginRunner.CollectHtmlTransforms(context, config, BuildExecutionMode.Production);

        var firstAnalytics = Assert.Single(first, x => x.Name == "analytics");
        var secondAnalytics = Assert.Single(second, x => x.Name == "analytics");
        Assert.NotSame(firstAnalytics, secondAnalytics);
        Assert.Equal(first.Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase), first.Select(x => x.Name));
    }

    [Fact]
    public void CollectHtmlTransforms_WhenGenericPluginIsDisabled_CreatesNoTransformOrExecutionRecord()
    {
        var plugin = new TestHtmlTransformPlugin("generic", order: 1, () => new AppendingTransform("generic"));
        var (context, config) = CreateContext(plugins: new Dictionary<string, PluginToggleConfig>
        {
            ["generic"] = new() { Enabled = false }
        });

        var transforms = PluginRunner.CollectHtmlTransforms(
            context,
            BuildExecutionMode.Production,
            PluginExecutionPolicy.From(config.Site),
            [plugin]);
        transforms.RecordExecutions();

        Assert.Empty(transforms);
        Assert.Equal(0, plugin.CreateCount);
        Assert.DoesNotContain(context.PluginExecutions, x => x.Name == "generic");
    }

    [Fact]
    public void CollectHtmlTransforms_WhenAnalyticsPluginIsDisabled_CreatesNoTransformOrExecutionRecord()
    {
        var (context, config) = CreateContext(
            plugins: new Dictionary<string, PluginToggleConfig>
            {
                ["analytics"] = new() { Enabled = false }
            },
            analytics: Analytics(Provider("google-analytics", measurementId: "G-DISABLED")));

        var transforms = PluginRunner.CollectHtmlTransforms(context, config, BuildExecutionMode.Production);
        transforms.RecordExecutions();

        Assert.DoesNotContain(transforms, x => x.Name == "analytics");
        Assert.DoesNotContain(context.PluginExecutions, x => x.Name == "analytics");
    }

    [Fact]
    public void CollectHtmlTransforms_ExcludesPluginWhoseHookFilterRejectsHtmlTransform()
    {
        var plugin = new RejectingHtmlTransformPlugin();
        var (context, config) = CreateContext();

        var transforms = PluginRunner.CollectHtmlTransforms(
            context,
            BuildExecutionMode.Production,
            PluginExecutionPolicy.From(config.Site),
            [plugin]);
        transforms.RecordExecutions();

        Assert.Empty(transforms);
        Assert.False(plugin.Created);
        Assert.Empty(context.PluginExecutions);
    }

    [Fact]
    public void CollectHtmlTransforms_WhenAnalyticsFeatureIsDisabled_StillCreatesTransformWithoutInjection()
    {
        var (context, config) = CreateContext(analytics: Analytics(
            [Provider("google-analytics", measurementId: "G-OFF")],
            enabled: false));

        var transforms = PluginRunner.CollectHtmlTransforms(context, config, BuildExecutionMode.Production);
        var analytics = Assert.Single(transforms, x => x.Name == "analytics");
        const string html = "<html><head></head><body></body></html>";

        Assert.Equal(html, analytics.Transform(HtmlContext(), html));
    }

    [Fact]
    public void CollectedHtmlTransforms_RecordZeroPageExecutionExactlyOnce()
    {
        var (context, config) = CreateContext(analytics: Analytics());
        var transforms = PluginRunner.CollectHtmlTransforms(context, config, BuildExecutionMode.Production);

        Parallel.Invoke(transforms.RecordExecutions, transforms.RecordExecutions, transforms.RecordExecutions);

        var execution = Assert.Single(context.PluginExecutions, x => x.Name == "analytics" && x.Hook == "html-transform");
        Assert.Equal(0, execution.DurationMs);
        Assert.True(execution.Success);
        Assert.Null(execution.Error);
    }

    [Fact]
    public void CollectedHtmlTransforms_EveryConcurrentCallerReturnsAfterRecordIsVisible()
    {
        var (context, config) = CreateContext(analytics: Analytics());
        var transforms = PluginRunner.CollectHtmlTransforms(context, config, BuildExecutionMode.Production);

        Parallel.For(0, 100, _ =>
        {
            transforms.RecordExecutions();
            Assert.Single(context.PluginExecutions, x => x.Name == "analytics" && x.Hook == "html-transform");
        });

        Assert.Single(context.PluginExecutions);
    }

    [Fact]
    public void TrackedHtmlTransform_StrictRecordsFirstErrorAndRethrows()
    {
        var plugin = new TestHtmlTransformPlugin("throwing", 1, () => new ThrowingTransform("strict boom"));
        var (context, config) = CreateContext(pluginFailMode: "strict");
        var transforms = PluginRunner.CollectHtmlTransforms(
            context,
            BuildExecutionMode.Production,
            PluginExecutionPolicy.From(config.Site),
            [plugin]);
        var transform = Assert.IsType<TrackedHtmlTransform>(Assert.Single(transforms));

        var exception = Assert.Throws<InvalidOperationException>(() => transform.Transform(HtmlContext(), "before"));
        transforms.RecordExecutions();

        Assert.Equal("strict boom", exception.Message);
        Assert.Equal(1, transform.InvocationCount);
        var execution = Assert.Single(context.PluginExecutions);
        Assert.False(execution.Success);
        Assert.Equal("strict boom", execution.Error);
    }

    [Fact]
    public void TrackedHtmlTransform_WarnReturnsInputContinuesAndAggregatesWarning()
    {
        var logger = new RecordingLogger();
        var throwing = new TestHtmlTransformPlugin("a-throwing", 1, () => new ThrowingTransform("warn boom"));
        var appending = new TestHtmlTransformPlugin("b-appending", 2, () => new AppendingTransform("after"));
        var (context, config) = CreateContext(pluginFailMode: "warn", logger: logger);
        var transforms = PluginRunner.CollectHtmlTransforms(
            context,
            BuildExecutionMode.Production,
            PluginExecutionPolicy.From(config.Site),
            [appending, throwing]);

        var html = "before";
        foreach (var transform in transforms)
        {
            html = transform.Transform(HtmlContext(logger: logger), html);
        }
        _ = transforms[0].Transform(HtmlContext(logger: logger), "second");
        transforms.RecordExecutions();

        Assert.Equal("before|after", html);
        Assert.Single(logger.Warnings, x => x.Contains("a-throwing", StringComparison.Ordinal));
        Assert.Collection(
            context.PluginExecutions,
            first =>
            {
                Assert.Equal("a-throwing", first.Name);
                Assert.False(first.Success);
                Assert.Equal("warn boom", first.Error);
            },
            second =>
            {
                Assert.Equal("b-appending", second.Name);
                Assert.True(second.Success);
            });
    }

    [Fact]
    public void TrackedHtmlTransform_ParallelCallsHaveRaceFreeCountErrorAndSingleRecord()
    {
        var logger = new RecordingLogger();
        var plugin = new TestHtmlTransformPlugin("parallel", 1, () => new SometimesThrowingTransform());
        var (context, config) = CreateContext(pluginFailMode: "warn", logger: logger);
        var transforms = PluginRunner.CollectHtmlTransforms(
            context,
            BuildExecutionMode.Production,
            PluginExecutionPolicy.From(config.Site),
            [plugin]);
        var transform = Assert.IsType<TrackedHtmlTransform>(Assert.Single(transforms));

        Parallel.For(0, 500, i =>
        {
            var input = i.ToString();
            var output = transform.Transform(HtmlContext(logger: logger), input);
            Assert.True(output == input || output == input + "|ok");
        });
        Parallel.Invoke(transforms.RecordExecutions, transforms.RecordExecutions);

        Assert.Equal(500, transform.InvocationCount);
        Assert.True(transform.ElapsedTimestampTicks >= 0);
        var execution = Assert.Single(context.PluginExecutions);
        Assert.False(execution.Success);
        Assert.NotNull(execution.Error);
        Assert.Single(logger.Warnings);
    }

    [Fact]
    public async Task RunDerivePages_AlreadyCancelled_ThrowsBeforePluginExecution()
    {
        var plugin = new BlockingDerivePlugin();
        var (context, config) = CreateContext(pluginFailMode: "warn");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PluginRunner.RunDerivePagesAsync(
                context,
                PluginExecutionPolicy.From(config.Site),
                [((IBukitPlugin)plugin, "test")],
                cts.Token));

        Assert.DoesNotContain(context.PluginExecutions, e => e.Name == "blocking-derive");
    }

    [Fact]
    public async Task RunAfterBuild_AlreadyCancelled_ThrowsBeforePluginExecution()
    {
        var plugin = new BlockingAfterBuildPlugin();
        var (context, config) = CreateContext(pluginFailMode: "warn");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PluginRunner.RunAfterBuildAsync(
                context,
                PluginExecutionPolicy.From(config.Site),
                [((IBukitPlugin)plugin, "test")],
                cts.Token));

        Assert.DoesNotContain(context.PluginExecutions, e => e.Name == "blocking-after");
    }

    [Fact]
    public async Task RunDerivePages_RecordsPluginExecutionInfo()
    {
        var (ctx, config) = CreateContext(plugins: DisableAfterBuildPlugins());

        await PluginRunner.RunDerivePagesAsync(ctx, config);

        Assert.NotEmpty(ctx.PluginExecutions);
        var deriveExecs = ctx.PluginExecutions.Where(e => e.Hook == "derive-pages").ToList();
        Assert.NotEmpty(deriveExecs);
        Assert.All(deriveExecs, e =>
        {
            Assert.NotNull(e.Name);
            Assert.Equal("derive-pages", e.Hook);
            Assert.True(e.DurationMs >= 0);
        });
    }

    [Fact]
    public async Task RunAfterBuild_RecordsPluginExecutionInfo()
    {
        var (ctx, config) = CreateContext(root: CreateTempRoot(), siteUrl: "https://example.com", plugins: DisableDerivePlugins());

        await PluginRunner.RunAfterBuildAsync(ctx, config);

        Assert.NotEmpty(ctx.PluginExecutions);
        var afterBuildExecs = ctx.PluginExecutions.Where(e => e.Hook == "after-build").ToList();
        Assert.NotEmpty(afterBuildExecs);
        Assert.All(afterBuildExecs, e =>
        {
            Assert.NotNull(e.Name);
            Assert.Equal("after-build", e.Hook);
            Assert.True(e.DurationMs >= 0);
        });
    }

    [Fact]
    public async Task RunDerivePages_WarnMode_PropagatesCallerCancellation()
    {
        var plugin = new BlockingDerivePlugin();
        var (context, config) = CreateContext(pluginFailMode: "warn");
        using var cancellation = new CancellationTokenSource();
        var runTask = PluginRunner.RunDerivePagesAsync(
            context,
            PluginExecutionPolicy.From(config.Site),
            [((IBukitPlugin)plugin, "test")],
            cancellation.Token);
        await plugin.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runTask.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task RunAfterBuild_WarnMode_PropagatesCallerCancellation()
    {
        var plugin = new BlockingAfterBuildPlugin();
        var (context, config) = CreateContext(pluginFailMode: "warn");
        using var cancellation = new CancellationTokenSource();
        var runTask = PluginRunner.RunAfterBuildAsync(
            context,
            PluginExecutionPolicy.From(config.Site),
            [((IBukitPlugin)plugin, "test")],
            cancellation.Token);
        await plugin.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runTask.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Plugins_OrderedByOrderThenNameThenVersion()
    {
        var (ctx, config) = CreateContext(plugins: DisableAfterBuildPlugins());

        await PluginRunner.RunDerivePagesAsync(ctx, config);

        var names = ctx.PluginExecutions.Where(e => e.Hook == "derive-pages").Select(e => e.Name!).ToList();
        var sorted = names.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public async Task PluginDisabledViaConfig_Skipped()
    {
        var plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["pages-index"] = new PluginToggleConfig { Enabled = false },
            ["taxonomy"] = new PluginToggleConfig { Enabled = false },
            ["sitemap"] = new PluginToggleConfig { Enabled = false },
            ["feed"] = new PluginToggleConfig { Enabled = false },
            ["search-index"] = new PluginToggleConfig { Enabled = false },
            ["pagination"] = new PluginToggleConfig { Enabled = false },
            ["archive"] = new PluginToggleConfig { Enabled = false }
        };
        var (ctx, config) = CreateContext(plugins: plugins);

        await PluginRunner.RunDerivePagesAsync(ctx, config);
        await PluginRunner.RunAfterBuildAsync(ctx, config);

        Assert.DoesNotContain(ctx.PluginExecutions, e => e.Name == "pages-index");
        Assert.DoesNotContain(ctx.PluginExecutions, e => e.Name == "sitemap");
    }

    [Fact]
    public void GetAllPlugins_SameSession_UsesSingleRegistryBuild()
    {
        PluginRegistry.ResetBuildCountForTests();
        var (ctx, config) = CreateContext();
        var session = PluginExecutionSession.Create(
            config,
            BuildExecutionMode.Production);

        _ = PluginRegistry.GetAllPlugins(ctx, session).ToList();
        var first = PluginRegistry.RegistrationBuildCountForTests;
        _ = PluginRegistry.GetAllPlugins(ctx, session).ToList();
        var second = PluginRegistry.RegistrationBuildCountForTests;

        Assert.Equal(first, second);
    }

    private static (BuildContext Context, AppConfig Config) CreateContext(
        string? root = null,
        string? siteUrl = null,
        string pluginFailMode = "strict",
        IReadOnlyDictionary<string, PluginToggleConfig>? plugins = null,
        AnalyticsConfig? analytics = null,
        ILogger? logger = null)
    {
        root ??= CreateTempRoot();
        var outputDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outputDir);

        var site = new SiteConfig
        {
            Name = "t",
            Title = "t",
            Url = siteUrl ?? "",
            PluginFailMode = pluginFailMode,
            Plugins = plugins,
            Analytics = analytics ?? new AnalyticsConfig()
        };

        var config = new AppConfig
        {
            Site = site,
            Content = TestContent.Markdown()
        };
        var context = new BuildContext
        {
            RootDir = root,
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = Path.Combine(root, "layouts"),
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = logger ?? new ConsoleLogger(LogLevel.Error)
        };
        return (context, config);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static IReadOnlyDictionary<string, PluginToggleConfig> DisableAfterBuildPlugins()
    {
        return new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["sitemap"] = new PluginToggleConfig { Enabled = false },
            ["feed"] = new PluginToggleConfig { Enabled = false },
            ["search-index"] = new PluginToggleConfig { Enabled = false },
            ["taxonomy"] = new PluginToggleConfig { Enabled = false }
        };
    }

    private static IReadOnlyDictionary<string, PluginToggleConfig> DisableDerivePlugins()
    {
        return new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["pages-index"] = new PluginToggleConfig { Enabled = false },
            ["taxonomy"] = new PluginToggleConfig { Enabled = false },
            ["pagination"] = new PluginToggleConfig { Enabled = false },
            ["archive"] = new PluginToggleConfig { Enabled = false }
        };
    }

    private static AnalyticsConfig Analytics(params AnalyticsProviderConfig[] providers)
        => Analytics(providers, enabled: true);

    private static AnalyticsConfig Analytics(IReadOnlyList<AnalyticsProviderConfig> providers, bool enabled)
        => new() { Enabled = enabled, Providers = providers };

    private static AnalyticsProviderConfig Provider(
        string type,
        string? measurementId = null)
        => new() { Type = type, MeasurementId = measurementId };

    private static HtmlTransformContext HtmlContext(ILogger? logger = null)
        => new(
            "/test/",
            "test/index.html",
            HtmlDocumentKind.Content,
            BuildExecutionMode.Production,
            logger ?? new ConsoleLogger(LogLevel.Error));

    private sealed class TestHtmlTransformPlugin(
        string name,
        int order,
        Func<IHtmlTransform> factory) : IBukitPlugin, IOrderedPlugin, IHookFilterPlugin, IHtmlTransformPlugin
    {
        private int _createCount;

        public string Name => name;
        public string Version => "1.0.0";
        public int Order => order;
        public int CreateCount => Volatile.Read(ref _createCount);
        public bool SupportsHook(string hook) => hook == HtmlTransformHooks.HtmlTransform;

        public IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context)
        {
            Interlocked.Increment(ref _createCount);
            return factory();
        }
    }

    private sealed class AppendingTransform(string name) : IHtmlTransform
    {
        public string Name => name;
        public string Transform(HtmlTransformContext context, string html) => html + "|" + name;
    }

    private sealed class RejectingHtmlTransformPlugin :
        IBukitPlugin,
        IHookFilterPlugin,
        IHtmlTransformPlugin
    {
        public string Name => "rejecting";
        public string Version => "1.0.0";
        public bool Created { get; private set; }
        public bool SupportsHook(string hook) => false;

        public IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context)
        {
            Created = true;
            return new AppendingTransform(Name);
        }
    }

    private sealed class ThrowingTransform(string message) : IHtmlTransform
    {
        public string Name => "throwing";
        public string Transform(HtmlTransformContext context, string html) => throw new InvalidOperationException(message);
    }

    private sealed class SometimesThrowingTransform : IHtmlTransform
    {
        private int _calls;
        public string Name => "parallel";

        public string Transform(HtmlTransformContext context, string html)
        {
            var call = Interlocked.Increment(ref _calls);
            return call % 7 == 0 ? throw new InvalidOperationException($"boom {call}") : html + "|ok";
        }
    }

    private sealed class BlockingDerivePlugin : IBukitPlugin, IDerivePagesAsyncPlugin
    {
        public string Name => "blocking-derive";
        public string Version => "1.0.0";
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<RoutedContentDocument>> DerivePagesAsync(
            BuildContext context,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Array.Empty<RoutedContentDocument>();
        }
    }

    private sealed class BlockingAfterBuildPlugin : IBukitPlugin, IAfterBuildAsyncPlugin
    {
        public string Name => "blocking-after";
        public string Version => "1.0.0";
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task AfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public ConcurrentQueue<string> Warnings { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Enqueue(message);
        public void Error(string message) { }
    }
}
