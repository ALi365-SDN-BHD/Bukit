using System.Net;
using Bukit.Content.Notion;
using Bukit.Notion.Transport;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// The CLI-facing bridge to the Notion code mapping. Bukit.Cli does not reference
/// Bukit.Content, so this lives in the engine assembly (which Content opens to the CLI).
/// </summary>
internal static class NotionDiagnostics
{
    internal static DiagnosticCode CodeFor(NotionApiErrorKind? kind, HttpStatusCode? status)
        => NotionErrorCodes.For(kind, status);

    internal static string Format(NotionApiErrorKind? kind, HttpStatusCode? status)
        => DiagnosticCodeFormatter.Format(CodeFor(kind, status));
}
