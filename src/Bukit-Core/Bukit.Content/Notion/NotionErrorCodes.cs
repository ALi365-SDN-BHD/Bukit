using System.Net;
using Bukit.Notion.Transport;
using Bukit.Shared;

namespace Bukit.Content.Notion;

/// <summary>
/// Maps a Notion transport failure to the stable <c>BKT-0Bxx</c> diagnostics shared by the
/// content pipeline, doctor and the CLI. Check <see cref="NotionApiErrorKind"/> before the HTTP
/// status: <see cref="NotionApiErrorKind.InvalidJson"/> can carry a 2xx status.
/// </summary>
internal static class NotionErrorCodes
{
    internal static DiagnosticCode For(NotionApiErrorKind? kind, HttpStatusCode? status)
    {
        if (kind == NotionApiErrorKind.Transport)
        {
            return DiagnosticCode.NotionNetwork;
        }

        if (kind == NotionApiErrorKind.InvalidJson)
        {
            return DiagnosticCode.NotionInvalidResponse;
        }

        if (kind == NotionApiErrorKind.RateLimited)
        {
            return DiagnosticCode.NotionRateLimited;
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => DiagnosticCode.NotionUnauthorized,
            HttpStatusCode.Forbidden => DiagnosticCode.NotionForbidden,
            HttpStatusCode.NotFound => DiagnosticCode.NotionNotFound,
            HttpStatusCode.TooManyRequests => DiagnosticCode.NotionRateLimited,
            _ => DiagnosticCode.NotionRequestFailed
        };
    }

    internal static ContentException Wrap(NotionApiException exception)
        => new(exception.Message, exception, For(exception.Kind, exception.StatusCode));
}
