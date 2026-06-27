using System.Text.Json;
using Bukit.Notion.Mapping;
using Bukit.Notion.Report;
using Bukit.Notion.Seed;

namespace Bukit.Notion.Push;

internal sealed class NotionPushPlanner
{
    private readonly NotionPushMode _mode;
    private readonly List<NotionPushDiagnostic> _diagnostics;
    private readonly HashSet<RecordIdentity> _identities = [];

    public NotionPushPlanner(NotionPushMode mode, List<NotionPushDiagnostic> diagnostics)
    {
        _mode = mode;
        _diagnostics = diagnostics;
    }

    public NotionPushRecordResult? Plan(
        NotionDatabaseMapEntry entry,
        NotionSeedCollection collection,
        NotionSeedRecord record)
    {
        string recordPath = $"{collection.Path}#{record.Index}";
        if (string.IsNullOrWhiteSpace(entry.UniqueField))
        {
            _diagnostics.Add(new NotionPushDiagnostic(
                "notion.uniqueFieldMissing",
                NotionDiagnosticSeverity.Error,
                "Database map entry uniqueField is required for push planning.",
                recordPath));
            return null;
        }

        if (!NotionUniqueValueResolver.TryResolve(entry, record, out string? uniqueValue))
        {
            _diagnostics.Add(new NotionPushDiagnostic(
                "notion.uniqueFieldMissing",
                NotionDiagnosticSeverity.Error,
                $"Seed record does not contain a value for unique field {entry.UniqueField}.",
                recordPath));
            return null;
        }

        if (!NotionPropertyMapper.Validate(entry, record, recordPath, _diagnostics))
        {
            return null;
        }

        var planned = new NotionPushRecordResult(
            Collection: entry.Collection ?? collection.Name,
            SeedFile: Path.GetFileName(collection.Path),
            Operation: NotionPushReportWriter.ToOperation(_mode),
            Title: ReadOptionalString(record, "title") ?? ReadOptionalString(record, "name"),
            UniqueField: entry.UniqueField,
            UniqueValue: uniqueValue!,
            DataSourceId: entry.EffectiveDataSourceId!);
        var identity = new RecordIdentity(
            planned.Collection,
            planned.SeedFile,
            planned.UniqueField,
            planned.UniqueValue);
        if (_identities.Add(identity))
        {
            return planned;
        }

        const string errorCode = "notion.seedDuplicateUniqueValue";
        string errorMessage = $"Seed records contain duplicate unique value {planned.UniqueValue} for {planned.UniqueField}.";
        _diagnostics.Add(new NotionPushDiagnostic(
            errorCode,
            NotionDiagnosticSeverity.Error,
            errorMessage,
            recordPath));
        return planned with
        {
            Status = NotionPushRecordStatus.Failed,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    private static string? ReadOptionalString(NotionSeedRecord record, string key)
        => record.Fields.TryGetValue(key, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private readonly record struct RecordIdentity(
        string Collection,
        string SeedFile,
        string UniqueField,
        string UniqueValue);
}
