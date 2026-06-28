using Bukit.Notion.Client;
using Bukit.Notion.Push;
using Bukit.Notion.RemoteSchema;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionRemoteSchemaValidationTests : IDisposable
{
    private readonly string _projectRoot;

    public NotionRemoteSchemaValidationTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-remote-schema-" + Guid.NewGuid().ToString("N"));
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
    public async Task ValidateAsync_ExactRemoteSchema_WritesSuccessfulReports()
    {
        string mapPath = WriteMap("dataSourceId: ds-pages");
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal)
        {
            ["ds-pages"] = new("ds-pages", new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Title"] = "title",
                ["Slug"] = "rich_text",
                ["Published"] = "checkbox"
            })
        });
        var service = new NotionRemoteSchemaValidationService(
            new FakeNotionClientFactory(client),
            new FakeTokenProvider("secret-token"));
        string reportPath = Path.Combine(
            _projectRoot,
            ".bukit",
            "reports",
            "plugin-output",
            "notion",
            "schema.json");

        NotionRemoteSchemaValidationResult result = await service.ValidateAsync(
            new NotionRemoteSchemaOptions(_projectRoot, mapPath, reportPath, "NOTION_TOKEN"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["ds-pages"], client.RetrievedIds);
        NotionRemoteSchemaDataSourceResult dataSource = Assert.Single(result.DataSources);
        Assert.True(dataSource.Success);
        Assert.Equal("Title", dataSource.TitleProperty);
        Assert.All(dataSource.Properties, property => Assert.Equal("matched", property.Status));
        Assert.True(File.Exists(reportPath));
        Assert.True(File.Exists(Path.ChangeExtension(reportPath, ".md")));
    }

    private string WriteMap(string identifierLine)
    {
        string mapPath = Path.Combine(_projectRoot, "notion-database-map.yaml");
        File.WriteAllText(mapPath, $$"""
        databases:
          pages:
            seed: pages.json
            collection: page
            {{identifierLine}}
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
        return mapPath;
    }

    private sealed class FakeNotionClientFactory : INotionClientFactory
    {
        private readonly INotionClient _client;

        public FakeNotionClientFactory(INotionClient client)
        {
            _client = client;
        }

        public INotionClient Create(NotionRequestOptions options)
            => _client;
    }

    private sealed class FakeTokenProvider : INotionTokenProvider
    {
        private readonly string? _token;

        public FakeTokenProvider(string? token)
        {
            _token = token;
        }

        public string? GetToken(string environmentVariable)
            => _token;
    }

    private sealed class FakeNotionClient : INotionClient
    {
        private readonly IReadOnlyDictionary<string, NotionDataSourceResult> _dataSources;

        public FakeNotionClient(IReadOnlyDictionary<string, NotionDataSourceResult> dataSources)
        {
            _dataSources = dataSources;
        }

        public List<string> RetrievedIds { get; } = [];

        public Task<NotionDataSourceResult> RetrieveDataSourceAsync(
            string dataSourceId,
            CancellationToken cancellationToken)
        {
            RetrievedIds.Add(dataSourceId);
            return Task.FromResult(_dataSources[dataSourceId]);
        }

        public Task<NotionQueryResult> QueryDataSourceAsync(
            string dataSourceId,
            NotionQueryRequest request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Query is not used by remote schema validation tests.");

        public Task<NotionPageResult> CreatePageAsync(
            NotionCreatePageRequest request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Create is not used by remote schema validation tests.");

        public Task<NotionPageResult> UpdatePagePropertiesAsync(
            string pageId,
            NotionUpdatePageRequest request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Update is not used by remote schema validation tests.");

        public Task AppendBlockChildrenAsync(
            string blockId,
            IReadOnlyList<NotionBlock> children,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Append is not used by remote schema validation tests.");

        public Task<IReadOnlyList<NotionBlockResult>> ListBlockChildrenAsync(
            string blockId,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("List is not used by remote schema validation tests.");

        public Task DeleteBlockAsync(
            string blockId,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Delete is not used by remote schema validation tests.");
    }
}
