using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SiteEngineBodyStoreLifetimeTests
{
    [Fact]
    public async Task BuildAsync_WhenBuildCompletes_DisposesFinalBodyStoreOnce()
    {
        var root = CreateSite();
        var store = new TrackingBodyStore(_ => Task.FromResult(new ContentBody("<p>body</p>")));

        try
        {
            var engine = CreateEngine(store);

            await engine.BuildAsync(CreateConfig(), root, new ConfigOverrides(), CancellationToken.None);

            Assert.Equal(1, store.DisposeCount);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task BuildAsync_WhenConsumerThrows_DisposesFinalBodyStoreOnce()
    {
        var root = CreateSite();
        var store = new TrackingBodyStore(_ => Task.FromException<ContentBody>(new InvalidOperationException("body failed")));

        try
        {
            var engine = CreateEngine(store);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                engine.BuildAsync(CreateConfig(), root, new ConfigOverrides(), CancellationToken.None));

            Assert.Equal(1, store.DisposeCount);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task BuildAsync_WhenConsumerIsCanceled_DisposesFinalBodyStoreOnce()
    {
        var root = CreateSite();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new TrackingBodyStore(async cancellationToken =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new ContentBody("<p>unreachable</p>");
        });
        using var cts = new CancellationTokenSource();

        try
        {
            var engine = CreateEngine(store);
            var buildTask = engine.BuildAsync(CreateConfig(), root, new ConfigOverrides(), cts.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => buildTask);
            Assert.Equal(1, store.DisposeCount);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    private static SiteEngine CreateEngine(TrackingBodyStore store)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "post"),
            ["collection"] = new("text", "post")
        };
        var document = new RawContentDocument(
            Id: "post-1",
            Title: "Post 1",
            Slug: "post-1",
            PublishAt: DateTimeOffset.UtcNow,
            Body: new RawBody(null, "post-1"),
            Properties: RawContentValue.FromFields(fields),
            CustomFields: fields);
        var loadResult = new RawContentLoadResult([document], store);
        return new SiteEngine(
            new SilentLogger(),
            new StaticFactory(loadResult),
            new DefaultSearchIndexBuilder());
    }

    private static AppConfig CreateConfig()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "body-store-lifetime",
                Title = "Body Store Lifetime",
                Url = "https://example.com",
                Language = "en",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new()
                    {
                        Permalink = "/posts/{slug}/",
                        Template = "pages/post.html",
                        ListRoute = "/posts/",
                        ListTemplate = "pages/list.html"
                    }
                }
            },
            Content = TestContent.Markdown(collection: "post") with
            {
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = new BuildConfig
            {
                Output = "dist",
                Clean = true,
                Report = new BuildReportConfig { Enabled = false }
            },
            Theme = new ThemeConfig { Layouts = "layouts" }
        };

    private static string CreateSite()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-body-store-lifetime", Guid.NewGuid().ToString("N"));
        var pages = Path.Combine(root, "layouts", "pages");
        Directory.CreateDirectory(pages);
        File.WriteAllText(Path.Combine(root, "layouts", "theme.yaml"), """
            name: lifetime-test
            version: 1.0.0
            engine: bukit
            templates:
              home:
                template: pages/index.html
                required: true
              post:
                template: pages/post.html
                accepts:
                  type: post
                  collection: post
              list:
                template: pages/list.html
                accepts:
                  kind: list
              taxonomyIndex:
                template: pages/taxonomy-index.html
                accepts:
                  kind: taxonomy-index
              taxonomyTerm:
                template: pages/taxonomy-term.html
                accepts:
                  kind: taxonomy-term
              pagination:
                template: pages/pagination.html
                accepts:
                  kind: pagination
              search:
                template: pages/search.html
                accepts:
                  kind: search
            """);
        File.WriteAllText(Path.Combine(pages, "index.html"), "index");
        File.WriteAllText(Path.Combine(pages, "post.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(pages, "list.html"), "list");
        File.WriteAllText(Path.Combine(pages, "taxonomy-index.html"), "taxonomy");
        File.WriteAllText(Path.Combine(pages, "taxonomy-term.html"), "term");
        File.WriteAllText(Path.Combine(pages, "pagination.html"), "pagination");
        File.WriteAllText(Path.Combine(pages, "search.html"), "search");
        return root;
    }

    private sealed class TrackingBodyStore : IContentBodyStore, IAsyncDisposable
    {
        private readonly Func<CancellationToken, Task<ContentBody>> _getAsync;
        private int _disposeCount;

        public TrackingBodyStore(Func<CancellationToken, Task<ContentBody>> getAsync)
        {
            _getAsync = getAsync;
        }

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
            => _getAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StaticFactory : IContentProviderFactory
    {
        private readonly RawContentLoadResult _result;

        public StaticFactory(RawContentLoadResult result)
        {
            _result = result;
        }

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
            => new StaticProvider(_result);

        public Task<RawContentLoadResult> LocalizeContentImagesAsync(
            RawContentLoadResult result,
            MediaConfig media,
            string rootDir,
            string cacheDir,
            ILogger logger,
            CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private sealed class StaticProvider : IContentProvider
    {
        private readonly RawContentLoadResult _result;

        public StaticProvider(RawContentLoadResult result)
        {
            _result = result;
        }

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class SilentLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
