using System.Text.Json;
using Bukit.Notion.Client;
using Bukit.Notion.Push;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionPushCreateTests : IDisposable
{
    private const string SecretToken = "secret-token-should-not-appear";
    private readonly string _projectRoot;

    public NotionPushCreateTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-push-create-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PushAsync_CreateMode_CreatesPagesAndWritesReportWithoutSecret()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json");
        var client = new RecordingNotionClient();
        var factory = new RecordingNotionClientFactory(client);
        var tokenProvider = new DictionaryNotionTokenProvider(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NOTION_TOKEN"] = SecretToken
        });
        var service = new NotionPushService(factory, tokenProvider);

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Create,
            DryRun: false,
            ReportPath: reportPath,
            TokenEnvironmentVariable: "NOTION_TOKEN"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(SecretToken, factory.Options!.Token);
        Assert.Equal(2, client.CreateRequests.Count);
        using JsonDocument createBody = JsonDocument.Parse(client.CreateRequests[0].Json);
        JsonElement root = createBody.RootElement;
        Assert.Equal("ds-pages", root.GetProperty("parent").GetProperty("data_source_id").GetString());
        JsonElement properties = root.GetProperty("properties");
        Assert.Equal("Home", properties.GetProperty("Title").GetProperty("title")[0].GetProperty("text").GetProperty("content").GetString());
        Assert.Equal("home", properties.GetProperty("Slug").GetProperty("rich_text")[0].GetProperty("text").GetProperty("content").GetString());
        Assert.True(properties.GetProperty("Published").GetProperty("checkbox").GetBoolean());

        string report = File.ReadAllText(reportPath);
        Assert.DoesNotContain(SecretToken, report, StringComparison.Ordinal);
        using JsonDocument reportJson = JsonDocument.Parse(report);
        Assert.False(reportJson.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.Equal(2, reportJson.RootElement.GetProperty("plannedCreate").GetInt32());
    }

    [Fact]
    public async Task PushAsync_CreateMode_RejectsNonAllowlistedTokenEnvironmentVariable()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        var client = new RecordingNotionClient();
        var service = new NotionPushService(
            new RecordingNotionClientFactory(client),
            new DictionaryNotionTokenProvider(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NOT_ALLOWED"] = SecretToken
            }));

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Create,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, "report.json"),
            TokenEnvironmentVariable: "NOT_ALLOWED"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("notion.tokenEnvNotAllowed", Assert.Single(result.Diagnostics).Code);
        Assert.Empty(client.CreateRequests);
    }

    [Fact]
    public async Task PushAsync_CreateMode_MissingTokenFailsWithoutCallingNotion()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        var client = new RecordingNotionClient();
        var service = new NotionPushService(
            new RecordingNotionClientFactory(client),
            new DictionaryNotionTokenProvider(new Dictionary<string, string>(StringComparer.Ordinal)));

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Create,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, "report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("notion.tokenMissing", Assert.Single(result.Diagnostics).Code);
        Assert.Empty(client.CreateRequests);
    }

    [Fact]
    public async Task PushAsync_ReplaceMode_RequiresConfirmation()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        var service = new NotionPushService(
            new RecordingNotionClientFactory(new RecordingNotionClient()),
            new DictionaryNotionTokenProvider(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NOTION_TOKEN"] = SecretToken
            }));

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Replace,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, "report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("notion.replaceRequiresConfirmation", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task PushAsync_CreateMode_MapsHttpFailureWithoutLeakingToken()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        var client = new RecordingNotionClient
        {
            CreateException = new HttpRequestException("network failed for secret-token-should-not-appear")
        };
        var service = new NotionPushService(
            new RecordingNotionClientFactory(client),
            new DictionaryNotionTokenProvider(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NOTION_TOKEN"] = SecretToken
            }));

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Create,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, "report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN"), CancellationToken.None);

        NotionPushDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.Success);
        Assert.Equal("notion.httpError", diagnostic.Code);
        Assert.DoesNotContain(SecretToken, diagnostic.Message, StringComparison.Ordinal);
    }

    private (string SeedDir, string MapPath) WriteValidHandoff()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "notion-seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "slug": "home",
    "published": true
  },
  {
    "title": "About",
    "slug": "about",
    "published": false
  }
]
""");
        string mapPath = Path.Combine(seedDir, "notion-database-map.yaml");
        File.WriteAllText(mapPath, """
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds-pages
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
      Published:
        source: published
        type: checkbox
""");
        return (seedDir, mapPath);
    }

    private sealed class RecordingNotionClientFactory : INotionClientFactory
    {
        private readonly INotionClient _client;

        public RecordingNotionClientFactory(INotionClient client)
        {
            _client = client;
        }

        public NotionRequestOptions? Options { get; private set; }

        public INotionClient Create(NotionRequestOptions options)
        {
            Options = options;
            return _client;
        }
    }

    private sealed class DictionaryNotionTokenProvider : INotionTokenProvider
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public DictionaryNotionTokenProvider(IReadOnlyDictionary<string, string> values)
        {
            _values = values;
        }

        public string? GetToken(string environmentVariable)
            => _values.TryGetValue(environmentVariable, out string? value) ? value : null;
    }

    private sealed class RecordingNotionClient : INotionClient
    {
        public List<NotionCreatePageRequest> CreateRequests { get; } = [];

        public Exception? CreateException { get; init; }

        public Task<NotionQueryResult> QueryDataSourceAsync(
            string dataSourceId,
            NotionQueryRequest request,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<NotionPageResult> CreatePageAsync(
            NotionCreatePageRequest request,
            CancellationToken cancellationToken)
        {
            if (CreateException is not null)
            {
                throw CreateException;
            }

            CreateRequests.Add(request);
            return Task.FromResult(new NotionPageResult("page-" + CreateRequests.Count.ToString("D2"), """{"id":"page"}"""));
        }

        public Task<NotionPageResult> UpdatePagePropertiesAsync(
            string pageId,
            NotionUpdatePageRequest request,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AppendBlockChildrenAsync(
            string blockId,
            IReadOnlyList<NotionBlock> children,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<NotionBlockResult>> ListBlockChildrenAsync(
            string blockId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task DeleteBlockAsync(
            string blockId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
