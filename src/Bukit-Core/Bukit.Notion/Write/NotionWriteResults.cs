using System.Net;
using System.Text.Json;
using Bukit.Notion.Transport;

namespace Bukit.Notion.Write;

public sealed class NotionWriteResult
{
    private NotionWriteResult(
        bool isSuccess,
        JsonElement? payload,
        HttpStatusCode? statusCode,
        string? reasonPhrase,
        NotionApiErrorKind? errorKind,
        string? errorMessage,
        int attempts)
    {
        IsSuccess = isSuccess;
        Payload = payload;
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
        Attempts = attempts;
    }

    public bool IsSuccess { get; }
    public JsonElement? Payload { get; }
    public HttpStatusCode? StatusCode { get; }
    public string? ReasonPhrase { get; }
    public NotionApiErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    public int Attempts { get; }

    internal static NotionWriteResult Success(JsonElement payload)
        => new(true, payload, HttpStatusCode.OK, null, null, null, 1);

    internal static NotionWriteResult Failure(NotionApiException exception)
        => new(
            false,
            null,
            exception.StatusCode,
            exception.ReasonPhrase,
            exception.Kind,
            exception.Message,
            exception.Attempts);

    public override string ToString()
        => $"{nameof(NotionWriteResult)} {{ IsSuccess = {IsSuccess}, StatusCode = {StatusCode}, ReasonPhrase = {ReasonPhrase}, ErrorKind = {ErrorKind}, ErrorMessage = {ErrorMessage}, Attempts = {Attempts} }}";
}
