using System.Text.Json;
using Bukit.Notion.Client;
using Bukit.Notion.Push;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionPushUpsertTests : IDisposable
{
    private const string SecretToken = "secret-token-should-not-appear";
    private readonly string _projectRoot;

    public NotionPushUpsertTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-push-upsert-" + Guid.NewGuid().ToString("N"));
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
    public async Task PushAsync_UpsertMode_UpdatesExistingPageAndCreatesMissingPage()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json");
        var client = new RecordingNotionClient();
        client.QueryResults["home"] = ["page-home"];
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
            Mode: NotionPushMode.Upsert,
            DryRun: false,
            ReportPath: reportPath,
            TokenEnvironmentVariable: "NOTION_TOKEN"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, client.QueryRequests.Count);
        Assert.Single(client.UpdateRequests);
        Assert.Equal("page-home", client.UpdateRequests[0].PageId);
        Assert.Single(client.CreateRequests);

        using JsonDocument query = JsonDocument.Parse(client.QueryRequests[0].Request.Json);
        JsonElement filter = query.RootElement.GetProperty("filter");
        Assert.Equal("Slug", filter.GetProperty("property").GetString());
        Assert.Equal("home", filter.GetProperty("rich_text").GetProperty("equals").GetString());

        string report = File.ReadAllText(reportPath);
        Assert.DoesNotContain(SecretToken, report, StringComparison.Ordinal);
        using JsonDocument reportJson = JsonDocument.Parse(report);
        Assert.False(reportJson.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.Equal(1, reportJson.RootElement.GetProperty("plannedCreate").GetInt32());
        Assert.Equal(1, reportJson.RootElement.GetProperty("plannedUpdate").GetInt32());
        Assert.Equal(0, reportJson.RootElement.GetProperty("plannedReplace").GetInt32());
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

        public INotionClient Create(NotionRequestOptions options)
            => _client;
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
        public Dictionary<string, IReadOnlyList<string>> QueryResults { get; } = new(StringComparer.Ordinal);

        public List<(string DataSourceId, NotionQueryRequest Request)> QueryRequests { get; } = [];

        public List<NotionCreatePageRequest> CreateRequests { get; } = [];

        public List<(string PageId, NotionUpdatePageRequest Request)> UpdateRequests { get; } = [];

        public Task<NotionQueryResult> QueryDataSourceAsync(
            string dataSourceId,
            NotionQueryRequest request,
            CancellationToken cancellationToken)
        {
            QueryRequests.Add((dataSourceId, request));
            using JsonDocument document = JsonDocument.Parse(request.Json);
            string uniqueValue = document.RootElement
                .GetProperty("filter")
                .GetProperty("rich_text")
                .GetProperty("equals")
                .GetString()!;
            return Task.FromResult(new NotionQueryResult(
                QueryResults.TryGetValue(uniqueValue, out IReadOnlyList<string>? ids) ? ids : [],
                "{}"));
        }

        public Task<NotionPageResult> CreatePageAsync(
            NotionCreatePageRequest request,
            CancellationToken cancellationToken)
        {
            CreateRequests.Add(request);
            return Task.FromResult(new NotionPageResult("page-new", """{"id":"page-new"}"""));
        }

        public Task<NotionPageResult> UpdatePagePropertiesAsync(
            string pageId,
            NotionUpdatePageRequest request,
            CancellationToken cancellationToken)
        {
            UpdateRequests.Add((pageId, request));
            return Task.FromResult(new NotionPageResult(pageId, """{"id":"page"}"""));
        }

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
