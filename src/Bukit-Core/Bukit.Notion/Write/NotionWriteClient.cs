using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bukit.Notion.Transport;

namespace Bukit.Notion.Write;

public sealed class NotionWriteClient
{
    private readonly NotionClient _client;

    public NotionWriteClient(NotionClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public Task<NotionWriteResult> QueryDatabaseAsync(
        string databaseId,
        string payload,
        CancellationToken cancellationToken = default)
        => SendJsonAsync(
            HttpMethod.Post,
            NotionApiUrls.DatabaseQuery(databaseId),
            payload,
            NotionRequestSemantics.IdempotentRead,
            cancellationToken);

    public Task<NotionWriteResult> InspectDatabaseSchemaAsync(
        string databaseId,
        CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            NotionApiUrls.Database(databaseId),
            payload: null,
            NotionRequestSemantics.IdempotentRead,
            cancellationToken);

    public Task<NotionWriteResult> CreateDatabaseAsync(
        string payload,
        CancellationToken cancellationToken = default)
        => SendJsonAsync(
            HttpMethod.Post,
            NotionApiUrls.Databases(),
            payload,
            NotionRequestSemantics.NonReplayableWrite,
            cancellationToken,
            includeUtf8Charset: true);

    public Task<NotionWriteResult> CreatePageAsync(
        string payload,
        CancellationToken cancellationToken = default)
        => SendJsonAsync(
            HttpMethod.Post,
            NotionApiUrls.Pages(),
            payload,
            NotionRequestSemantics.NonReplayableWrite,
            cancellationToken);

    public Task<NotionWriteResult> UpdatePageAsync(
        string pageId,
        string payload,
        CancellationToken cancellationToken = default)
        => SendJsonAsync(
            HttpMethod.Patch,
            NotionApiUrls.Pages(pageId),
            payload,
            NotionRequestSemantics.NonReplayableWrite,
            cancellationToken);

    public Task<NotionWriteResult> AppendBlockChildrenAsync(
        string blockId,
        string payload,
        CancellationToken cancellationToken = default)
        => SendJsonAsync(
            HttpMethod.Patch,
            NotionApiUrls.BlockChildrenEndpoint(blockId),
            payload,
            NotionRequestSemantics.NonReplayableWrite,
            cancellationToken);

    public Task<NotionWriteResult> ListBlockChildrenAsync(
        string blockId,
        string? startCursor = null,
        CancellationToken cancellationToken = default)
    {
        var url = NotionApiUrls.BlockChildren(blockId);
        if (!string.IsNullOrWhiteSpace(startCursor))
        {
            url += $"&start_cursor={Uri.EscapeDataString(startCursor)}";
        }

        return SendAsync(
            HttpMethod.Get,
            url,
            payload: null,
            NotionRequestSemantics.IdempotentRead,
            cancellationToken);
    }

    public Task<NotionWriteResult> ArchiveBlockAsync(
        string blockId,
        CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Delete,
            NotionApiUrls.Blocks(blockId),
            payload: null,
            NotionRequestSemantics.NonReplayableWrite,
            cancellationToken);

    private Task<NotionWriteResult> SendJsonAsync(
        HttpMethod method,
        string url,
        string payload,
        NotionRequestSemantics semantics,
        CancellationToken cancellationToken,
        bool includeUtf8Charset = false)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return SendAsync(method, url, payload, semantics, cancellationToken, includeUtf8Charset);
    }

    private async Task<NotionWriteResult> SendAsync(
        HttpMethod method,
        string url,
        string? payload,
        NotionRequestSemantics semantics,
        CancellationToken cancellationToken,
        bool includeUtf8Charset = false)
    {
        using var request = new HttpRequestMessage(method, url);
        if (payload is not null)
        {
            request.Content = includeUtf8Charset
                ? new StringContent(payload, Encoding.UTF8, "application/json")
                : new StringContent(payload);
            if (!includeUtf8Charset)
            {
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
        }

        try
        {
            using var document = await _client.SendAsync(request, semantics, cancellationToken);
            return NotionWriteResult.Success(document.RootElement.Clone());
        }
        catch (NotionApiException exception)
        {
            return NotionWriteResult.Failure(exception);
        }
    }
}
