using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using Bukit.Cli.Commands;
using Bukit.Content.Notion;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

/// <summary>
/// A multilingual site builds each language in its own worker process. An offline build must
/// give every worker the parent's snapshot, need no token, and match the recorded build; a
/// snapshot that lacks one language's data fails the build instead of skipping it. Recording
/// through real worker processes would reach the real Notion API (the client only talks to
/// api.notion.com), so the snapshot is recorded in-process here and recording through the
/// worker path is covered by <c>I18nWorktreeNotionSnapshotTests</c> in Bukit.Engine.Tests.
/// </summary>
[Collection("Console")]
public sealed class NotionOfflineSnapshotMultilingualTests : IDisposable
{
    private const string Token = "secret_i18n_cli_snapshot_token_4b7e";
    private const string DatabaseId = "7c0d0000-0000-4000-8000-0000000018cc";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-notion-i18n-cli-" + Guid.NewGuid().ToString("N"));
    private readonly string _snapshotRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-i18n-cli-snap-" + Guid.NewGuid().ToString("N"));
    private readonly string? _originalToken = Environment.GetEnvironmentVariable(EnvironmentHelper.NotionTokenKey);

    private string SnapshotDir => Path.Combine(_snapshotRoot, "notion");

    private string ConfigPath => Path.Combine(_root, "site.yaml");

    public void Dispose()
    {
        NotionContentClient.TransportOverride.Value = null;
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, _originalToken);
        TestCleanup.DeleteDirectory(_root, recursive: true);
        TestCleanup.DeleteDirectory(_snapshotRoot, recursive: true);
    }

    [Fact]
    public async Task OfflineWorktreeBuild_EveryLanguageWorkerReadsTheSnapshot_AndMatchesTheRecordedBuild()
    {
        CreateSite();
        await InitializeRepositoryAsync(_root);
        var recorded = await RecordAsync();

        var offline = await RunBukitAsync("build", "--config", ConfigPath, "--notion-snapshot", SnapshotDir, "--notion-offline");

        Assert.True(offline.ExitCode == 0, offline.Output);
        Assert.Contains("event=notion.snapshot.offline", offline.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("NOTION_TOKEN", offline.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(_snapshotRoot, offline.Output, StringComparison.Ordinal);
        var published = Published(Path.Combine(_root, "dist"));
        Assert.Equal(recorded.Keys.Order(StringComparer.Ordinal), published.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, bytes) in recorded)
        {
            Assert.True(bytes.AsSpan().SequenceEqual(published[path]), $"Offline output differs: {path}");
        }

        // The zh-CN worker resolved its relation from the snapshot (pages_index).
        var zh = Encoding.UTF8.GetString(published[Path.Combine("zh-CN", "intro-zh", "index.html")]);
        Assert.Contains("Body of page-zh", zh, StringComparison.Ordinal);
        Assert.Contains("<aside>Related rel-zh;</aside>", zh, StringComparison.Ordinal);
        Assert.Single(Directory.GetFileSystemEntries(_snapshotRoot));
    }

    [Fact]
    public async Task OfflineWorktreeBuild_ALanguageMissingFromTheSnapshot_FailsAndKeepsThePublishedSite()
    {
        CreateSite();
        await InitializeRepositoryAsync(_root);
        var recorded = await RecordAsync();

        // Only the zh-CN pages relate to this page; its worker needs it from the snapshot.
        File.Delete(Path.Combine(SnapshotDir, "api", "pages", "relzh.json"));
        var offline = await RunBukitAsync("build", "--config", ConfigPath, "--notion-snapshot", SnapshotDir, "--notion-offline");

        Assert.NotEqual(0, offline.ExitCode);
        Assert.Contains("Notion offline snapshot has no page 'rel-zh'", offline.Output, StringComparison.Ordinal);
        Assert.Contains("Worker for language 'zh-CN'", offline.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(_snapshotRoot, offline.Output, StringComparison.Ordinal);
        // Nothing was published: the recorded site is untouched.
        var published = Published(Path.Combine(_root, "dist"));
        Assert.Equal(recorded.Keys.Order(StringComparer.Ordinal), published.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>Records the snapshot with an in-process build and returns its published output.</summary>
    private async Task<Dictionary<string, byte[]>> RecordAsync()
    {
        Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, Token);
        var api = new I18nNotionApi();
        NotionContentClient.TransportOverride.Value = () => api;
        try
        {
            var code = await BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
                ["build", "--config", ConfigPath, "--notion-snapshot", SnapshotDir]));
            Assert.Equal(0, code);
        }
        finally
        {
            NotionContentClient.TransportOverride.Value = null;
            Environment.SetEnvironmentVariable(EnvironmentHelper.NotionTokenKey, _originalToken);
        }

        Assert.Contains("GET /v1/pages/rel-en", api.Requests);
        Assert.Contains("GET /v1/pages/rel-zh", api.Requests);
        Assert.True(File.Exists(Path.Combine(SnapshotDir, "manifest.json")));
        return Published(Path.Combine(_root, "dist"));
    }

    private void CreateSite()
    {
        Directory.CreateDirectory(Path.Combine(_root, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(_root, "layouts", "pages"));
        File.WriteAllText(Path.Combine(_root, ".gitignore"), "/dist/\n/.cache/\n");
        File.WriteAllText(ConfigPath, $$"""
            site:
              name: i18n-notion-cli-test
              title: I18N Notion CLI Test
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
                    pageSize: 1
                    fieldPolicy:
                      mode: all
              media:
                downloadToLocal: false
            build:
              output: dist
              clean: false
              # Serialize workers, as the other worktree tests do.
              languageJobs: 1
            theme:
              layouts: layouts
              params:
                pages_index:
                  resolve_notion:
                    enabled: true
                    field_keys: [related]
            """);
        File.WriteAllText(Path.Combine(_root, "layouts", "theme.yaml"), """
            name: i18n-notion-cli-test
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
        File.WriteAllText(Path.Combine(_root, "layouts", "layouts", "base.html"),
            "<!doctype html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>");
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "page.html"),
            "<main>{{ page.content }}<p>{{ page.fields.tags.value | array.join \",\" }}</p>" +
            "<aside>{{ for id in page.fields.related.value }}{{ site.data.pages_by_id[id].title }};{{ end }}</aside></main>");
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "index.html"), "<main>home</main>");
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "list.html"), "<main>{{ for p in pages }}{{ p.title }};{{ end }}</main>");
    }

    private static Dictionary<string, byte[]> Published(string outputDir)
        => Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(outputDir, path).Split(Path.DirectorySeparatorChar).Contains(".bukit", StringComparer.Ordinal) &&
                !string.Equals(Path.GetFileName(path), ".bukit-build-state.json", StringComparison.Ordinal))
            .ToDictionary(path => Path.GetRelativePath(outputDir, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static async Task InitializeRepositoryAsync(string root)
    {
        foreach (var arguments in new[]
                 {
                     new[] { "init" },
                     ["config", "user.email", "bukit-tests@example.test"],
                     ["config", "user.name", "Bukit Tests"],
                     ["add", "."],
                     ["commit", "-m", "fixture"]
                 })
        {
            var result = await RunAsync("git", arguments, root);
            Assert.True(result.ExitCode == 0, result.Output);
        }
    }

    /// <summary>Runs the real CLI, whose language workers are child processes, without a token.</summary>
    private Task<(int ExitCode, string Output)> RunBukitAsync(params string[] arguments)
        => RunAsync("dotnet", [typeof(BuildCommand).Assembly.Location, .. arguments], _root);

    private static async Task<(int ExitCode, string Output)> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment.Remove(EnvironmentHelper.NotionTokenKey);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout + await stderr);
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
