namespace Bukit.Notion.Transport;

public sealed class NotionClientOptions
{
    public required string Token { get; init; }
    public string ApiVersion { get; init; } = NotionApiUrls.NotionVersion;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int RequestDelayMs { get; init; }
    public int MaxRetries { get; init; } = 5;
    public int? MaxRps { get; init; }

    /// <summary>
    /// Optional factory that creates the handler chain for the internal <see cref="HttpClient"/>.
    /// The chain must end in a <see cref="SocketsHttpHandler"/> or <see cref="HttpClientHandler"/>;
    /// the client disables automatic redirects on that terminal handler. Other terminal handlers,
    /// missing inner handlers, and null factory results are rejected during construction. When
    /// <c>null</c>, an owned <see cref="SocketsHttpHandler"/> is used.
    /// </summary>
    public Func<HttpMessageHandler>? HttpHandlerFactory { get; init; }

    public override string ToString()
        => $"{nameof(NotionClientOptions)} {{ Token = [redacted], ApiVersion = {ApiVersion}, Timeout = {Timeout}, RequestDelayMs = {RequestDelayMs}, MaxRetries = {MaxRetries}, MaxRps = {MaxRps} }}";
}

public sealed record NotionClientStats(
    long RequestCount,
    long ThrottleWaitCount,
    long ThrottleWaitTotalMs);
