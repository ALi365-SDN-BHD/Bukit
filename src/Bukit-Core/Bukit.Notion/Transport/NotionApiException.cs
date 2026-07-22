using System.Net;

namespace Bukit.Notion.Transport;

public enum NotionApiErrorKind
{
    HttpStatus,
    RateLimited,
    InvalidJson,
    Transport
}

public sealed class NotionApiException(
    NotionApiErrorKind kind,
    string message,
    HttpStatusCode? statusCode = null,
    string? reasonPhrase = null,
    int attempts = 1,
    string? rootErrorType = null)
    : Exception(message)
{
    public NotionApiErrorKind Kind { get; } = kind;
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string? ReasonPhrase { get; } = reasonPhrase;
    public int Attempts { get; } = attempts;
    public string? RootErrorType { get; } = rootErrorType;
}
