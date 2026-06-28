using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bukit.Notion.Client;

public sealed class NotionHttpClient : INotionClient
{
    private readonly HttpClient _httpClient;
    private readonly NotionRequestOptions _options;
    private readonly NotionRateLimitPolicy _rateLimitPolicy;

    public NotionHttpClient(HttpClient httpClient, NotionRequestOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        _rateLimitPolicy = new NotionRateLimitPolicy(options.MaxRetries);
        _httpClient.BaseAddress ??= options.EffectiveBaseUri;
    }

    public async Task<NotionDataSourceResult> RetrieveDataSourceAsync(
        string dataSourceId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            $"/v1/data_sources/{Uri.EscapeDataString(dataSourceId)}",
            json: null,
            cancellationToken).ConfigureAwait(false);
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string id = root.TryGetProperty("id", out JsonElement idElement)
            && idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString() ?? dataSourceId
                : dataSourceId;
        var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (root.TryGetProperty("properties", out JsonElement propertyObject)
            && propertyObject.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in propertyObject.EnumerateObject())
            {
                string? type = property.Value.TryGetProperty("type", out JsonElement typeElement)
                    && typeElement.ValueKind == JsonValueKind.String
                        ? typeElement.GetString()
                        : null;
                properties[property.Name] = type;
            }
        }

        return new NotionDataSourceResult(id, properties);
    }

    public async Task<NotionQueryResult> QueryDataSourceAsync(
        string dataSourceId,
        NotionQueryRequest request,
        CancellationToken cancellationToken)
    {
        var allIds = new List<string>();
        string? cursor = null;
        string lastJson = "{}";

        do
        {
            string json = cursor is null ? request.Json : WithStartCursor(request.Json, cursor);
            using HttpResponseMessage response = await SendAsync(
                HttpMethod.Post,
                $"/v1/data_sources/{Uri.EscapeDataString(dataSourceId)}/query",
                json,
                cancellationToken).ConfigureAwait(false);

            lastJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(lastJson);
            if (document.RootElement.TryGetProperty("results", out JsonElement results)
                && results.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("id", out JsonElement id)
                        && id.ValueKind == JsonValueKind.String)
                    {
                        allIds.Add(id.GetString() ?? string.Empty);
                    }
                }
            }

            bool hasMore = document.RootElement.TryGetProperty("has_more", out JsonElement hasMoreElement)
                && hasMoreElement.ValueKind is JsonValueKind.True;
            cursor = hasMore
                && document.RootElement.TryGetProperty("next_cursor", out JsonElement nextCursor)
                && nextCursor.ValueKind == JsonValueKind.String
                    ? nextCursor.GetString()
                    : null;
        }
        while (!string.IsNullOrWhiteSpace(cursor));

        return new NotionQueryResult(allIds, lastJson);
    }

    public async Task<NotionPageResult> CreatePageAsync(
        NotionCreatePageRequest request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "/v1/pages", request.Json, cancellationToken).ConfigureAwait(false);
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new NotionPageResult(ReadId(json), json);
    }

    public async Task<NotionPageResult> UpdatePagePropertiesAsync(
        string pageId,
        NotionUpdatePageRequest request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Patch,
            $"/v1/pages/{Uri.EscapeDataString(pageId)}",
            request.Json,
            cancellationToken).ConfigureAwait(false);
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new NotionPageResult(ReadId(json), json);
    }

    public async Task AppendBlockChildrenAsync(
        string blockId,
        IReadOnlyList<NotionBlock> children,
        CancellationToken cancellationToken)
    {
        string json = "{\"children\":[" + string.Join(",", children.Select(static child => child.Json)) + "]}";
        using HttpResponseMessage _ = await SendAsync(
            HttpMethod.Patch,
            $"/v1/blocks/{Uri.EscapeDataString(blockId)}/children",
            json,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotionBlockResult>> ListBlockChildrenAsync(
        string blockId,
        CancellationToken cancellationToken)
    {
        var blocks = new List<NotionBlockResult>();
        string? cursor = null;
        do
        {
            string path = $"/v1/blocks/{Uri.EscapeDataString(blockId)}/children";
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                path += $"?start_cursor={Uri.EscapeDataString(cursor)}";
            }

            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, path, json: null, cancellationToken).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("results", out JsonElement results)
                && results.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement result in results.EnumerateArray())
                {
                    blocks.Add(new NotionBlockResult(
                        result.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null,
                        result.GetRawText()));
                }
            }

            cursor = document.RootElement.TryGetProperty("has_more", out JsonElement hasMore)
                && hasMore.ValueKind is JsonValueKind.True
                && document.RootElement.TryGetProperty("next_cursor", out JsonElement nextCursor)
                && nextCursor.ValueKind == JsonValueKind.String
                    ? nextCursor.GetString()
                    : null;
        }
        while (!string.IsNullOrWhiteSpace(cursor));

        return blocks;
    }

    public async Task DeleteBlockAsync(
        string blockId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage _ = await SendAsync(
            HttpMethod.Delete,
            $"/v1/blocks/{Uri.EscapeDataString(blockId)}",
            json: null,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? json,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            using HttpRequestMessage request = CreateRequest(method, path, json);
            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (response.StatusCode == (HttpStatusCode)429 && _rateLimitPolicy.ShouldRetry(attempt))
            {
                TimeSpan delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.Zero;
                response.Dispose();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            HttpStatusCode statusCode = response.StatusCode;
            response.Dispose();
            throw new NotionApiException(statusCode, ReadErrorCode(body));
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? json)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        request.Headers.TryAddWithoutValidation("Notion-Version", _options.NotionVersion);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string WithStartCursor(string json, string cursor)
    {
        JsonNode? node = JsonNode.Parse(json);
        var obj = node as JsonObject ?? new JsonObject();
        obj["start_cursor"] = cursor;
        return obj.ToJsonString();
    }

    private static string? ReadId(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("id", out JsonElement id)
            && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
    }

    private static string? ReadErrorCode(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("code", out JsonElement code)
                && code.ValueKind == JsonValueKind.String
                    ? code.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
