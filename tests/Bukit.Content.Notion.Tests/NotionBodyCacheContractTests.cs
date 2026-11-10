using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Transport;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Notion.Tests;

// fix-notion-body-cache-contract: cache format 2 is bound to the renderer contract and
// reused only once the edit minute has settled on Notion's clock.
public sealed class NotionBodyCacheContractTests : IDisposable
{
    private const string EditedAt = "2026-11-10T10:00:00.000Z";
    private static readonly DateTimeOffset s_editMinute = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-notion-contract-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(65, true)]
    [InlineData(120, true)]
    [InlineData(30, false)]
    [InlineData(64, false)]
    public async Task Readwrite_ReusesOnlyAfterTheEditMinuteSettles(int fetchedSecondsAfterMinute, bool reused)
    {
        var handler = new BlocksHandler { ServerTime = s_editMinute.AddSeconds(fetchedSecondsAfterMinute) };
        var cache = CreateCache("readwrite");

        await RenderAsync(handler, cache, EditedAt);
        await RenderAsync(handler, cache, EditedAt);

        Assert.Equal(reused ? 1 : 2, handler.RequestCount);
    }

    [Fact]
    public async Task Readwrite_UsesNotionClockNotLocalClock()
    {
        // Local clock 30 s ahead: local time says settled, Notion's Date header says not.
        var handler = new BlocksHandler { ServerTime = s_editMinute.AddSeconds(55) };
        var cache = CreateCache("readwrite");
        var localNow = s_editMinute.AddSeconds(85);

        await RenderAsync(handler, cache, EditedAt, localNow);
        Assert.Equal(s_editMinute.AddSeconds(55), ReadFetchedAt());

        await RenderAsync(handler, cache, EditedAt, localNow);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Readwrite_FallsBackToLocalClockWithoutDateHeader()
    {
        var handler = new BlocksHandler { ServerTime = null };
        var cache = CreateCache("readwrite");
        var localNow = s_editMinute.AddSeconds(70);

        await RenderAsync(handler, cache, EditedAt, localNow);
        await RenderAsync(handler, cache, EditedAt, localNow);

        Assert.Equal(localNow, ReadFetchedAt());
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Readwrite_DoesNotReuseEntriesWithoutLastEditedTime()
    {
        var handler = new BlocksHandler { ServerTime = s_editMinute.AddHours(1) };
        var cache = CreateCache("readwrite");

        await RenderAsync(handler, cache, lastEditedTime: null);
        await RenderAsync(handler, cache, lastEditedTime: null);

        Assert.Equal(2, handler.RequestCount);
    }

    [Theory]
    [InlineData(1, NotionBlocksRenderer.ContractVersion)]
    [InlineData(2, NotionBlocksRenderer.ContractVersion - 1)]
    [InlineData(2, null)]
    public async Task Readwrite_RefetchesOtherFormatsAndContracts(int format, int? renderer)
    {
        var handler = new BlocksHandler { ServerTime = s_editMinute.AddHours(1) };
        var cache = CreateCache("readwrite");
        WriteEntry(format, renderer, "<p>stale</p>");

        var html = await RenderAsync(handler, cache, EditedAt);

        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain("stale", html, StringComparison.Ordinal);
        using var entry = JsonDocument.Parse(File.ReadAllText(EntryPath));
        Assert.Equal(NotionCacheManager.CacheFormat, entry.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(NotionBlocksRenderer.ContractVersion, entry.RootElement.GetProperty("renderer").GetInt32());
    }

    [Theory]
    [InlineData(1, null, 1)]
    [InlineData(2, NotionBlocksRenderer.ContractVersion - 1, 1)]
    [InlineData(2, NotionBlocksRenderer.ContractVersion, 0)]
    public async Task Readonly_ReusesReadableFormatsAndWarnsOnceForOtherContracts(int format, int? renderer, int expectedStale)
    {
        var handler = new BlocksHandler { ServerTime = s_editMinute.AddSeconds(10) };
        var cache = CreateCache("readonly");
        WriteEntry(format, renderer, "<p>frozen</p>", fetchedAt: s_editMinute.AddSeconds(10));

        Assert.Equal("<p>frozen</p>", await RenderAsync(handler, cache, EditedAt));
        Assert.Equal("<p>frozen</p>", await RenderAsync(handler, cache, EditedAt));
        Assert.Equal(0, handler.RequestCount);

        var logger = new CapturingLogger();
        cache.ReportStaleContracts(logger);
        cache.ReportStaleContracts(logger);
        if (expectedStale == 0)
        {
            Assert.Empty(logger.Warnings);
        }
        else
        {
            var warning = Assert.Single(logger.Warnings);
            Assert.Contains("event=notion.cache.renderer_mismatch", warning, StringComparison.Ordinal);
            Assert.Contains("entries=2", warning, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task FetchedAt_IsTheEarliestResponseOfThePage()
    {
        var handler = new BlocksHandler
        {
            ServerTime = s_editMinute.AddSeconds(90),
            Paged = true
        };
        var cache = CreateCache("readwrite");

        await RenderAsync(handler, cache, EditedAt);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(s_editMinute.AddSeconds(90), ReadFetchedAt());
    }

    [Fact]
    public async Task Renderer_LastPageFetchedAt_ResetsPerPage()
    {
        var handler = new BlocksHandler { ServerTime = s_editMinute.AddSeconds(90) };
        using var client = CreateClient(handler, DateTimeOffset.UtcNow);
        var renderer = new NotionBlocksRenderer(client);

        await renderer.RenderPageAsync("page-1", CancellationToken.None);
        Assert.Equal(s_editMinute.AddSeconds(90), renderer.LastPageFetchedAt);

        handler.ServerTime = s_editMinute.AddSeconds(200);
        await renderer.RenderPageAsync("page-1", CancellationToken.None);
        Assert.Equal(s_editMinute.AddSeconds(200), renderer.LastPageFetchedAt);
    }

    [Theory]
    [InlineData("2026-11-10T10:00:00.000Z", 64, false)]
    [InlineData("2026-11-10T10:00:00.000Z", 65, true)]
    [InlineData("2026-11-10T10:00:59.000Z", 65, true)]
    [InlineData("2026-11-10T18:00:00.000+08:00", 65, true)]
    [InlineData("not-a-time", 3600, false)]
    [InlineData("", 3600, false)]
    public void IsSettled_UsesTheEndOfTheEditMinutePlusMargin(string lastEditedTime, int secondsAfterMinute, bool settled)
        => Assert.Equal(settled, NotionCacheManager.IsSettled(lastEditedTime, s_editMinute.AddSeconds(secondsAfterMinute)));

    [Fact]
    public void BodyFingerprint_CarriesRendererAndSettledState()
    {
        var unsettled = NotionContentSource.BuildBodyFingerprint(EditedAt, s_editMinute.AddSeconds(30), renderContent: true);
        var settled = NotionContentSource.BuildBodyFingerprint(EditedAt, s_editMinute.AddSeconds(65), renderContent: true);
        var later = NotionContentSource.BuildBodyFingerprint(EditedAt, s_editMinute.AddHours(5), renderContent: true);
        var off = NotionContentSource.BuildBodyFingerprint(EditedAt, s_editMinute.AddHours(5), renderContent: false);

        Assert.Equal($"{EditedAt}:renderer={NotionBlocksRenderer.ContractVersion}:unsettled:content=on", unsettled);
        Assert.Equal($"{EditedAt}:renderer={NotionBlocksRenderer.ContractVersion}:content=on", settled);
        Assert.Equal(settled, later);
        Assert.EndsWith(":content=off", off, StringComparison.Ordinal);
        Assert.Null(NotionContentSource.BuildBodyFingerprint(null, s_editMinute, renderContent: true));
        Assert.Null(NotionContentSource.BuildBodyFingerprint(" ", s_editMinute, renderContent: true));
    }

    private string EntryPath => Path.Combine(_root, "pages", "page-1.json");

    private NotionCacheManager.PageHtmlCache CreateCache(string mode)
    {
        Directory.CreateDirectory(Path.Combine(_root, "pages"));
        var cache = NotionCacheManager.CreatePageHtmlCache(new NotionContentSourceOptions
        {
            DatabaseId = "db-1",
            Token = "token",
            CacheMode = mode,
            CacheDir = _root
        });
        Assert.NotNull(cache);
        return cache;
    }

    private static async Task<string> RenderAsync(
        BlocksHandler handler,
        NotionCacheManager.PageHtmlCache cache,
        string? lastEditedTime,
        DateTimeOffset? localNow = null)
    {
        using var client = CreateClient(handler, localNow ?? DateTimeOffset.UtcNow);
        var renderer = new NotionBlocksRenderer(client);
        return await NotionCacheManager.GetOrRenderPageHtmlAsync(
            renderer, cache, "page-1", lastEditedTime, CancellationToken.None);
    }

    private static NotionClient CreateClient(HttpMessageHandler handler, DateTimeOffset localNow)
        => new(
            new NotionClientOptions { Token = "token", MaxRetries = 0 },
            handler,
            static (_, _) => Task.CompletedTask,
            () => localNow);

    private void WriteEntry(int format, int? renderer, string html, DateTimeOffset? fetchedAt = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["version"] = format,
            ["lastEditedTime"] = EditedAt,
            ["html"] = html
        };
        if (renderer is not null)
        {
            payload["renderer"] = renderer;
        }

        payload["fetchedAt"] = (fetchedAt ?? s_editMinute.AddHours(1)).ToString("O");
        Directory.CreateDirectory(Path.GetDirectoryName(EntryPath)!);
        File.WriteAllText(EntryPath, JsonSerializer.Serialize(payload, CacheTestJsonContext.Default.DictionaryStringObject));
    }

    private DateTimeOffset? ReadFetchedAt()
    {
        using var entry = JsonDocument.Parse(File.ReadAllText(EntryPath));
        return entry.RootElement.TryGetProperty("fetchedAt", out var value) ? value.GetDateTimeOffset() : null;
    }

    private sealed class BlocksHandler : HttpMessageHandler
    {
        public DateTimeOffset? ServerTime { get; set; }

        public bool Paged { get; init; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var secondPage = request.RequestUri!.Query.Contains("start_cursor=", StringComparison.Ordinal);
            var json = Paged && !secondPage
                ? Page("first", hasMore: true)
                : Page(secondPage ? "second" : "only", hasMore: false);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            if (ServerTime is { } serverTime)
            {
                // Later pages answer later; fetchedAt must keep the first one.
                response.Headers.Date = secondPage ? serverTime.AddSeconds(20) : serverTime;
            }

            return Task.FromResult(response);
        }

        private static string Page(string text, bool hasMore)
            => $$"""
                {
                  "results": [
                    { "id": "block-{{text}}", "type": "paragraph",
                      "paragraph": { "rich_text": [ { "type": "text", "plain_text": "{{text}}", "text": { "content": "{{text}}" } } ] } }
                  ],
                  "has_more": {{(hasMore ? "true" : "false")}},
                  "next_cursor": {{(hasMore ? "\"cursor-2\"" : "null")}}
                }
                """;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public void Debug(string message) { }

        public void Info(string message) { }

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message) { }
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, object?>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(int))]
[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
internal sealed partial class CacheTestJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
