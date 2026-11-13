using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Bukit.Cli.Commands;
using Bukit.Content.Notion;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

/// <summary>
/// <c>--notion-snapshot</c> / <c>--notion-offline</c> on build, config check and doctor:
/// a build with a token records the snapshot; afterwards all three pass with no token and
/// no Notion request.
/// </summary>
[Collection("Console")]
public sealed class NotionOfflineSnapshotCommandTests : IDisposable
{
    private const string DatabaseId = "7c0d0000-0000-4000-8000-0000000000cc";
    private const string Token = "secret_cli_snapshot_token_93be";
    private readonly string _rootDir = Path.Combine(Path.GetTempPath(), "bukit-notion-offline-cli-" + Guid.NewGuid().ToString("N"));
    private readonly string _snapshotRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-offline-cli-snap-" + Guid.NewGuid().ToString("N"));
    private readonly string _configPath;
    private readonly string? _originalToken = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);

    public NotionOfflineSnapshotCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "layouts", "pages"));
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "page.html"), "<html><body>{{ page.title }}{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "index.html"), "<html><body>{{ for p in pages }}{{ p.title }}{{ end }}</body></html>");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "list.html"), "<html><body>{{ for p in pages }}{{ p.title }}{{ end }}</body></html>");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
            name: test
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
            """);
        _configPath = Path.Combine(_rootDir, "site.yaml");
        WriteConfig(DatabaseId);
    }

    private string SnapshotDir => Path.Combine(_snapshotRoot, "notion");

    public void Dispose()
    {
        NotionContentClient.TransportOverride.Value = null;
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, _originalToken);
        foreach (var directory in new[] { _rootDir, _snapshotRoot })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BuildConfigCheckAndDoctor_PassOfflineWithoutTokenOrRequests()
    {
        await RecordSnapshotAsync();
        var manifest = await File.ReadAllTextAsync(Path.Combine(SnapshotDir, "manifest.json"));

        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);
        var offlineApi = new FakeNotionApi();
        NotionContentClient.TransportOverride.Value = () => offlineApi;

        var (checkCode, checkOutput) = await CaptureAsync(() => ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config",
            ["config", "check", "--config", _configPath, "--notion-snapshot", SnapshotDir, "--notion-offline"])));
        Assert.True(checkCode == 0, checkOutput);
        Assert.Contains("notion: offline snapshot (format 1", checkOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(SnapshotDir, checkOutput, StringComparison.Ordinal);

        var (doctorCode, doctorOutput) = await CaptureAsync(() => DoctorCommand.RunAsync(CliTestHelper.CreateCommand("doctor",
            ["doctor", "--config", _configPath, "--notion-snapshot", SnapshotDir, "--notion-offline"])));
        Assert.True(doctorCode == 0, doctorOutput);
        Assert.Contains("Notion network checks skipped", doctorOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("NOTION_TOKEN", doctorOutput, StringComparison.Ordinal);

        Directory.Delete(Path.Combine(_rootDir, "dist"), recursive: true);
        var (buildCode, buildOutput) = await CaptureAsync(() => BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
            ["build", "--config", _configPath, "--notion-snapshot", SnapshotDir, "--notion-offline"])));
        Assert.True(buildCode == 0, buildOutput);
        Assert.Contains("Body of notion-page", await File.ReadAllTextAsync(Path.Combine(_rootDir, "dist", "pages", "notion-page", "index.html")), StringComparison.Ordinal);

        Assert.Empty(offlineApi.Requests);
        // Offline builds only read the snapshot.
        Assert.Equal(manifest, await File.ReadAllTextAsync(Path.Combine(SnapshotDir, "manifest.json")));
        Assert.False(Directory.Exists(Path.Combine(_rootDir, ".cache", "notion")));
    }

    [Fact]
    public async Task RecordingOverAnExistingBuild_StillKeepsEveryBody()
    {
        // The second recording runs where dist and the build manifest already exist, which
        // would let an incremental build skip unchanged pages and drop their bodies.
        await RecordSnapshotAsync();
        await RecordSnapshotAsync("--no-clean");

        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);
        var offlineApi = new FakeNotionApi();
        NotionContentClient.TransportOverride.Value = () => offlineApi;
        Directory.Delete(Path.Combine(_rootDir, "dist"), recursive: true);
        var (code, output) = await CaptureAsync(() => BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
            ["build", "--config", _configPath, "--notion-snapshot", SnapshotDir, "--notion-offline"])));

        Assert.True(code == 0, output);
        Assert.Contains("Body of notion-page", await File.ReadAllTextAsync(Path.Combine(_rootDir, "dist", "pages", "notion-page", "index.html")), StringComparison.Ordinal);
        Assert.DoesNotContain(_snapshotRoot, output, StringComparison.Ordinal);
        Assert.Empty(offlineApi.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dist")]
    public async Task SnapshotInsideTheSiteOrOutput_IsRejected(string area)
    {
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, Token);
        var inside = Path.Combine(_rootDir, area, "notion-snapshot");

        var error = await Assert.ThrowsAnyAsync<Exception>(() => BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
            ["build", "--config", _configPath, "--notion-snapshot", inside])));

        Assert.Contains("must point outside the site", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(inside));
    }

    [Fact]
    public async Task WithoutOffline_ChecksStillNeedTheToken()
    {
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);

        var (code, output) = await CaptureAsync(() => ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config",
            ["config", "check", "--config", _configPath])));

        Assert.Equal(1, code);
        Assert.Contains("NOTION_TOKEN", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangedDatabaseId_FailsConfigCheckDoctorAndBuildOffline()
    {
        await RecordSnapshotAsync();
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, null);
        WriteConfig("99990000-0000-4000-8000-000000000000");

        var (checkCode, checkOutput) = await CaptureAsync(() => ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config",
            ["config", "check", "--config", _configPath, "--notion-snapshot", SnapshotDir, "--notion-offline"])));
        Assert.Equal(1, checkCode);
        Assert.Contains("no data for database", checkOutput, StringComparison.Ordinal);

        var (doctorCode, doctorOutput) = await CaptureAsync(() => DoctorCommand.RunAsync(CliTestHelper.CreateCommand("doctor",
            ["doctor", "--config", _configPath, "--notion-snapshot", SnapshotDir, "--notion-offline"])));
        Assert.Equal(1, doctorCode);
        Assert.Contains("no data for database", doctorOutput, StringComparison.Ordinal);

        await Assert.ThrowsAsync<Bukit.Shared.NotionSnapshotException>(() => BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
            ["build", "--config", _configPath, "--notion-snapshot", SnapshotDir, "--notion-offline"])));
    }

    [Fact]
    public async Task OptionMisuse_IsAnArgumentError()
    {
        await Assert.ThrowsAsync<CommandArgumentException>(() => BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
            ["build", "--config", _configPath, "--notion-offline"])));
        await Assert.ThrowsAsync<CommandArgumentException>(() => ConfigCommand.RunAsync(CliTestHelper.CreateCommand("config",
            ["config", "check", "--config", _configPath, "--notion-snapshot", SnapshotDir])));
        var (missingCode, missingOutput) = await CaptureAsync(() => DoctorCommand.RunAsync(CliTestHelper.CreateCommand("doctor",
            ["doctor", "--config", _configPath, "--notion-snapshot", SnapshotDir, "--notion-offline"])));
        Assert.Equal(1, missingCode);
        Assert.Contains("snapshot not found", missingOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedRecordingBuild_LeavesNoSnapshot()
    {
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, Token);
        NotionContentClient.TransportOverride.Value = () => new FakeNotionApi { FailQueries = true };

        await Assert.ThrowsAnyAsync<Exception>(() => BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
            ["build", "--config", _configPath, "--notion-snapshot", SnapshotDir])));

        Assert.False(Directory.Exists(SnapshotDir));
        Assert.Empty(Directory.Exists(_snapshotRoot) ? Directory.GetFileSystemEntries(_snapshotRoot) : []);
    }

    private async Task RecordSnapshotAsync(params string[] extraArgs)
    {
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, Token);
        var api = new FakeNotionApi();
        NotionContentClient.TransportOverride.Value = () => api;
        var (code, output) = await CaptureAsync(() => BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
            ["build", "--config", _configPath, "--notion-snapshot", SnapshotDir, .. extraArgs])));
        Assert.True(code == 0, output);
        Assert.NotEmpty(api.Requests);
        Assert.DoesNotContain(_snapshotRoot, output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(SnapshotDir, "manifest.json")));
        foreach (var file in Directory.EnumerateFiles(SnapshotDir, "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(Token, await File.ReadAllTextAsync(file), StringComparison.Ordinal);
        }

        Assert.Single(Directory.GetFileSystemEntries(_snapshotRoot));
        NotionContentClient.TransportOverride.Value = null;
    }

    private void WriteConfig(string databaseId)
        => File.WriteAllText(_configPath, $"""
            site:
              name: test
              title: Test
              url: https://example.com
              collections:
                page:
                  permalink: /pages/{"{slug}"}/
                  template: pages/page.html
                  listRoute: /pages/
            content:
              sources:
                - type: notion
                  name: page
                  collection: page
                  notion:
                    databaseId: {databaseId}
                    filterType: none
                    cacheMode: readwrite
              media:
                downloadToLocal: false
            build:
              output: dist
              listPageContentMode: auto
            """);

    private static async Task<(int Code, string Output)> CaptureAsync(Func<Task<int>> run)
    {
        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var code = await run();
            return (code, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private sealed class FakeNotionApi : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();
        public bool FailQueries { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue($"{request.Method} {path}");
            if (request.Method == HttpMethod.Get && path == $"/v1/databases/{DatabaseId}")
            {
                return Json("""{"object":"database","properties":{"Title":{"type":"title"},"Slug":{"type":"rich_text"},"Tags":{"type":"multi_select","multi_select":{"options":[]}}}}""");
            }

            if (request.Method == HttpMethod.Post && path == $"/v1/databases/{DatabaseId}/query")
            {
                if (FailQueries)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{}") });
                }

                return Json("""
                    {"object":"list","has_more":false,"next_cursor":null,"results":[{
                      "object":"page","id":"notion-page","created_time":"2026-01-02T03:04:05.000Z","last_edited_time":"2026-05-15T12:00:00.000Z",
                      "properties":{
                        "Title":{"type":"title","title":[{"plain_text":"Notion Page"}]},
                        "Slug":{"type":"rich_text","rich_text":[{"plain_text":"notion-page"}]},
                        "Type":{"type":"select","select":{"name":"page"}}
                      }}]}
                    """);
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/v1/blocks/", StringComparison.Ordinal))
            {
                return Json("""{"object":"list","has_more":false,"results":[{"type":"paragraph","paragraph":{"rich_text":[{"plain_text":"Body of notion-page"}]}}]}""");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") });
        }

        private static Task<HttpResponseMessage> Json(string body)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            response.Headers.Date = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            return Task.FromResult(response);
        }
    }
}
