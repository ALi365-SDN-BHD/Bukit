using System.Net;
using Bukit.Notion.Transport;

namespace Bukit.Notion.Diagnostics;

public sealed record NotionHealthResult(
    bool IsSuccess,
    HttpStatusCode? StatusCode = null,
    string? ReasonPhrase = null,
    NotionApiErrorKind? ErrorKind = null,
    string? ErrorMessage = null);
