using System.Net;
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

    [Fact]
    public async Task ValidateAsync_MissingAndMismatchedProperties_EmitsGranularAndSummaryDiagnostics()
    {
        NotionRemoteSchemaValidationResult result = await ValidateWithRemotePropertiesAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Title"] = "title",
                ["Slug"] = "url"
            });

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.remoteSchemaPropertyTypeMismatch");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.remoteSchemaPropertyMissing");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.remoteSchemaValidationFailed");
        Assert.Equal(1, result.Diagnostics.Count(diagnostic => diagnostic.Code == "notion.remoteSchemaValidationFailed"));
    }

    [Theory]
    [InlineData(false, "notion.remoteSchemaTitleMissing")]
    [InlineData(true, "notion.remoteSchemaTitleNotUnique")]
    public async Task ValidateAsync_InvalidTitleCardinality_EmitsStableDiagnostic(
        bool duplicateTitle,
        string expectedCode)
    {
        var properties = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Slug"] = "rich_text",
            ["Published"] = "checkbox"
        };
        if (duplicateTitle)
        {
            properties["Title"] = "title";
            properties["Name"] = "title";
        }

        NotionRemoteSchemaValidationResult result = await ValidateWithRemotePropertiesAsync(properties);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
    }

    [Fact]
    public async Task ValidateAsync_MissingRemoteUniqueField_EmitsDedicatedDiagnostic()
    {
        NotionRemoteSchemaValidationResult result = await ValidateWithRemotePropertiesAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Title"] = "title",
                ["Published"] = "checkbox"
            });

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.remoteSchemaUniqueFieldMissing");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.remoteSchemaPropertyMissing");
    }

    [Fact]
    public async Task ValidateAsync_PropertyMatchingIsOrdinalAndExtraRemotePropertiesAreIgnored()
    {
        NotionRemoteSchemaValidationResult result = await ValidateWithRemotePropertiesAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Title"] = "title",
                ["slug"] = "rich_text",
                ["Published"] = "checkbox",
                ["Owner"] = "people"
            });

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.remoteSchemaPropertyMissing");
        Assert.DoesNotContain(result.DataSources[0].Properties, property => property.Name == "Owner");
    }

    [Fact]
    public async Task ValidateAsync_LegacyDatabaseId_UsesEffectiveIdentifierAndReportsSource()
    {
        string mapPath = WriteMap("databaseId: legacy-pages");
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal)
        {
            ["legacy-pages"] = new("legacy-pages", MatchingProperties())
        });
        var service = CreateService(client);

        NotionRemoteSchemaValidationResult result = await service.ValidateAsync(
            CreateOptions(mapPath),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["legacy-pages"], client.RetrievedIds);
        Assert.Equal("databaseId", Assert.Single(result.DataSources).IdentifierSource);
    }

    [Fact]
    public async Task ValidateAsync_MultipleEntries_AggregatesInOrdinalOrderAfterNotFound()
    {
        string mapPath = Path.Combine(_projectRoot, "multiple-map.yaml");
        File.WriteAllText(mapPath, """
        databases:
          b:
            seed: b.json
            collection: b
            dataSourceId: ds-b
            uniqueField: Slug
            properties:
              Title: { source: title, type: title }
              Slug: { source: slug, type: rich_text }
          a:
            seed: a.json
            collection: a
            dataSourceId: ds-a
            uniqueField: Slug
            properties:
              Title: { source: title, type: title }
              Slug: { source: slug, type: rich_text }
        """);
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal)
        {
            ["ds-b"] = new("ds-b", new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Title"] = "title",
                ["Slug"] = "rich_text"
            })
        });
        client.RetrieveExceptions["ds-a"] = new NotionApiException(HttpStatusCode.NotFound, "object_not_found");
        var service = CreateService(client);

        NotionRemoteSchemaValidationResult result = await service.ValidateAsync(
            CreateOptions(mapPath),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(["ds-a", "ds-b"], client.RetrievedIds);
        Assert.Equal(["a", "b"], result.DataSources.Select(item => item.Entry).ToArray());
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.remoteSchemaDataSourceNotFound");
        Assert.True(File.Exists(CreateOptions(mapPath).ReportPath));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "notion.apiUnauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "notion.apiForbidden")]
    [InlineData(HttpStatusCode.Conflict, "notion.apiConflict")]
    [InlineData((HttpStatusCode)429, "notion.rateLimited")]
    [InlineData(HttpStatusCode.InternalServerError, "notion.apiFailed")]
    public async Task ValidateAsync_RemoteApiFailure_MapsStableRuntimeDiagnostic(
        HttpStatusCode statusCode,
        string expectedCode)
    {
        string mapPath = WriteMap("dataSourceId: ds-pages");
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal));
        client.RetrieveExceptions["ds-pages"] = new NotionApiException(statusCode, "remote_error");
        var service = CreateService(client);

        NotionRemoteSchemaValidationResult result = await service.ValidateAsync(
            CreateOptions(mapPath),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == expectedCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.remoteSchemaValidationFailed");
    }

    [Fact]
    public async Task ValidateAsync_HttpFailure_MapsRuntimeDiagnosticWithoutToken()
    {
        string mapPath = WriteMap("dataSourceId: ds-pages");
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal));
        client.RetrieveExceptions["ds-pages"] = new HttpRequestException("request failed");
        var service = CreateService(client);

        NotionRemoteSchemaValidationResult result = await service.ValidateAsync(
            CreateOptions(mapPath),
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.httpError");
        Assert.DoesNotContain("secret-token", File.ReadAllText(CreateOptions(mapPath).ReportPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_InvalidMap_WritesFailureReportWithoutNetworkCall()
    {
        string mapPath = Path.Combine(_projectRoot, "invalid-map.yaml");
        File.WriteAllText(mapPath, "databases: [");
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal));
        var service = CreateService(client);
        NotionRemoteSchemaOptions options = CreateOptions(mapPath);

        NotionRemoteSchemaValidationResult result = await service.ValidateAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(client.RetrievedIds);
        Assert.True(File.Exists(options.ReportPath));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.databaseMapInvalidYaml");
    }

    [Fact]
    public async Task ValidateAsync_MissingToken_WritesFailureReportWithoutNetworkCall()
    {
        string mapPath = WriteMap("dataSourceId: ds-pages");
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal));
        var service = CreateService(client, token: null);
        NotionRemoteSchemaOptions options = CreateOptions(mapPath);

        NotionRemoteSchemaValidationResult result = await service.ValidateAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(client.RetrievedIds);
        Assert.True(File.Exists(options.ReportPath));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.tokenMissing");
    }

    [Fact]
    public async Task ValidateAsync_DisallowedTokenEnvironment_WritesFailureReportWithoutNetworkCall()
    {
        string mapPath = WriteMap("dataSourceId: ds-pages");
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal));
        var service = CreateService(client);
        NotionRemoteSchemaOptions options = CreateOptions(mapPath) with { TokenEnvironmentVariable = "OTHER_TOKEN" };

        NotionRemoteSchemaValidationResult result = await service.ValidateAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(client.RetrievedIds);
        Assert.True(File.Exists(options.ReportPath));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "notion.tokenEnvNotAllowed");
    }

    private NotionRemoteSchemaValidationService CreateService(
        INotionClient client,
        string? token = "secret-token")
        => new(new FakeNotionClientFactory(client), new FakeTokenProvider(token));

    private NotionRemoteSchemaOptions CreateOptions(string mapPath)
        => new(
            _projectRoot,
            mapPath,
            Path.Combine(_projectRoot, "reports", Path.GetFileNameWithoutExtension(mapPath) + ".json"),
            "NOTION_TOKEN");

    private static IReadOnlyDictionary<string, string?> MatchingProperties()
        => new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = "title",
            ["Slug"] = "rich_text",
            ["Published"] = "checkbox"
        };

    private async Task<NotionRemoteSchemaValidationResult> ValidateWithRemotePropertiesAsync(
        IReadOnlyDictionary<string, string?> properties)
    {
        string mapPath = WriteMap("dataSourceId: ds-pages");
        var client = new FakeNotionClient(new Dictionary<string, NotionDataSourceResult>(StringComparer.Ordinal)
        {
            ["ds-pages"] = new("ds-pages", properties)
        });
        var service = new NotionRemoteSchemaValidationService(
            new FakeNotionClientFactory(client),
            new FakeTokenProvider("secret-token"));
        string reportPath = Path.Combine(_projectRoot, "schema-validation.json");
        return await service.ValidateAsync(
            new NotionRemoteSchemaOptions(_projectRoot, mapPath, reportPath, "NOTION_TOKEN"),
            CancellationToken.None);
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

        public Dictionary<string, Exception> RetrieveExceptions { get; } = new(StringComparer.Ordinal);

        public Task<NotionDataSourceResult> RetrieveDataSourceAsync(
            string dataSourceId,
            CancellationToken cancellationToken)
        {
            RetrievedIds.Add(dataSourceId);
            if (RetrieveExceptions.TryGetValue(dataSourceId, out Exception? exception))
            {
                throw exception;
            }

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
