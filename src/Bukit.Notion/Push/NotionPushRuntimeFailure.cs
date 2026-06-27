using Bukit.Notion.Client;

namespace Bukit.Notion.Push;

internal static class NotionPushRuntimeFailure
{
    public static NotionPushResult Create(
        NotionPushOptions options,
        IReadOnlyList<NotionPushRecordResult> completedRecords,
        NotionPushRecordResult? currentRecord,
        string? remotePageId,
        string errorCode,
        string errorMessage,
        int exitCode = 1,
        string status = NotionPushRecordStatus.Failed)
    {
        var records = new List<NotionPushRecordResult>(completedRecords);
        if (currentRecord is not null)
        {
            records.Add(currentRecord with
            {
                Status = status,
                RemotePageId = remotePageId,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            });
        }

        return new NotionPushResult(
            false,
            exitCode,
            options.DryRun,
            options.Mode,
            records,
            Diagnostics:
            [
                new NotionPushDiagnostic(
                    errorCode,
                    NotionDiagnosticSeverity.Error,
                    errorMessage)
            ],
            Artifacts: []);
    }

    public static string MapApiDiagnosticCode(NotionApiException exception)
    {
        int statusCode = (int)exception.StatusCode;
        return statusCode switch
        {
            401 => "notion.apiUnauthorized",
            403 => "notion.apiForbidden",
            404 => "notion.apiNotFound",
            409 => "notion.apiConflict",
            429 => "notion.rateLimited",
            >= 500 and <= 599 => "notion.apiFailed",
            _ => "notion.apiError"
        };
    }
}
