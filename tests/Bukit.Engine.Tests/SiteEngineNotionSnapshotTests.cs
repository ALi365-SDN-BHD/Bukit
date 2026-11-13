using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Bukit.Config;
using Bukit.Content.Notion;
using Bukit.Content.Notion.Snapshot;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed partial class SiteEngineIntegrationTests
{
    private const string SnapshotDatabaseId = "5e1f0000-0000-4000-8000-0000000000aa";
    private const string SnapshotToken = "secret_engine_snapshot_token_51c2";

    [Fact]
    public async Task BuildAsync_NotionOfflineSnapshot_RebuildsTheSameSiteWithoutToken()
    {
        var (onlineRoot, onlineConfig) = CreateNotionSnapshotSite();
        var (offlineRoot, offlineConfig) = CreateNotionSnapshotSite();
        var snapshotRoot = Path.Combine(Path.GetTempPath(), "bukit-engine-notion-snapshot", Guid.NewGuid().ToString("N"));
        var snapshotDir = Path.Combine(snapshotRoot, "snapshot");
        var originalToken = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);
        var onlineApi = new SnapshotNotionApi();
        var offlineApi = new SnapshotNotionApi();
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, SnapshotToken);
            NotionContentClient.TransportOverride.Value = () => onlineApi;
            using (var recording = NotionSnapshotSession.BeginWrite(snapshotDir))
            using (NotionSnapshotScope.Enter(recording))
            {
                await new SiteEngine(new TestLogger()).BuildAsync(onlineConfig, onlineRoot, new ConfigOverrides { Clean = true, Incremental = false });
                recording.Commit();
            }

            var requests = onlineApi.Requests.ToArray();
            Assert.Contains(requests, r => r.StartsWith("POST /v1/databases/", StringComparison.Ordinal));
            // Schema (filter), taxonomy options and pages_index all went through the recorded client.
            Assert.True(requests.Count(r => r == $"GET /v1/databases/{SnapshotDatabaseId}") >= 2);
            Assert.Contains("GET /v1/pages/rel-1", requests);

            NotionContentClient.TransportOverride.Value = () => offlineApi;
            var logger = new TestLogger();
            using (var offline = NotionSnapshotSession.OpenOffline(snapshotDir))
            using (NotionSnapshotScope.Enter(offline))
            {
                await new SiteEngine(logger).BuildAsync(offlineConfig, offlineRoot, new ConfigOverrides { Clean = true, Incremental = false });
            }

            Assert.Empty(offlineApi.Requests);
            var onlineOutput = Path.Combine(onlineRoot, "dist");
            var offlineOutput = Path.Combine(offlineRoot, "dist");
            var onlineFiles = EnumeratePublicOutputFiles(onlineOutput);
            Assert.Contains("pages/notion-page-1/index.html", onlineFiles);
            Assert.Contains("pages/notion-page-2/index.html", onlineFiles);
            Assert.Equal(onlineFiles, EnumeratePublicOutputFiles(offlineOutput));
            foreach (var file in onlineFiles)
            {
                Assert.True(
                    File.ReadAllBytes(Path.Combine(onlineOutput, file)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(offlineOutput, file))),
                    $"Offline output differs: {file}");
            }

            Assert.Contains("Body of notion-page-1", File.ReadAllText(Path.Combine(offlineOutput, "pages", "notion-page-1", "index.html")), StringComparison.Ordinal);
            // Neither build wrote Notion data into the site directory.
            foreach (var root in new[] { onlineRoot, offlineRoot })
            {
                Assert.False(Directory.Exists(Path.Combine(root, ".cache", "notion")));
                Assert.False(File.Exists(Path.Combine(root, "pages-cache.json")));
            }

            foreach (var file in Directory.EnumerateFiles(snapshotDir, "*", SearchOption.AllDirectories))
            {
                Assert.DoesNotContain(SnapshotToken, File.ReadAllText(file), StringComparison.Ordinal);
            }

            // pages_index reads its pages from the snapshot: a missing one fails instead of vanishing.
            File.Delete(Path.Combine(snapshotDir, "api", "pages", "rel1.json"));
            using (var offline = NotionSnapshotSession.OpenOffline(snapshotDir))
            using (NotionSnapshotScope.Enter(offline))
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                    new SiteEngine(new TestLogger()).BuildAsync(offlineConfig, offlineRoot, new ConfigOverrides { Clean = true, Incremental = false }));
                var messages = string.Join(" | ", Unwrap(error).Select(static e => e.Message));
                Assert.Contains("Notion offline snapshot has no page 'rel-1'", messages, StringComparison.Ordinal);
            }

            Assert.Empty(offlineApi.Requests);
        }
        finally
        {
            NotionContentClient.TransportOverride.Value = null;
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, originalToken);
            CleanupDir(onlineRoot);
            CleanupDir(offlineRoot);
            CleanupDir(snapshotRoot);
        }
    }

    private static IEnumerable<Exception> Unwrap(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Unwrap))
                {
                    yield return inner;
                }
            }
        }
    }

    private static (string Root, AppConfig Config) CreateNotionSnapshotSite()
    {
        var (root, config) = CreateBuildReportHealthSite();
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"),
            "<html><head><title>{{ page.title }}</title></head><body><main>{{ page.content }}<p>{{ page.tags | array.join \",\" }}</p></main></body></html>");
        var source = TestContent.NotionSource(SnapshotDatabaseId) with
        {
            Notion = new NotionConfig { DatabaseId = SnapshotDatabaseId, CacheMode = "readwrite", PageSize = 1, FieldPolicy = new NotionFieldPolicyConfig { Mode = "all" } }
        };
        return (root, config with
        {
            Content = ContentConfigFactory.FromSources([source]) with { Media = new MediaConfig { DownloadToLocal = false } },
            Build = config.Build with { Clean = true, Report = new BuildReportConfig { Enabled = false } },
            Theme = config.Theme with
            {
                Params = new Dictionary<string, object>
                {
                    ["pages_index"] = new Dictionary<string, object>
                    {
                        ["resolve_notion"] = new Dictionary<string, object>
                        {
                            ["enabled"] = true,
                            ["field_keys"] = new[] { "related" },
                            ["cache_mode"] = "readwrite",
                            ["cache_path"] = "pages-cache.json"
                        }
                    }
                }
            }
        });
    }

    private sealed class SnapshotNotionApi : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue($"{request.Method} {path}");
            if (request.Method == HttpMethod.Get && path == $"/v1/databases/{SnapshotDatabaseId}")
            {
                return Json("""
                    {"object":"database","properties":{
                      "Title":{"type":"title"},"Slug":{"type":"rich_text"},"Type":{"type":"select"},
                      "PublishAt":{"type":"date"},"Collection":{"type":"select"},"Published":{"type":"checkbox"},
                      "Related":{"type":"relation"},
                      "Tags":{"type":"multi_select","multi_select":{"options":[{"name":"News"},{"name":"Unused option"}]}}}}
                    """);
            }

            if (request.Method == HttpMethod.Post && path == $"/v1/databases/{SnapshotDatabaseId}/query")
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                return body.Contains("\"start_cursor\":\"next\"", StringComparison.Ordinal)
                    ? Json($$"""{"object":"list","has_more":false,"next_cursor":null,"results":[{{Page(2)}}]}""")
                    : Json($$"""{"object":"list","has_more":true,"next_cursor":"next","results":[{{Page(1)}}]}""");
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/v1/blocks/", StringComparison.Ordinal))
            {
                var id = path.Split('/')[3];
                return Json("{\"object\":\"list\",\"has_more\":false,\"results\":[{\"type\":\"paragraph\",\"paragraph\":{\"rich_text\":[{\"plain_text\":\"Body of " + id + "\"}]}}]}");
            }

            if (request.Method == HttpMethod.Get && path == "/v1/pages/rel-1")
            {
                return Json("""
                    {"object":"page","id":"rel-1","url":"https://www.notion.so/rel-1","properties":{
                      "Name":{"type":"title","title":[{"plain_text":"Related page"}]},
                      "Summary":{"type":"rich_text","rich_text":[{"plain_text":"Related summary"}]}}}
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        }

        private static string Page(int number) => $$$"""
            {"object":"page","id":"notion-page-{{{number}}}","created_time":"2026-01-02T03:04:05.000Z","last_edited_time":"2026-05-15T12:00:00.000Z",
             "properties":{
               "Title":{"type":"title","title":[{"plain_text":"Notion Page {{{number}}}"}]},
               "Slug":{"type":"rich_text","rich_text":[{"plain_text":"notion-page-{{{number}}}"}]},
               "Type":{"type":"select","select":{"name":"page"}},
               "PublishAt":{"type":"date","date":{"start":"2026-05-15T10:30:00+00:00"}},
               "Collection":{"type":"select","select":{"name":"page"}},
               "Published":{"type":"checkbox","checkbox":true},
               "Tags":{"type":"multi_select","multi_select":[{"name":"News"}]},
               "Related":{"type":"relation","relation":[{"id":"rel-1"}]}
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
