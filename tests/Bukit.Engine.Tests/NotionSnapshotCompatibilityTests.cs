using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Content.Notion;
using Bukit.Content.Notion.Snapshot;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Notion.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

/// <summary>
/// The committed format-1 Notion snapshot must stay readable as Bukit evolves. The fixture is
/// hand-written raw Notion data (<c>api/</c>) plus one minimal site; <c>snapshot/</c> is recorded
/// from it once and is never regenerated to hide a format change.
/// </summary>
public sealed class NotionSnapshotCompatibilityTests : IDisposable
{
    private const string FixtureToken = "secret_notion_snapshot_fixture_token";
    private const string FixtureBukitVersion = "2.0.0";
    private const string FixtureCreatedAt = "2026-06-01T10:00:00Z";
    private const string GenerateVariable = "BUKIT_GENERATE_SNAPSHOT_FIXTURE";
    private static readonly Regex s_notionBody = new(
        "<!--notion-body-->.*?<!--/notion-body-->",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "bukit-notion-snapshot-compat-" + Guid.NewGuid().ToString("N"));

    public NotionSnapshotCompatibilityTests() => Directory.CreateDirectory(_root);

    public void Dispose() => TestCleanup.DeleteDirectory(_root);

    private static string FixtureRoot => Path.Combine(
        FindRepoRoot(),
        "tests",
        "fixtures",
        "notion-snapshot-format-1");

    [Fact]
    public void Fixture_OpensWithAnotherBukitVersion()
    {
        var snapshot = CopyToTemp(Path.Combine(FixtureRoot, "snapshot"), "snapshot");

        using var offline = NotionSnapshotSession.OpenOffline(snapshot);

        Assert.NotNull(offline.Manifest);
        Assert.Equal(1, offline.Manifest!.Format);
        Assert.NotEqual(NotionSnapshotSession.BukitVersion, offline.Manifest.BukitVersion);
    }

    [Fact]
    public async Task OfflineBuild_MatchesOnlineBuild_AndMakesNoRequest()
    {
        var fixture = FixtureRoot;
        var onlineSite = CopyToTemp(Path.Combine(fixture, "site"), "online-site");
        var offlineSite = CopyToTemp(Path.Combine(fixture, "site"), "offline-site");
        var snapshotCopy = CopyToTemp(Path.Combine(fixture, "snapshot"), "snapshot-copy");
        var originalToken = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, FixtureToken);

            var onlineApi = new DirectoryNotionApi(Path.Combine(fixture, "api"));
            await BuildAsync(onlineSite, snapshot: null, onlineApi);
            Assert.Empty(onlineApi.UnexpectedRequests);
            var expectedRequests = new[]
            {
                "GET /v1/databases/11111111-1111-4111-8111-111111111111",
                "POST /v1/databases/11111111-1111-4111-8111-111111111111/query",
                "GET /v1/databases/22222222-2222-4222-8222-222222222222",
                "POST /v1/databases/22222222-2222-4222-8222-222222222222/query",
                "GET /v1/pages/menu-1",
                "GET /v1/pages/missing-person",
                "GET /v1/blocks/article-1/children"
            };
            Assert.Equal(
                expectedRequests.Order(StringComparer.Ordinal),
                onlineApi.Requests.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            Assert.Contains(
                onlineApi.QueryBodies,
                static body => body.Contains("\"Published\"", StringComparison.Ordinal) &&
                    body.Contains("\"equals\":true", StringComparison.Ordinal));

            var offlineApi = new DirectoryNotionApi(Path.Combine(fixture, "api"));
            TestLogger offlineLogger;
            using (var snapshot = NotionSnapshotSession.OpenOffline(snapshotCopy))
            {
                offlineLogger = await BuildAsync(offlineSite, snapshot, offlineApi);
            }

            Assert.Empty(offlineApi.Requests);
            Assert.Empty(offlineApi.UnexpectedRequests);

            var onlineOutput = Path.Combine(onlineSite, "dist");
            var offlineOutput = Path.Combine(offlineSite, "dist");
            var online = OutputSnapshot.Public(onlineOutput, "en");
            var offline = OutputSnapshot.Public(offlineOutput, "en");
            Assert.Equal(online.Entries.Keys, offline.Entries.Keys);

            var expectedMismatch = ReadFixtureRenderer(snapshotCopy) != NotionBlocksRenderer.ContractVersion;
            var actualMismatch = offlineLogger.Warnings.Any(
                static message => message.Contains("event=notion.cache.renderer_mismatch", StringComparison.Ordinal));
            Assert.Equal(expectedMismatch, actualMismatch);

            foreach (var key in online.Entries.Keys)
            {
                var expected = online.Entries[key].Bytes;
                var actual = offline.Entries[key].Bytes;
                if (!expectedMismatch)
                {
                    Assert.True(
                        expected.AsSpan().SequenceEqual(actual),
                        $"Offline output differs: {key}");
                    continue;
                }

                // The content projections and the search index embed the rendered body; only the
                // HTML pages can be compared after removing the body when the renderer differs.
                if (key.StartsWith("content/", StringComparison.Ordinal) ||
                    string.Equals(key, "search.json", StringComparison.Ordinal))
                {
                    continue;
                }

                var expectedText = Encoding.UTF8.GetString(expected);
                var actualText = Encoding.UTF8.GetString(actual);
                if (!expectedText.Contains("<!--notion-body-->", StringComparison.Ordinal))
                {
                    Assert.True(
                        expected.AsSpan().SequenceEqual(actual),
                        $"Offline output differs: {key}");
                    continue;
                }

                Assert.Equal(
                    s_notionBody.Replace(expectedText, string.Empty),
                    s_notionBody.Replace(actualText, string.Empty));
            }

            // The draft page exists in api/pages/article-2.json, but the Published query filter
            // excludes it; neither the snapshot nor the published output can contain it.
            Assert.DoesNotContain(online.Entries.Keys, static key => key.Contains("draft-two", StringComparison.Ordinal));
            var queryRequests = Directory.GetFiles(
                snapshotCopy,
                "request.json",
                SearchOption.AllDirectories);
            Assert.Contains(queryRequests, path => File.ReadAllText(path).Contains("\"Published\"", StringComparison.Ordinal));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(snapshotCopy, "*", SearchOption.AllDirectories),
                path => File.ReadAllText(path).Contains("draft-two", StringComparison.Ordinal));

            // The missing relation target is the expected 404; the fetched menu page is the
            // successful pages_index target. The cross-source author is rendered as well.
            Assert.Contains("GET /v1/pages/missing-person", onlineApi.Requests);
            Assert.Contains("Menu One", File.ReadAllText(Path.Combine(offlineOutput, "index.html")), StringComparison.Ordinal);
            Assert.Contains("Person One", File.ReadAllText(Path.Combine(offlineOutput, "posts", "article-one", "index.html")), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, originalToken);
        }
    }

    [Fact]
    public async Task GeneratesTheFixtureSnapshot_WhenRequested()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(GenerateVariable), "1", StringComparison.Ordinal))
        {
            return;
        }

        var fixture = FixtureRoot;
        var site = CopyToTemp(Path.Combine(fixture, "site"), "generate-site");
        var generated = Path.Combine(fixture, ".generated-snapshot-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(fixture, ".snapshot-backup-" + Guid.NewGuid().ToString("N"));
        var originalToken = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, FixtureToken);
            using (var recording = NotionSnapshotSession.BeginWrite(generated))
            {
                await BuildAsync(site, recording, new DirectoryNotionApi(Path.Combine(fixture, "api")));
                recording.Commit();
            }

            RewriteManifest(Path.Combine(generated, "manifest.json"));
            var target = Path.Combine(fixture, "snapshot");
            if (Directory.Exists(target))
            {
                Directory.Move(target, backup);
            }

            try
            {
                Directory.Move(generated, target);
            }
            catch
            {
                if (Directory.Exists(backup))
                {
                    Directory.Move(backup, target);
                }

                throw;
            }

            // The old fixture is now safely replaced; only the successful path removes its backup.
            TestCleanup.DeleteDirectory(backup);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, originalToken);
            TestCleanup.DeleteDirectory(generated);
            // A failed restore keeps the backup as the only copy of the old fixture.
        }
    }

    private static async Task<TestLogger> BuildAsync(
        string siteRoot,
        NotionSnapshotSession? snapshot,
        DirectoryNotionApi api)
    {
        var config = ConfigLoader.Load(Path.Combine(siteRoot, "site.yaml"));
        var logger = new TestLogger();
        var previous = NotionContentClient.TransportOverride.Value;
        NotionContentClient.TransportOverride.Value = () => api;
        try
        {
            using var scope = snapshot is null ? null : NotionSnapshotScope.Enter(snapshot);
            await new SiteEngine(logger).BuildAsync(
                config,
                siteRoot,
                new ConfigOverrides { Clean = true, Incremental = false });
        }
        finally
        {
            NotionContentClient.TransportOverride.Value = previous;
        }

        return logger;
    }

    private string CopyToTemp(string source, string name)
    {
        var target = Path.Combine(_root, name);
        CopyDirectory(source, target);
        return target;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static int ReadFixtureRenderer(string snapshotDirectory)
    {
        var body = Path.Combine(snapshotDirectory, "cache", "pages", "article-1.json");
        Assert.True(File.Exists(body), body);
        using var document = JsonDocument.Parse(File.ReadAllText(body));
        return document.RootElement.GetProperty("renderer").GetInt32();
    }

    private static void RewriteManifest(string path)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        manifest["bukitVersion"] = FixtureBukitVersion;
        manifest["createdAt"] = FixtureCreatedAt;
        File.WriteAllText(
            path,
            manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Bukit repository root was not found.");
    }

    private sealed class TestLogger : ILogger
    {
        public ConcurrentQueue<string> Infos { get; } = new();
        public ConcurrentQueue<string> Warnings { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) => Infos.Enqueue(message);
        public void Warn(string message) => Warnings.Enqueue(message);
        public void Error(string message) { }
    }

    /// <summary>
    /// Serves the fixture's hand-written raw responses. An unknown path is a fixture defect: record
    /// it and fail instead of returning 404, which a relation resolver could mistake for a real 404.
    /// </summary>
    private sealed class DirectoryNotionApi(string apiRoot) : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();
        public ConcurrentQueue<string> QueryBodies { get; } = new();
        public ConcurrentQueue<string> UnexpectedRequests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue($"{request.Method} {path}");
            if (request.Method == HttpMethod.Get &&
                TryGetId(path, "/v1/databases/", out var databaseId))
            {
                return File(Path.Combine(apiRoot, "databases", databaseId, "schema.json"));
            }

            if (request.Method == HttpMethod.Post &&
                TryGetId(path, "/v1/databases/", out databaseId) &&
                path.EndsWith("/query", StringComparison.Ordinal))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                QueryBodies.Enqueue(body);
                var cursor = ReadCursor(body);
                var name = string.IsNullOrEmpty(cursor) ? "first" : cursor;
                return File(Path.Combine(apiRoot, "databases", databaseId, "queries", name + ".json"));
            }

            if (request.Method == HttpMethod.Get &&
                TryGetId(path, "/v1/pages/", out var pageId))
            {
                var missing = Path.Combine(apiRoot, "pages", pageId + ".404.json");
                return System.IO.File.Exists(missing) ? NotFound() : File(Path.Combine(apiRoot, "pages", pageId + ".json"));
            }

            if (request.Method == HttpMethod.Get &&
                path.StartsWith("/v1/blocks/", StringComparison.Ordinal) &&
                path.EndsWith("/children", StringComparison.Ordinal))
            {
                var blockId = path["/v1/blocks/".Length..^"/children".Length];
                return File(Path.Combine(apiRoot, "blocks", blockId + ".json"));
            }

            UnexpectedRequests.Enqueue($"{request.Method} {path}");
            throw new InvalidOperationException($"The Notion snapshot fixture has no response for {request.Method} {path}.");
        }

        private static bool TryGetId(string path, string prefix, out string id)
        {
            id = string.Empty;
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var rest = path[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                rest = rest[..slash];
            }

            if (string.IsNullOrWhiteSpace(rest))
            {
                return false;
            }

            id = Uri.UnescapeDataString(rest);
            return true;
        }

        private static string? ReadCursor(string body)
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("start_cursor", out var cursor) &&
                cursor.ValueKind == JsonValueKind.String
                ? cursor.GetString()
                : null;
        }

        private HttpResponseMessage File(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                UnexpectedRequests.Enqueue($"missing fixture response: {path}");
                throw new InvalidOperationException($"The Notion snapshot fixture is missing {path}.");
            }

            return Json(System.IO.File.ReadAllText(path));
        }

        private static HttpResponseMessage Json(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            response.Headers.Date = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            return response;
        }

        private static HttpResponseMessage NotFound()
            => new(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    }
}
