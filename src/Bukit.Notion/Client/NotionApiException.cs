using System.Net;

namespace Bukit.Notion.Client;

public sealed class NotionApiException : Exception
{
    public NotionApiException(HttpStatusCode statusCode, string? code)
        : base($"Notion API request failed with status {statusCode} ({(int)statusCode}), code {code ?? "unknown"}.")
    {
        StatusCode = statusCode;
        Code = code;
    }

    public HttpStatusCode StatusCode { get; }

    public string? Code { get; }
}
