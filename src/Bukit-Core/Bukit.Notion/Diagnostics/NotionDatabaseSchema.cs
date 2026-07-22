using System.Net;
using Bukit.Notion.Transport;

namespace Bukit.Notion.Diagnostics;

public sealed record NotionDatabaseProperty(string Name, string Type);

public sealed record NotionDatabaseSchema(
    bool IsSuccess,
    IReadOnlyList<NotionDatabaseProperty> Properties,
    HttpStatusCode? StatusCode = null,
    string? ReasonPhrase = null,
    NotionApiErrorKind? ErrorKind = null,
    string? ErrorMessage = null);
