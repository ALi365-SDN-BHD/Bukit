using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion.Snapshot;
using Bukit.Notion;
using Bukit.Notion.Transport;
using Bukit.Shared;

namespace Bukit.Content.Notion;

/// <summary>
/// Notion read client for content loading. Online it wraps a <see cref="NotionClient"/>
/// and, when a snapshot is being recorded, stores every read answer in it. Offline it has
/// no <see cref="NotionClient"/> at all and answers only from the snapshot.
/// </summary>
internal sealed class NotionContentClient : IDisposable
{
    private readonly NotionClient? _client;
    private readonly NotionSnapshotSession? _snapshot;
    private readonly ILogger? _logger;

    /// <summary>
    /// Test seam: transport handler for clients created by <see cref="Create"/> in this async
    /// flow, so whole-build tests reach a fake Notion API instead of the network.
    /// </summary>
    internal static readonly AsyncLocal<Func<HttpMessageHandler>?> TransportOverride = new();

    internal NotionContentClient(NotionContentSourceOptions options)
        : this(new NotionClient(MapOptions(options)))
    {
    }

    internal NotionContentClient(NotionClient client)
        : this(client, recorder: null)
    {
    }

    internal NotionContentClient(NotionClient client, NotionSnapshotSession? recorder)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _snapshot = recorder is { Offline: false } ? recorder : null;
    }

    private NotionContentClient(NotionSnapshotSession offlineSnapshot, ILogger? logger)
    {
        _snapshot = offlineSnapshot;
        _logger = logger;
    }

    /// <summary>
    /// Creates the client for <paramref name="options"/>: offline snapshot reads, or the Notion
    /// API (recording into the snapshot when one is being written).
    /// </summary>
    internal static NotionContentClient Create(NotionContentSourceOptions options, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Snapshot is { Offline: true } offline)
        {
            return new NotionContentClient(offline, logger);
        }

        var transport = TransportOverride.Value is { } handlerFactory
            ? new NotionClient(MapOptions(options), handlerFactory(), static (_, _) => Task.CompletedTask, static () => DateTimeOffset.UtcNow)
            : new NotionClient(MapOptions(options));
        return new NotionContentClient(transport, options.Snapshot);
    }

    /// <summary>True when this client answers from the offline snapshot only.</summary>
    internal bool IsOffline => _client is null;

    internal NotionContentClient(
        NotionContentSourceOptions options,
        HttpMessageHandler handler,
        Func<int, CancellationToken, Task> delayAsync)
        : this(options, handler, delayAsync, static () => DateTimeOffset.UtcNow)
    {
    }

    internal NotionContentClient(
        NotionContentSourceOptions options,
        HttpMessageHandler handler,
        Func<int, CancellationToken, Task> delayAsync,
        Func<DateTimeOffset> utcNow)
        : this(new NotionClient(
            MapOptions(options),
            handler,
            delayAsync,
            utcNow))
    {
    }

    internal NotionContentClient(
        NotionContentSourceOptions options,
        HttpClient httpClient,
        Func<int, CancellationToken, Task> delayAsync)
        : this(new NotionClient(
            MapOptions(options),
            httpClient,
            delayAsync,
            static () => DateTimeOffset.UtcNow,
            ownsHttpClient: false,
            injectedTransport: true))
    {
    }

    /// <summary>The Notion API transport; offline clients have none.</summary>
    internal NotionClient Transport => _client ?? throw new NotionSnapshotException(
        "Notion offline builds do not call the Notion API; this data must come from the offline snapshot. " +
        NotionSnapshotException.RebuildHint);

    internal async Task<JsonDocument> PostAsync(
        string url,
        string json,
        CancellationToken cancellationToken)
        => (await PostWithServerTimeAsync(url, json, cancellationToken)).Document;

    /// <summary>Posts a read query and returns the body with Notion's response time.</summary>
    internal async Task<NotionResponse> PostWithServerTimeAsync(
        string url,
        string json,
        CancellationToken cancellationToken)
    {
        var path = NotionApiPath.PathOf(url) ?? string.Empty;
        if (_client is null)
        {
            if (!NotionApiPath.TryParseDatabaseQuery(path, out var offlineDatabaseId))
            {
                throw new NotionSnapshotException("Notion offline builds only replay database queries.");
            }

            var (document, serverTime) = _snapshot!.ReadQuery(offlineDatabaseId, json, _logger);
            return new NotionResponse(document, serverTime);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        NotionResponse response;
        try
        {
            response = await _client.SendWithServerTimeAsync(
                request,
                NotionRequestSemantics.IdempotentRead,
                cancellationToken);
        }
        catch (NotionApiException exception)
        {
            throw new ContentException(exception.Message, exception);
        }

        if (_snapshot is not null && NotionApiPath.TryParseDatabaseQuery(path, out var databaseId))
        {
            _snapshot.RecordQuery(databaseId, json, response.ServerTime, response.Document.RootElement);
        }

        return response;
    }

    internal async Task<JsonDocument> GetAsync(string url, CancellationToken cancellationToken)
    {
        var path = NotionApiPath.PathOf(url) ?? string.Empty;
        if (_client is null)
        {
            return ReadOffline(path);
        }

        JsonDocument document;
        try
        {
            document = await _client.GetAsync(url, cancellationToken);
        }
        catch (NotionApiException exception)
        {
            if (_snapshot is not null &&
                exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound &&
                IsSnapshotRead(path))
            {
                _snapshot.RecordNotFound(path, (int)exception.StatusCode.Value);
            }

            throw new ContentException(exception.Message, exception);
        }

        if (_snapshot is not null)
        {
            if (NotionApiPath.TryParseDatabase(path, out var databaseId))
            {
                _snapshot.RecordDatabaseSchema(databaseId, document.RootElement);
            }
            else if (NotionApiPath.TryParsePage(path, out var pageId))
            {
                _snapshot.RecordPage(pageId, document.RootElement);
            }
        }

        return document;
    }

    private JsonDocument ReadOffline(string path)
    {
        try
        {
            if (NotionApiPath.TryParseDatabase(path, out var databaseId))
            {
                return _snapshot!.ReadDatabaseSchema(databaseId);
            }

            if (NotionApiPath.TryParsePage(path, out var pageId))
            {
                return _snapshot!.ReadPage(pageId);
            }
        }
        catch (NotionSnapshotRecordedFailure failure)
        {
            // Same shape as the online failure, so callers keep their permission handling.
            var status = (HttpStatusCode)failure.StatusCode;
            var apiException = new NotionApiException(
                NotionApiErrorKind.HttpStatus,
                $"Notion request failed: {failure.StatusCode} (recorded in the offline snapshot)",
                status);
            throw new ContentException(apiException.Message, apiException);
        }

        throw new NotionSnapshotException("Notion offline builds only read database schemas and pages from the snapshot.");
    }

    private static bool IsSnapshotRead(string path)
        => NotionApiPath.TryParseDatabase(path, out _) || NotionApiPath.TryParsePage(path, out _);

    internal NotionClientStats GetStats() => _client?.GetStats() ?? new NotionClientStats(0, 0, 0);

    public void Dispose() => _client?.Dispose();

    private static NotionClientOptions MapOptions(NotionContentSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new NotionClientOptions
        {
            Token = options.Token,
            ApiVersion = NotionApiUrls.NotionVersion,
            RequestDelayMs = options.RequestDelayMs,
            MaxRetries = options.MaxRetries,
            MaxRps = options.MaxRps,
            HttpHandlerFactory = static () => SsrfGuard.CreateSafeHandler()
        };
    }
}
