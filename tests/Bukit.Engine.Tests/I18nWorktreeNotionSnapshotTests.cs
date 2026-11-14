using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Bukit.Config;
using Bukit.Content.Notion;
using Bukit.Content.Notion.Snapshot;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Incremental;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

/// <summary>
/// The Notion offline snapshot on the multilingual worktree path. The parent loads content
/// once and each language runs as a worker; here each worker runs in-process through the
/// same steps <c>WorktreeBuildCoordinator.RunWorkerAsync</c> takes (request file round trip,
/// <c>AttachWorker</c> on the snapshot the request names, then the variant), so the Notion
/// transport seam can stand in for the real API. Real worker processes are covered offline in
/// <c>Bukit.Cli.Tests</c>; recording through real processes would reach the real Notion API.
/// </summary>
public sealed class I18nWorktreeNotionSnapshotTests : IDisposable
{
    private const string DatabaseId = "5e1f0000-0000-4000-8000-0000000018aa";
    private const string Token = "secret_i18n_snapshot_token_93d1";
    private static readonly string[] Languages = ["en", "zh-CN"];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-i18n-notion-snapshot-" + Guid.NewGuid().ToString("N"));
    private readonly string? _originalToken = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);

    private string SnapshotDir => Path.Combine(_root, "snapshot", "notion");

    public I18nWorktreeNotionSnapshotTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        NotionContentClient.TransportOverride.Value = null;
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, _originalToken);
        TestCleanup.DeleteDirectory(_root, recursive: true);
    }

    [Fact]
    public async Task Recording_WorkersShareTheParentSnapshot_AndOfflineBuildMatches()
    {
        // Recording: the parent writes a staging snapshot, every worker records into it, and
        // one commit after all languages publishes a snapshot that covers both of them.
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, Token);
        var onlineApi = new I18nNotionApi();
        NotionContentClient.TransportOverride.Value = () => onlineApi;
        var recordedOutput = Path.Combine(_root, "recorded-output");
        ConcurrentQueue<string> workerRequests;
        using (var recording = NotionSnapshotSession.BeginWrite(SnapshotDir))
        using (NotionSnapshotScope.Enter(recording))
        {
            var overrides = Overrides("recorded", recording.Directory, offline: false);
            workerRequests = await BuildAsync(Path.Combine(_root, "recorded"), overrides, onlineApi);
            recording.Commit();
            CopyPublished(overrides.Output!, recordedOutput);
        }

        // Each worker resolved its own language's relation target and read the taxonomy options.
        Assert.Contains("GET /v1/pages/rel-en", workerRequests);
        Assert.Contains("GET /v1/pages/rel-zh", workerRequests);
        Assert.Contains($"GET /v1/databases/{DatabaseId}", workerRequests);
        Assert.Single(Directory.GetFileSystemEntries(Path.GetDirectoryName(SnapshotDir)!));
        foreach (var file in new[] { "rel-en", "rel-zh" })
        {
            Assert.True(File.Exists(Path.Combine(SnapshotDir, "api", "pages", file.Replace("-", string.Empty, StringComparison.Ordinal) + ".json")), file);
        }

        foreach (var page in new[] { "page-en", "page-zh" })
        {
            Assert.True(File.Exists(Path.Combine(SnapshotDir, "cache", "pages", page + ".json")), page);
        }

        foreach (var file in Directory.EnumerateFiles(SnapshotDir, "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(Token, await File.ReadAllTextAsync(file), StringComparison.Ordinal);
        }

        Assert.Contains("Body of page-en", await File.ReadAllTextAsync(Path.Combine(recordedOutput, "en", "intro-en", "index.html")), StringComparison.Ordinal);
        Assert.Contains("Body of page-zh", await File.ReadAllTextAsync(Path.Combine(recordedOutput, "zh-CN", "intro-zh", "index.html")), StringComparison.Ordinal);
        Assert.Contains("<aside>Related rel-zh;</aside>", await File.ReadAllTextAsync(Path.Combine(recordedOutput, "zh-CN", "intro-zh", "index.html")), StringComparison.Ordinal);

        // Offline: no token, every worker reads the committed snapshot, nothing reaches Notion,
        // and the output is the recorded output byte for byte.
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);
        var offlineApi = new I18nNotionApi();
        NotionContentClient.TransportOverride.Value = () => offlineApi;
        using (var offline = NotionSnapshotSession.OpenOffline(SnapshotDir))
        using (NotionSnapshotScope.Enter(offline))
        {
            var overrides = Overrides("offline", SnapshotDir, offline: true);
            await BuildAsync(Path.Combine(_root, "offline"), overrides, offlineApi);
            Assert.Empty(offlineApi.Requests);
            var expected = Published(recordedOutput);
            var actual = Published(overrides.Output!);
            Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
            foreach (var (path, bytes) in expected)
            {
                Assert.True(bytes.AsSpan().SequenceEqual(actual[path]), $"Offline output differs: {path}");
            }
        }
    }

    [Fact]
    public async Task Offline_ALanguageMissingFromTheSnapshot_FailsThatWorker()
    {
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, Token);
        var onlineApi = new I18nNotionApi();
        NotionContentClient.TransportOverride.Value = () => onlineApi;
        using (var recording = NotionSnapshotSession.BeginWrite(SnapshotDir))
        using (NotionSnapshotScope.Enter(recording))
        {
            await BuildAsync(Path.Combine(_root, "recorded"), Overrides("recorded", recording.Directory, offline: false), onlineApi);
            recording.Commit();
        }

        // Only the zh-CN worker resolves this relation target.
        File.Delete(Path.Combine(SnapshotDir, "api", "pages", "relzh.json"));
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);
        var offlineApi = new I18nNotionApi();
        NotionContentClient.TransportOverride.Value = () => offlineApi;
        using var offline = NotionSnapshotSession.OpenOffline(SnapshotDir);
        using var scope = NotionSnapshotScope.Enter(offline);
        var finishedLanguages = new List<string>();

        var error = await Assert.ThrowsAsync<NotionSnapshotException>(() =>
            BuildAsync(Path.Combine(_root, "offline"), Overrides("offline", SnapshotDir, offline: true), offlineApi, finishedLanguages));

        Assert.Contains("Notion offline snapshot has no page 'rel-zh'", error.Message, StringComparison.Ordinal);
        Assert.Equal(DiagnosticCode.NotionSnapshotEntryMissing, error.Code);
        Assert.Equal(["en"], finishedLanguages);
        Assert.Empty(offlineApi.Requests);
    }

    private ConfigOverrides Overrides(string name, string snapshotDir, bool offline) => new()
    {
        Output = Path.Combine(_root, name + "-staging-output"),
        CacheDir = Path.Combine(_root, name + "-staging-cache"),
        Clean = false,
        Incremental = false,
        Jobs = 1,
        NotionSnapshotDir = snapshotDir,
        NotionOffline = offline
    };

    /// <summary>
    /// Prepares the build as the parent does, runs every language as a worker and finalizes.
    /// Returns the Notion requests the workers made.
    /// </summary>
    private async Task<ConcurrentQueue<string>> BuildAsync(
        string root,
        ConfigOverrides overrides,
        I18nNotionApi api,
        List<string>? finishedLanguages = null)
    {
        var configPath = CreateSite(root);
        var config = ConfigLoader.Load(configPath);
        var logger = new ConsoleLogger(LogLevel.Error);
        var engine = new SiteEngine(logger);
        await using var prepared = await engine.PrepareI18nWorktreeBuildAsync(
            config, root, root, Path.Combine(root, "dist"), overrides, CancellationToken.None);
        var content = await I18nWorktreeProtocol.CaptureContentAsync(
            "head", prepared.Plan.StartedAt, prepared.Content, root, root, CancellationToken.None);
        var contentPath = Path.Combine(root, "content-snapshot.json");
        await I18nWorktreeProtocolStore.WriteContentAsync(contentPath, content, CancellationToken.None);
        content = await I18nWorktreeProtocolStore.ReadContentAsync(contentPath, CancellationToken.None);
        var contentHash = await I18nWorktreeProtocolStore.Sha256Async(contentPath, CancellationToken.None);
        var configHash = I18nWorktreeProtocol.ComputeConfigHash(config, overrides);
        var parentRequests = api.Requests.Count;

        var variants = new List<BuildVariantResult>();
        foreach (var language in Languages)
        {
            var output = Path.Combine(overrides.Output!, language);
            var requestPath = Path.Combine(root, "request-" + language + ".json");
            await I18nWorktreeProtocolStore.WriteRequestAsync(requestPath, new I18nWorkerRequest(
                I18nWorktreeProtocol.RequestSchema, "head", configHash, contentHash, language, "text",
                configPath, root, root, config, overrides, output, Path.Combine(root, "worker-cache", language),
                prepared.AssetsDir, prepared.ScssOutputDir, prepared.Plan.MediaCacheDir, prepared.SeoAlternates,
                contentPath, Path.Combine(root, "result-" + language + ".json")), CancellationToken.None);
            var request = await I18nWorktreeProtocolStore.ReadRequestAsync(requestPath, CancellationToken.None);
            Assert.Equal(configHash, I18nWorktreeProtocol.ComputeConfigHash(request.Config, request.Overrides));

            // The worker opens the snapshot its request names, as WorktreeBuildCoordinator does.
            BuildVariantResult result;
            int warnings;
            int errors;
            using (var worker = NotionSnapshotSession.AttachWorker(request.Overrides.NotionSnapshotDir!, request.Overrides.NotionOffline))
            using (NotionSnapshotScope.Enter(worker))
            {
                (result, warnings, errors) = await engine.ExecuteI18nWorktreeVariantAsync(request, content, logger, CancellationToken.None);
            }

            var captured = await I18nWorktreeProtocol.CaptureVariantAsync(
                request.Head, request.ConfigHash, request.ContentHash, result, warnings, errors, CancellationToken.None);
            variants.Add(I18nWorktreeProtocol.RestoreVariant(
                captured, prepared.Content.ContentGraph!, "head", configHash, contentHash, language, output,
                Path.Combine(overrides.CacheDir!, "build-manifest." + language + ".json"), config.Site));
            finishedLanguages?.Add(language);
        }

        await engine.FinalizeI18nWorktreeBuildAsync(prepared, variants, 0, 0, CancellationToken.None);
        return new ConcurrentQueue<string>(api.Requests.Skip(parentRequests));
    }

    private static string CreateSite(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
        var path = Path.Combine(root, "site.yaml");
        File.WriteAllText(path, $$"""
            site:
              name: i18n-notion-snapshot-test
              title: I18N Notion Snapshot Test
              url: https://example.test
              languages: [en, zh-CN]
              defaultLanguage: en
              collections:
                page:
                  permalink: /{slug}/
                  template: pages/page.html
            content:
              sources:
                - type: notion
                  name: pages
                  collection: page
                  notion:
                    databaseId: {{DatabaseId}}
                    filterType: none
                    cacheMode: readwrite
                    pageSize: 1
                    fieldPolicy:
                      mode: all
              media:
                downloadToLocal: false
            build:
              output: dist
              clean: false
            theme:
              layouts: layouts
              params:
                pages_index:
                  resolve_notion:
                    enabled: true
                    field_keys: [related]
                    cache_mode: readwrite
                    cache_path: pages-cache.json
            """);
        File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"),
            "<!doctype html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"),
            "<main>{{ page.content }}<p>{{ page.fields.tags.value | array.join \",\" }}</p>" +
            "<aside>{{ for id in page.fields.related.value }}{{ site.data.pages_by_id[id].title }};{{ end }}</aside></main>");
        File.WriteAllText(Path.Combine(root, "layouts", "theme.yaml"), """
            name: i18n-notion-snapshot-test
            templates:
              home:
                template: pages/index.html
                required: true
              page:
                template: pages/page.html
                accepts:
                  type: page
              list:
                template: pages/list.html
                accepts:
                  kind: list
              taxonomyIndex:
                template: pages/list.html
                accepts:
                  kind: taxonomy_index
              taxonomyTerm:
                template: pages/list.html
                accepts:
                  kind: taxonomy_term
            """);
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "<main>home</main>");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "list.html"),
            "<main>{{ for p in pages }}{{ p.title }};{{ end }}</main>");
        return path;
    }

    private static Dictionary<string, byte[]> Published(string outputDir)
        => Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(outputDir, path).Split(Path.DirectorySeparatorChar).Contains(".bukit", StringComparer.Ordinal) &&
                !string.Equals(Path.GetFileName(path), ".bukit-build-state.json", StringComparison.Ordinal))
            .ToDictionary(path => Path.GetRelativePath(outputDir, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static void CopyPublished(string from, string to)
    {
        foreach (var (relative, bytes) in Published(from))
        {
            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, bytes);
        }
    }

    private sealed class I18nNotionApi : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue($"{request.Method} {path}");
            if (request.Method == HttpMethod.Get && path == $"/v1/databases/{DatabaseId}")
            {
                return Json("""
                    {"object":"database","properties":{
                      "Title":{"type":"title"},"Slug":{"type":"rich_text"},"Type":{"type":"select"},
                      "Language":{"type":"select"},"I18nKey":{"type":"rich_text"},"Related":{"type":"relation"},
                      "Tags":{"type":"multi_select","multi_select":{"options":[{"name":"News"},{"name":"Unused option"}]}}}}
                    """);
            }

            if (request.Method == HttpMethod.Post && path == $"/v1/databases/{DatabaseId}/query")
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                return body.Contains("\"start_cursor\":\"next\"", StringComparison.Ordinal)
                    ? Json($$"""{"object":"list","has_more":false,"next_cursor":null,"results":[{{Page("zh", "zh-CN")}}]}""")
                    : Json($$"""{"object":"list","has_more":true,"next_cursor":"next","results":[{{Page("en", "en")}}]}""");
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/v1/blocks/", StringComparison.Ordinal))
            {
                var id = path.Split('/')[3];
                return Json("{\"object\":\"list\",\"has_more\":false,\"results\":[{\"type\":\"paragraph\",\"paragraph\":{\"rich_text\":[{\"plain_text\":\"Body of " + id + "\"}]}}]}");
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/v1/pages/rel-", StringComparison.Ordinal))
            {
                var id = path["/v1/pages/".Length..];
                return Json("{\"object\":\"page\",\"id\":\"" + id + "\",\"url\":\"https://www.notion.so/" + id +
                    "\",\"properties\":{\"Name\":{\"type\":\"title\",\"title\":[{\"plain_text\":\"Related " + id + "\"}]}}}");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        }

        private static string Page(string suffix, string language) => $$$"""
            {"object":"page","id":"page-{{{suffix}}}","created_time":"2026-01-02T03:04:05.000Z","last_edited_time":"2026-05-15T12:00:00.000Z",
             "properties":{
               "Title":{"type":"title","title":[{"plain_text":"Intro {{{suffix}}}"}]},
               "Slug":{"type":"rich_text","rich_text":[{"plain_text":"intro-{{{suffix}}}"}]},
               "Type":{"type":"select","select":{"name":"page"}},
               "Language":{"type":"select","select":{"name":"{{{language}}}"}},
               "I18nKey":{"type":"rich_text","rich_text":[{"plain_text":"intro"}]},
               "Tags":{"type":"multi_select","multi_select":[{"name":"News"}]},
               "Related":{"type":"relation","relation":[{"id":"rel-{{{suffix}}}"}]}
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
