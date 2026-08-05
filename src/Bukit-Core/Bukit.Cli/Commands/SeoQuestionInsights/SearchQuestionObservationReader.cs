using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Cli.Commands.SeoInsights;

namespace Bukit.Cli.Commands.SeoQuestionInsights;

internal static partial class SearchQuestionObservationReader
{
    internal const long MaximumFileBytes = 50L * 1024 * 1024;
    internal const int MaximumRows = 100_000;
    internal const string Schema = "https://bukit.dev/schemas/search-question-observation.v1.json";
    internal const string SchemaVersion = "1.0";
    internal const string Provider = "google-search-console";
    internal const string Scope = "google-organic";

    private static readonly HashSet<string> RootProperties =
        ["schema", "schemaVersion", "provider", "scope", "collectedAt", "collectionMethod", "window", "rows"];
    private static readonly HashSet<string> WindowProperties = ["startDate", "endDate", "timeZone"];
    private static readonly HashSet<string> RowProperties =
        ["questionKey", "topicKey", "url", "locale", "device", "impressions", "clicks", "averagePosition"];
    private static readonly HashSet<string> AllowedDevices = ["desktop", "mobile", "tablet", "unknown"];
    private static readonly HashSet<string> AllowedCollectionMethods = ["api", "export", "manual"];

    internal static SearchQuestionObservationDataset Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRemoteUri(path))
        {
            throw Invalid("question_observation.path_invalid", "A local question observation file path is required.");
        }

        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        if (file.Length > MaximumFileBytes)
        {
            throw Invalid("question_observation.file_too_large", $"Question observation file exceeds {MaximumFileBytes} bytes.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileBytes)
            {
                throw Invalid("question_observation.file_too_large", $"Question observation file exceeds {MaximumFileBytes} bytes.");
            }

            using var document = JsonDocument.Parse(stream);
            return ReadDocument(document.RootElement);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Invalid("question_observation.json_invalid", "Question observation file is not valid JSON.", exception);
        }
    }

    private static SearchQuestionObservationDataset ReadDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("question_observation.json_invalid", "Question observation root must be an object.");
        }

        RejectUnknown(root, RootProperties);
        Require(root, RootProperties);

        var window = root.GetProperty("window");
        if (window.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("question_observation.window_invalid", "Question observation window must be an object.");
        }

        RejectUnknown(window, WindowProperties);
        Require(window, WindowProperties);

        var rows = root.GetProperty("rows");
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("question_observation.rows_invalid", "Question observation rows must be an array.");
        }

        if (rows.GetArrayLength() > MaximumRows)
        {
            throw Invalid("question_observation.row_limit_exceeded", $"Question observation dataset exceeds {MaximumRows} rows.");
        }

        var collectionMethod = ReadString(root, "collectionMethod", "question_observation.collection_method_invalid");
        if (!AllowedCollectionMethods.Contains(collectionMethod))
        {
            throw Invalid("question_observation.collection_method_invalid", "Collection method must be api, export, or manual.");
        }

        foreach (var row in rows.EnumerateArray())
        {
            ValidateRowShape(row);
        }

        SearchQuestionObservationDataset? dataset;
        try
        {
            dataset = root.Deserialize(SeoQuestionInsightsJsonContext.Default.SearchQuestionObservationDataset);
        }
        catch (JsonException exception)
        {
            throw Invalid("question_observation.json_invalid", "Question observation values do not match the v1 contract.", exception);
        }

        if (dataset is null)
        {
            throw Invalid("question_observation.json_invalid", "Question observation dataset is empty.");
        }

        ValidateDataset(dataset);
        return dataset;
    }

    private static void ValidateRowShape(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("question_observation.rows_invalid", "Each question observation row must be an object.");
        }

        RejectUnknown(row, RowProperties);
        Require(row, RowProperties);

        var questionKey = ReadString(row, "questionKey", "question_observation.question_key_invalid");
        var topicKey = ReadString(row, "topicKey", "question_observation.topic_key_invalid");
        var locale = ReadString(row, "locale", "question_observation.locale_invalid");
        var device = ReadString(row, "device", "question_observation.device_invalid");

        if (!QuestionKeyRegex().IsMatch(questionKey))
        {
            throw Invalid("question_observation.question_key_invalid", "Question key must be a question:sha256 identity.");
        }

        if (!TopicKeyRegex().IsMatch(topicKey))
        {
            throw Invalid("question_observation.topic_key_invalid", "Topic key must be a topic:sha256 identity.");
        }

        if (string.IsNullOrWhiteSpace(locale))
        {
            throw Invalid("question_observation.locale_invalid", "Question observation locale must not be blank.");
        }

        if (!AllowedDevices.Contains(device))
        {
            throw Invalid("question_observation.device_invalid", "Question observation device is not supported by v1.");
        }

        foreach (var metric in new[] { "impressions", "clicks" })
        {
            var value = row.GetProperty(metric);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))
            {
                throw Invalid("question_observation.metric_invalid", "Question observation integer metric is out of range.");
            }
        }

        var averagePosition = row.GetProperty("averagePosition");
        if (averagePosition.ValueKind != JsonValueKind.Number ||
            !averagePosition.TryGetDouble(out var position) ||
            !double.IsFinite(position))
        {
            throw Invalid("question_observation.metric_invalid", "Question observation metric must be finite.");
        }
    }

    private static void ValidateDataset(SearchQuestionObservationDataset dataset)
    {
        if (!string.Equals(dataset.Schema, Schema, StringComparison.Ordinal) ||
            !string.Equals(dataset.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid("question_observation.schema_invalid", "Question observation schema must be the exact v1 contract.");
        }

        if (string.IsNullOrWhiteSpace(dataset.Provider) ||
            !string.Equals(dataset.Provider, Provider, StringComparison.Ordinal))
        {
            throw Invalid("question_observation.provider_invalid", "Question observation provider is not supported by v1.");
        }

        if (string.IsNullOrWhiteSpace(dataset.Scope) ||
            !string.Equals(dataset.Scope, Scope, StringComparison.Ordinal))
        {
            throw Invalid("question_observation.scope_invalid", "Question observation scope must be google-organic.");
        }

        if (dataset.Window is null || string.IsNullOrWhiteSpace(dataset.Window.TimeZone) ||
            dataset.Window.EndDate < dataset.Window.StartDate)
        {
            throw Invalid("question_observation.window_invalid", "Question observation window or time zone is invalid.");
        }

        if (dataset.Rows is null)
        {
            throw Invalid("question_observation.rows_invalid", "Question observation rows are required.");
        }

        foreach (var row in dataset.Rows)
        {
            ValidateRow(row);
        }
    }

    private static void ValidateRow(SearchQuestionObservationRow row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.Url))
        {
            throw Invalid("question_observation.url_invalid", "Question observation row URL must not be blank.");
        }

        if (row.Impressions < 0 || row.Clicks < 0 || row.AveragePosition < 0 ||
            !double.IsFinite(row.AveragePosition) || row.Clicks > row.Impressions)
        {
            throw Invalid("question_observation.metric_invalid", "Question observation row metrics are invalid.");
        }
    }

    private static string ReadString(JsonElement value, string property, string code)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid(code, $"Question observation field '{property}' must be a string.");
        }

        return element.GetString()!;
    }

    private static void RejectUnknown(JsonElement value, IReadOnlySet<string> allowed)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw Invalid("question_observation.duplicate_field", "Question observation object contains a duplicate field.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw Invalid("question_observation.unknown_field", $"Unknown question observation field '{property.Name}'.");
            }
        }
    }

    private static void Require(JsonElement value, IEnumerable<string> required)
    {
        foreach (var property in required)
        {
            if (!value.TryGetProperty(property, out _))
            {
                throw Invalid("question_observation.field_required", $"Question observation field '{property}' is required.");
            }
        }
    }

    private static bool IsRemoteUri(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is not "file";

    private static InvalidDataException Invalid(string code, string detail, Exception? inner = null)
        => new($"{code}: {detail}", inner);

    [GeneratedRegex("^question:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex QuestionKeyRegex();

    [GeneratedRegex("^topic:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TopicKeyRegex();
}
