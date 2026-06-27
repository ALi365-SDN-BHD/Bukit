using Bukit.Notion.Client;

namespace Bukit.Notion.Push;

internal static class NotionPushRuntimeFailure
{
    public static NotionPushResult Create(
        NotionPushOptions options,
        IReadOnlyList<NotionPushRecordResult> completedRecords,
        IReadOnlyList<NotionPushRecordResult> plannedRecords,
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

        int skippedStartIndex = FindCurrentRecordIndex(plannedRecords, currentRecord);
        skippedStartIndex = skippedStartIndex >= 0
            ? skippedStartIndex + 1
            : Math.Min(completedRecords.Count + (currentRecord is null ? 0 : 1), plannedRecords.Count);
        for (int index = skippedStartIndex; index < plannedRecords.Count; index++)
        {
            records.Add(plannedRecords[index] with
            {
                Status = NotionPushRecordStatus.Skipped,
                RemotePageId = null,
                ErrorCode = "notion.pushNotExecuted",
                ErrorMessage = "Not executed because a previous record stopped the push."
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

    private static int FindCurrentRecordIndex(
        IReadOnlyList<NotionPushRecordResult> plannedRecords,
        NotionPushRecordResult? currentRecord)
    {
        if (currentRecord is null)
        {
            return -1;
        }

        for (int index = 0; index < plannedRecords.Count; index++)
        {
            if (plannedRecords[index] == currentRecord)
            {
                return index;
            }
        }

        return -1;
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
