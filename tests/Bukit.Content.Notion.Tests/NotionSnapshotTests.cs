using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion.Snapshot;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Notion.Tests;

public sealed class NotionSnapshotTests : IDisposable
{
    private const string Token = "secret_snapshot_token_7f3a9c";
    private const string DatabaseId = "1a2b3c4d-0000-4000-8000-00000000db01";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-notion-snapshot-" + Guid.NewGuid().ToString("N"));

    private string Target => Path.Combine(_root, "snapshot");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task OfflineLoad_MatchesTheOnlineLoadThatWroteTheSnapshot_WithoutAnyRequest()
    {
        var online = await LoadOnlineAsync(Options());

        using var offlineSnapshot = NotionSnapshotSession.OpenOffline(Target);
        var handler = new FakeNotionApi();
        NotionContentClient.TransportOverride.Value = () => handler;
        try
        {
            var offline = await LoadAsync(Options(offlineSnapshot, token: string.Empty));
            Assert.Equal(online, offline);
            Assert.Empty(handler.Requests);
        }
        finally
        {
            NotionContentClient.TransportOverride.Value = null;
        }
    }

    [Fact]
    public async Task Snapshot_NeverContainsTheToken_AndIsPrivate()
    {
        await LoadOnlineAsync(Options());

        foreach (var file in Directory.EnumerateFiles(Target, "*", SearchOption.AllDirectories))
        {
            var text = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file));
            Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
            Assert.DoesNotContain("Bearer", text, StringComparison.Ordinal);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            var directories = Directory.EnumerateDirectories(Target, "*", SearchOption.AllDirectories).Append(Target);
            foreach (var directory in directories)
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
            }
        }

        // Raw API data: every property, drafts included, and Notion's server time.
        var queryFiles = Directory.GetFiles(Path.Combine(Target, "api", "databases"), "first.json", SearchOption.AllDirectories);
        var first = await File.ReadAllTextAsync(Assert.Single(queryFiles));
        Assert.Contains("InternalNote", first, StringComparison.Ordinal);
        Assert.Contains("\"serverTime\":\"2026-06-01T10:00:00", first, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedBuild_KeepsThePreviousSnapshot()
    {
        await LoadOnlineAsync(Options());
        var manifest = await File.ReadAllTextAsync(Path.Combine(Target, "manifest.json"));

        using (var failed = NotionSnapshotSession.BeginWrite(Target))
        {
            NotionContentClient.TransportOverride.Value = () => new FakeNotionApi();
            try
            {
                await LoadAsync(Options(failed));
            }
            finally
            {
                NotionContentClient.TransportOverride.Value = null;
            }
            // No commit: the build failed after loading.
        }

        Assert.Equal(manifest, await File.ReadAllTextAsync(Path.Combine(Target, "manifest.json")));
        Assert.Single(Directory.GetDirectories(_root));
    }

    [Fact]
    public async Task ChangedFilter_UsesTheFormalBuildPages_AndWarnsOnce()
    {
        var online = await LoadOnlineAsync(Options());
        using var offlineSnapshot = NotionSnapshotSession.OpenOffline(Target);
        var logger = new ListLogger();

        var offline = await LoadAsync(Options(offlineSnapshot, token: string.Empty) with { FilterProperty = "Featured" }, logger);

        Assert.Equal(online, offline);
        Assert.Single(logger.Warnings, w => w.StartsWith("event=notion.snapshot.query_changed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangedDatabaseId_IsAClearError()
    {
        await LoadOnlineAsync(Options());
        using var offlineSnapshot = NotionSnapshotSession.OpenOffline(Target);

        var error = await Assert.ThrowsAsync<NotionSnapshotException>(() =>
            LoadAsync(Options(offlineSnapshot, token: string.Empty) with { DatabaseId = "99999999-0000-4000-8000-000000000000" }));
        Assert.Contains("no data for database", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Target, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingBody_IsAClearError()
    {
        await LoadOnlineAsync(Options());
        File.Delete(Path.Combine(Target, "cache", "pages", "page-1.json"));
        using var offlineSnapshot = NotionSnapshotSession.OpenOffline(Target);

        var error = await Assert.ThrowsAsync<NotionSnapshotException>(() => LoadAsync(Options(offlineSnapshot, token: string.Empty)));
        Assert.Contains("rendered body for page 'page-1'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingRelationTarget_IsAClearError()
    {
        await LoadOnlineAsync(Options());
        File.Delete(Path.Combine(Target, "api", "pages", "rel1.json"));
        using var offlineSnapshot = NotionSnapshotSession.OpenOffline(Target);

        var error = await Assert.ThrowsAsync<NotionSnapshotException>(() => LoadAsync(Options(offlineSnapshot, token: string.Empty)));
        Assert.Contains("page 'rel-1'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordedNotFound_ReplaysAsTheSameUnresolvedRelation()
    {
        var api = new FakeNotionApi { MissingPages = { "rel-1" } };
        var online = await LoadOnlineAsync(Options(), api);
        Assert.True(File.Exists(Path.Combine(Target, "api", "pages", "rel1.error.json")));

        using var offlineSnapshot = NotionSnapshotSession.OpenOffline(Target);
        var offline = await LoadAsync(Options(offlineSnapshot, token: string.Empty));

        Assert.Equal(online, offline);
    }

    [Fact]
    public async Task OtherBukitVersionOrFormat_IsAnError()
    {
        await LoadOnlineAsync(Options());

        NotionSnapshotSession.BukitVersionOverride.Value = "0.0.0-other";
        try
        {
            var error = Assert.Throws<NotionSnapshotException>(() => NotionSnapshotSession.OpenOffline(Target));
            Assert.Contains("was written by Bukit", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            NotionSnapshotSession.BukitVersionOverride.Value = null;
        }

        var manifestPath = Path.Combine(Target, "manifest.json");
        var manifest = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(manifestPath, manifest.Replace("\"format\": 1", "\"format\": 99", StringComparison.Ordinal));
        Assert.Contains("format 99", Assert.Throws<NotionSnapshotException>(() => NotionSnapshotSession.OpenOffline(Target)).Message, StringComparison.Ordinal);
        Assert.Throws<NotionSnapshotException>(() => NotionSnapshotSession.OpenOffline(Path.Combine(_root, "absent")));
    }

    [Fact]
    public async Task RewritingTheSnapshot_DropsBodiesThePreviousBuildNoLongerUses()
    {
        await LoadOnlineAsync(Options());
        Assert.True(File.Exists(Path.Combine(Target, "cache", "pages", "page-2.json")));

        var api = new FakeNotionApi { OmitPage2 = true };
        await LoadOnlineAsync(Options(), api);

        Assert.True(File.Exists(Path.Combine(Target, "cache", "pages", "page-1.json")));
        Assert.False(File.Exists(Path.Combine(Target, "cache", "pages", "page-2.json")));
        // page-1 was unchanged and its body was reused from the previous snapshot.
        Assert.DoesNotContain(api.Requests, r => r.Contains("/blocks/page-1/", StringComparison.Ordinal));
    }

    [Fact]
    public void OfflineClient_HasNoTransport()
    {
        using var write = NotionSnapshotSession.BeginWrite(Target);
        write.Commit();
        using var offline = NotionSnapshotSession.OpenOffline(Target);
        using var client = NotionContentClient.Create(Options(offline, token: string.Empty));

        Assert.True(client.IsOffline);
        Assert.Throws<NotionSnapshotException>(() => client.Transport);
    }

    private async Task<string> LoadOnlineAsync(NotionContentSourceOptions options, FakeNotionApi? api = null)
    {
        api ??= new FakeNotionApi();
        using var snapshot = NotionSnapshotSession.BeginWrite(Target);
        NotionContentClient.TransportOverride.Value = () => api;
        try
        {
            var projected = await LoadAsync(options with { Snapshot = snapshot });
            snapshot.Commit();
            Assert.Contains(api.Requests, r => r.StartsWith("POST", StringComparison.Ordinal));
            return projected;
        }
        finally
        {
            NotionContentClient.TransportOverride.Value = null;
        }
    }

    /// <summary>Loads every document and body and projects them to comparable text.</summary>
    private static async Task<string> LoadAsync(NotionContentSourceOptions options, ILogger? logger = null)
    {
        var source = new NotionContentSource(options, logger);
        var result = await source.LoadRawAsync();
        await using var store = result.BodyStore as IAsyncDisposable;
        var builder = new StringBuilder();
        foreach (var document in result.Documents.OrderBy(static d => d.Id, StringComparer.Ordinal))
        {
            var body = await result.BodyStore.GetAsync(document);
            builder.Append(document.Id).Append('|').Append(document.Title).Append('|').Append(document.Slug)
                .Append('|').Append(document.PublishAt.ToString("O")).Append('\n');
            foreach (var (key, field) in document.CustomFields!.OrderBy(static f => f.Key, StringComparer.Ordinal))
            {
                builder.Append("  ").Append(key).Append('=').Append(Describe(field.Value)).Append('\n');
            }

            builder.Append("  body=").Append(body.Html).Append('\n');
        }

        return builder.ToString();
    }

    private static string Describe(object? value)
    {
        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            return value?.ToString() ?? "null";
        }
    }

    private NotionContentSourceOptions Options(NotionSnapshotSession? snapshot = null, string token = Token)
        => new()
        {
            DatabaseId = DatabaseId,
            Token = token,
            PageSize = 1,
            FieldPolicyMode = "all",
            FilterProperty = "Published",
            FilterType = "checkbox_true",
            RenderContent = true,
            MaxRetries = 0,
            MaxRps = 1000,
            CacheMode = snapshot is null ? "off" : snapshot.Offline ? "readonly" : "readwrite",
            CacheDir = snapshot?.CacheDirectory,
            Snapshot = snapshot
        };

    private sealed class ListLogger : ILogger
    {
        public ConcurrentQueue<string> Warnings { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Enqueue(message);
        public void Error(string message) { }
    }

    /// <summary>A fake Notion API: two pages over two query pages, bodies and one relation target.</summary>
    internal sealed class FakeNotionApi : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();
        public HashSet<string> MissingPages { get; } = new(StringComparer.Ordinal);
        public bool OmitPage2 { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue($"{request.Method} {request.RequestUri.PathAndQuery}");
            if (request.Method == HttpMethod.Get && path == $"/v1/databases/{DatabaseId}")
            {
                return Json("""{"object":"database","properties":{"Title":{"type":"title"},"Published":{"type":"checkbox"},"Featured":{"type":"checkbox"},"Slug":{"type":"rich_text"},"Tags":{"type":"multi_select","multi_select":{"options":[{"name":"News"}]}}}}""");
            }

            if (request.Method == HttpMethod.Post && path == $"/v1/databases/{DatabaseId}/query")
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                var second = body.Contains("\"start_cursor\":\"cursor-2\"", StringComparison.Ordinal);
                if (OmitPage2)
                {
                    return Json($$"""{"object":"list","has_more":false,"next_cursor":null,"results":[{{Page("page-1")}}]}""");
                }

                return second
                    ? Json($$"""{"object":"list","has_more":false,"next_cursor":null,"results":[{{Page("page-2")}}]}""")
                    : Json($$"""{"object":"list","has_more":true,"next_cursor":"cursor-2","results":[{{Page("page-1")}}]}""");
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/v1/blocks/", StringComparison.Ordinal))
            {
                var id = path.Split('/')[3];
                return Json($$$"""{"object":"list","has_more":false,"results":[{"type":"paragraph","paragraph":{"rich_text":[{"plain_text":"Body of {{{id}}}"}]}}]}""");
            }

            if (request.Method == HttpMethod.Get && path == "/v1/pages/rel-1")
            {
                return MissingPages.Contains("rel-1")
                    ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }
                    : Json("""{"object":"page","id":"rel-1","url":"https://www.notion.so/rel-1","properties":{"Name":{"type":"title","title":[{"plain_text":"Related"}]},"Slug":{"type":"rich_text","rich_text":[{"plain_text":"related"}]},"Type":{"type":"select","select":{"name":"tag"}}}}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        }

        private static string Page(string id) => $$$"""
            {"object":"page","id":"{{{id}}}","created_time":"2026-05-01T00:00:00.000Z","last_edited_time":"2026-05-20T08:00:00.000Z",
             "properties":{
               "Title":{"type":"title","title":[{"plain_text":"Title {{{id}}}"}]},
               "Slug":{"type":"rich_text","rich_text":[{"plain_text":"{{{id}}}"}]},
               "Published":{"type":"checkbox","checkbox":true},
               "InternalNote":{"type":"rich_text","rich_text":[{"plain_text":"not shown"}]},
               "Tags":{"type":"relation","relation":[{"id":"rel-1"}]}
             }}
            """;

        private static HttpResponseMessage Json(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            response.Headers.Date = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            return response;
        }
    }
}
