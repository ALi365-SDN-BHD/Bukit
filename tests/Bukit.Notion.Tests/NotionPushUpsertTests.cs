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

    [Fact]
    public async Task PushAsync_UpsertMode_MultipleMatchesFailsWithoutUpdatingOrCreating()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "multiple-matches.json");
        var client = new RecordingNotionClient();
        client.QueryResults["home"] = ["page-home-1", "page-home-2"];
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

        Assert.False(result.Success);
        Assert.Equal("notion.upsertMultipleMatches", Assert.Single(result.Diagnostics).Code);
        Assert.Empty(client.UpdateRequests);
        Assert.Empty(client.CreateRequests);
        Assert.True(File.Exists(reportPath));
    }

    [Fact]
    public async Task PushAsync_UpsertMode_NumberUniqueFieldUsesNumberFilter()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "rank": 42
  }
]
""");
        File.WriteAllText(mapPath, """
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds-pages
    uniqueField: Rank
    properties:
      Title:
        source: title
        type: title
      Rank:
        source: rank
        type: number
""");
        var client = new RecordingNotionClient();
        client.QueryResults["42"] = ["page-42"];
        var service = CreateService(client);

        NotionPushResult result = await service.PushAsync(CreateOptions(seedDir, mapPath), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(client.UpdateRequests);
        using JsonDocument query = JsonDocument.Parse(Assert.Single(client.QueryRequests).Request.Json);
        Assert.Equal(42, query.RootElement.GetProperty("filter").GetProperty("number").GetProperty("equals").GetInt32());
    }

    [Fact]
    public async Task PushAsync_UpsertMode_CheckboxUniqueFieldUsesBooleanFilter()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "published": true
  }
]
""");
        File.WriteAllText(mapPath, """
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds-pages
    uniqueField: Published
    properties:
      Title:
        source: title
        type: title
      Published:
        source: published
        type: checkbox
""");
        var client = new RecordingNotionClient();
        client.QueryResults["true"] = ["page-published"];
        var service = CreateService(client);

        NotionPushResult result = await service.PushAsync(CreateOptions(seedDir, mapPath), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(client.UpdateRequests);
        using JsonDocument query = JsonDocument.Parse(Assert.Single(client.QueryRequests).Request.Json);
        Assert.True(query.RootElement.GetProperty("filter").GetProperty("checkbox").GetProperty("equals").GetBoolean());
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

    private NotionPushService CreateService(INotionClient client)
        => new(
            new RecordingNotionClientFactory(client),
            new DictionaryNotionTokenProvider(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NOTION_TOKEN"] = SecretToken
            }));

    private NotionPushOptions CreateOptions(string seedDir, string mapPath)
        => new(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Upsert,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "typed-unique-report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN");

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

        public Task<NotionDataSourceResult> RetrieveDataSourceAsync(
            string dataSourceId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<NotionQueryResult> QueryDataSourceAsync(
            string dataSourceId,
            NotionQueryRequest request,
            CancellationToken cancellationToken)
        {
            QueryRequests.Add((dataSourceId, request));
            using JsonDocument document = JsonDocument.Parse(request.Json);
            JsonElement filter = document.RootElement.GetProperty("filter");
            JsonProperty typedFilter = filter.EnumerateObject().Single(property => property.Name != "property");
            JsonElement equals = typedFilter.Value.GetProperty("equals");
            string uniqueValue = equals.ValueKind switch
            {
                JsonValueKind.String => equals.GetString()!,
                JsonValueKind.Number => equals.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => throw new InvalidOperationException("Unsupported test filter value.")
            };
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
