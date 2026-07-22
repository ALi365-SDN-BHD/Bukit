using System.Net;

namespace Bukit.Notion.Transport;

public enum NotionApiErrorKind
{
    HttpStatus,
    RateLimited,
    InvalidJson,
    Transport
}

public sealed class NotionApiException : Exception
{
    public NotionApiException(
        NotionApiErrorKind kind,
        string message,
        HttpStatusCode? statusCode = null,
        string? reasonPhrase = null,
        int attempts = 1,
        string? rootErrorType = null)
        : base(message)
    {
        Kind = kind;
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Attempts = attempts;
        RootErrorType = rootErrorType;
    }

    public NotionApiErrorKind Kind { get; }
    public HttpStatusCode? StatusCode { get; }
    public string? ReasonPhrase { get; }
    public int Attempts { get; }
    public string? RootErrorType { get; }
}
