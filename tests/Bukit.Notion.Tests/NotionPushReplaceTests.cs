using System.Text.Json;
using Bukit.Notion.Client;
using Bukit.Notion.Push;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionPushReplaceTests : IDisposable
{
    private const string SecretToken = "secret-token-should-not-appear";
    private readonly string _projectRoot;

    public NotionPushReplaceTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-push-replace-" + Guid.NewGuid().ToString("N"));
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
    public async Task PushAsync_ReplaceMode_NoMatchFailsWithoutCreatingPage()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        var client = new RecordingNotionClient();
        var service = CreateService(client);

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Replace,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, "report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN",
            ConfirmReplace: true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("notion.replaceNoMatch", Assert.Single(result.Diagnostics).Code);
        Assert.Empty(client.CreateRequests);
        Assert.Empty(client.AppendRequests);
    }

    [Fact]
    public async Task PushAsync_ReplaceMode_UpdatesPropertiesDeletesChildrenAndAppendsBlocks()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        string reportPath = Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json");
        var client = new RecordingNotionClient();
        client.QueryResults["home"] = ["page-home"];
        client.BlockChildren["page-home"] = [new NotionBlockResult("block-old-1", "{}"), new NotionBlockResult("block-old-2", "{}")];
        var service = CreateService(client);

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Replace,
            DryRun: false,
            ReportPath: reportPath,
            TokenEnvironmentVariable: "NOTION_TOKEN",
            ConfirmReplace: true), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(client.UpdateRequests);
        Assert.Equal("page-home", client.UpdateRequests[0].PageId);
        Assert.Equal(["block-old-1", "block-old-2"], client.DeletedBlockIds);
        Assert.Single(client.AppendRequests);
        Assert.Equal("page-home", client.AppendRequests[0].BlockId);
        Assert.Contains("New body", client.AppendRequests[0].Children[0].Json, StringComparison.Ordinal);
        Assert.Empty(client.CreateRequests);

        string report = File.ReadAllText(reportPath);
        Assert.DoesNotContain(SecretToken, report, StringComparison.Ordinal);
        using JsonDocument reportJson = JsonDocument.Parse(report);
        Assert.False(reportJson.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.Equal(0, reportJson.RootElement.GetProperty("plannedCreate").GetInt32());
        Assert.Equal(0, reportJson.RootElement.GetProperty("plannedUpdate").GetInt32());
        Assert.Equal(1, reportJson.RootElement.GetProperty("plannedReplace").GetInt32());
    }

    [Fact]
    public async Task PushAsync_ReplaceMode_AppendsReplacementBlocksInApiSizedBatches()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        string content = string.Join("\n\n", Enumerable.Range(1, 101).Select(index => $"Paragraph {index}"));
        File.WriteAllText(
            Path.Combine(seedDir, "pages.json"),
            JsonSerializer.Serialize(new[]
            {
                new { title = "Home", slug = "home", published = true, content }
            }));
        var client = new RecordingNotionClient();
        client.QueryResults["home"] = ["page-home"];
        var service = CreateService(client);

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Replace,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, ".bukit", "reports", "plugin-output", "notion", "batch-report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN",
            ConfirmReplace: true), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, client.AppendRequests.Count);
        Assert.Equal(100, client.AppendRequests[0].Children.Count);
        Assert.Single(client.AppendRequests[1].Children);
    }

    [Fact]
    public async Task PushAsync_ReplaceMode_MultipleMatchesFails()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        var client = new RecordingNotionClient();
        client.QueryResults["home"] = ["page-home-1", "page-home-2"];
        var service = CreateService(client);

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Replace,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, "report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN",
            ConfirmReplace: true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("notion.replaceMultipleMatches", Assert.Single(result.Diagnostics).Code);
        Assert.Empty(client.UpdateRequests);
        Assert.Empty(client.AppendRequests);
    }

    [Fact]
    public async Task PushAsync_ReplaceMode_DeleteFailureStopsAppend()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        var client = new RecordingNotionClient
        {
            DeleteException = new HttpRequestException("delete failed")
        };
        client.QueryResults["home"] = ["page-home"];
        client.BlockChildren["page-home"] = [new NotionBlockResult("block-old-1", "{}")];
        var service = CreateService(client);

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Replace,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, "report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN",
            ConfirmReplace: true), CancellationToken.None);

        Assert.False(result.Success);
        NotionPushDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("notion.replaceDeleteFailed", diagnostic.Code);
        Assert.Contains("properties may have been updated", diagnostic.Message, StringComparison.Ordinal);
        NotionPushRecordResult failedRecord = Assert.Single(result.Records);
        Assert.Equal("failed", failedRecord.Status);
        Assert.Equal("page-home", failedRecord.RemotePageId);
        Assert.Equal("notion.replaceDeleteFailed", failedRecord.ErrorCode);
        Assert.Equal(diagnostic.Message, failedRecord.ErrorMessage);
        Assert.Empty(client.AppendRequests);

        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Path.Combine(_projectRoot, "report.json")));
        JsonElement reportRecord = Assert.Single(report.RootElement.GetProperty("records").EnumerateArray());
        Assert.Equal("failed", reportRecord.GetProperty("status").GetString());
        Assert.Equal("page-home", reportRecord.GetProperty("remotePageId").GetString());
    }

    [Fact]
    public async Task PushAsync_ReplaceMode_AppendFailureReportsReplaceAppendFailed()
    {
        (string seedDir, string mapPath) = WriteValidHandoff();
        var client = new RecordingNotionClient
        {
            AppendException = new HttpRequestException("append failed")
        };
        client.QueryResults["home"] = ["page-home"];
        var service = CreateService(client);

        NotionPushResult result = await service.PushAsync(new NotionPushOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            DatabaseMapPath: mapPath,
            Mode: NotionPushMode.Replace,
            DryRun: false,
            ReportPath: Path.Combine(_projectRoot, "report.json"),
            TokenEnvironmentVariable: "NOTION_TOKEN",
            ConfirmReplace: true), CancellationToken.None);

        Assert.False(result.Success);
        NotionPushDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("notion.replaceAppendFailed", diagnostic.Code);
        Assert.Contains("properties may have been updated", diagnostic.Message, StringComparison.Ordinal);
    }

    private NotionPushService CreateService(INotionClient client)
        => new(
            new RecordingNotionClientFactory(client),
            new DictionaryNotionTokenProvider(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NOTION_TOKEN"] = SecretToken
            }));

    private (string SeedDir, string MapPath) WriteValidHandoff()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "notion-seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Home",
    "slug": "home",
    "published": true,
    "content": "New body"
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

        public Dictionary<string, IReadOnlyList<NotionBlockResult>> BlockChildren { get; } = new(StringComparer.Ordinal);

        public List<NotionCreatePageRequest> CreateRequests { get; } = [];

        public List<(string PageId, NotionUpdatePageRequest Request)> UpdateRequests { get; } = [];

        public List<string> DeletedBlockIds { get; } = [];

        public List<(string BlockId, IReadOnlyList<NotionBlock> Children)> AppendRequests { get; } = [];

        public Exception? DeleteException { get; init; }

        public Exception? AppendException { get; init; }

        public Task<NotionQueryResult> QueryDataSourceAsync(
            string dataSourceId,
            NotionQueryRequest request,
            CancellationToken cancellationToken)
        {
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
            return Task.FromResult(new NotionPageResult("created", """{"id":"created"}"""));
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
        {
            if (AppendException is not null)
            {
                throw AppendException;
            }

            AppendRequests.Add((blockId, children));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<NotionBlockResult>> ListBlockChildrenAsync(
            string blockId,
            CancellationToken cancellationToken)
            => Task.FromResult(BlockChildren.TryGetValue(blockId, out IReadOnlyList<NotionBlockResult>? children)
                ? children
                : []);

        public Task DeleteBlockAsync(
            string blockId,
            CancellationToken cancellationToken)
        {
            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            DeletedBlockIds.Add(blockId);
            return Task.CompletedTask;
        }
    }
}
