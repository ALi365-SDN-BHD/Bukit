using System.Text.Json;

namespace Bukit.Cli.Commands.SeoInsights;

internal static class SeoObservationDatasetReader
{
    internal const long MaximumFileBytes = 50L * 1024 * 1024;
    internal const int MaximumRows = 100_000;
    internal const string Schema = "https://bukit.dev/schemas/seo-observation.v1.json";
    internal const string SchemaVersion = "1.0";
    internal const string GoogleSearchConsole = "google-search-console";
    internal const string GoogleAnalytics4 = "google-analytics-4";
    internal const string Scope = "google-organic";

    private static readonly HashSet<string> RootProperties =
        ["schema", "schemaVersion", "provider", "scope", "collectedAt", "window", "rows"];
    private static readonly HashSet<string> WindowProperties = ["startDate", "endDate", "timeZone"];
    private static readonly HashSet<string> RowProperties =
        ["url", "impressions", "clicks", "averagePosition", "sessions", "engagedSessions", "keyEvents"];
    private static readonly HashSet<string> GscMetrics = ["impressions", "clicks", "averagePosition"];
    private static readonly HashSet<string> Ga4Metrics = ["sessions", "engagedSessions", "keyEvents"];

    internal static SeoObservationDataset Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRemoteUri(path))
        {
            throw Invalid("observation.path_invalid", "A local observation file path is required.");
        }

        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        if (file.Length > MaximumFileBytes)
        {
            throw Invalid("observation.file_too_large", $"Observation file exceeds {MaximumFileBytes} bytes.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileBytes)
            {
                throw Invalid("observation.file_too_large", $"Observation file exceeds {MaximumFileBytes} bytes.");
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
            throw Invalid("observation.json_invalid", "Observation file is not valid JSON.", exception);
        }
    }

    private static SeoObservationDataset ReadDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("observation.json_invalid", "Observation root must be an object.");
        }

        RejectUnknown(root, RootProperties);
        Require(root, RootProperties);

        var window = root.GetProperty("window");
        if (window.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("observation.window_invalid", "Observation window must be an object.");
        }

        RejectUnknown(window, WindowProperties);
        Require(window, WindowProperties);

        var rows = root.GetProperty("rows");
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("observation.rows_invalid", "Observation rows must be an array.");
        }

        if (rows.GetArrayLength() > MaximumRows)
        {
            throw Invalid("observation.row_limit_exceeded", $"Observation dataset exceeds {MaximumRows} rows.");
        }

        var provider = ReadRequiredString(root, "provider", "observation.provider_invalid");
        foreach (var row in rows.EnumerateArray())
        {
            ValidateRowShape(row, provider);
        }

        SeoObservationDataset? dataset;
        try
        {
            dataset = root.Deserialize(SeoInsightsJsonContext.Default.SeoObservationDataset);
        }
        catch (JsonException exception)
        {
            throw Invalid("observation.json_invalid", "Observation values do not match the v1 contract.", exception);
        }

        if (dataset is null)
        {
            throw Invalid("observation.json_invalid", "Observation dataset is empty.");
        }

        ValidateDataset(dataset);
        return dataset;
    }

    private static void ValidateDataset(SeoObservationDataset dataset)
    {
        if (!string.Equals(dataset.Schema, Schema, StringComparison.Ordinal) ||
            !string.Equals(dataset.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid("observation.schema_invalid", "Observation schema must be the exact v1 contract.");
        }

        if (string.IsNullOrWhiteSpace(dataset.Provider) ||
            dataset.Provider is not GoogleSearchConsole and not GoogleAnalytics4)
        {
            throw Invalid("observation.provider_invalid", "Observation provider is not supported by v1.");
        }

        if (string.IsNullOrWhiteSpace(dataset.Scope) || !string.Equals(dataset.Scope, Scope, StringComparison.Ordinal))
        {
            throw Invalid("observation.scope_invalid", "Observation scope must be google-organic.");
        }

        if (dataset.Window is null || string.IsNullOrWhiteSpace(dataset.Window.TimeZone) ||
            dataset.Window.EndDate < dataset.Window.StartDate)
        {
            throw Invalid("observation.window_invalid", "Observation window or time zone is invalid.");
        }

        if (dataset.Rows is null)
        {
            throw Invalid("observation.rows_invalid", "Observation rows are required.");
        }

        foreach (var row in dataset.Rows)
        {
            ValidateRow(row, dataset.Provider);
        }
    }

    private static void ValidateRowShape(JsonElement row, string provider)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("observation.rows_invalid", "Each observation row must be an object.");
        }

        RejectUnknown(row, RowProperties);
        if (!row.TryGetProperty("url", out _))
        {
            throw Invalid("observation.url_invalid", "Observation row URL is required.");
        }

        var requiredMetrics = provider switch
        {
            GoogleSearchConsole => GscMetrics,
            GoogleAnalytics4 => Ga4Metrics,
            _ => throw Invalid("observation.provider_invalid", "Observation provider is not supported by v1.")
        };
        var foreignMetrics = provider == GoogleSearchConsole ? Ga4Metrics : GscMetrics;
        if (requiredMetrics.Any(metric => !row.TryGetProperty(metric, out _)) ||
            foreignMetrics.Any(metric => row.TryGetProperty(metric, out _)))
        {
            throw Invalid("observation.provider_metrics_invalid", "Observation row metrics do not match its provider.");
        }

        foreach (var metric in provider == GoogleSearchConsole
                     ? new[] { "impressions", "clicks" }
                     : new[] { "sessions", "engagedSessions", "keyEvents" })
        {
            var value = row.GetProperty(metric);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))
            {
                throw Invalid("observation.metric_invalid", "Observation integer metric is out of range.");
            }
        }

        if (provider == GoogleSearchConsole)
        {
            var averagePosition = row.GetProperty("averagePosition");
            if (averagePosition.ValueKind != JsonValueKind.Number ||
                !averagePosition.TryGetDouble(out var position) ||
                !double.IsFinite(position))
            {
                throw Invalid("observation.metric_invalid", "Observation metric must be finite.");
            }
        }
    }

    private static void ValidateRow(SeoObservationRow row, string provider)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.Url))
        {
            throw Invalid("observation.url_invalid", "Observation row URL must not be blank.");
        }

        if (provider == GoogleSearchConsole)
        {
            if (row.Impressions is null || row.Clicks is null || row.AveragePosition is null ||
                row.Sessions is not null || row.EngagedSessions is not null || row.KeyEvents is not null)
            {
                throw Invalid("observation.provider_metrics_invalid", "GSC row metrics are incomplete or foreign.");
            }

            if (row.Impressions < 0 || row.Clicks < 0 || row.AveragePosition < 0 ||
                !double.IsFinite(row.AveragePosition.Value) || row.Clicks > row.Impressions)
            {
                throw Invalid("observation.metric_invalid", "GSC row metrics are invalid.");
            }

            return;
        }

        if (row.Sessions is null || row.EngagedSessions is null || row.KeyEvents is null ||
            row.Impressions is not null || row.Clicks is not null || row.AveragePosition is not null)
        {
            throw Invalid("observation.provider_metrics_invalid", "GA4 row metrics are incomplete or foreign.");
        }

        if (row.Sessions < 0 || row.EngagedSessions < 0 || row.KeyEvents < 0 ||
            row.EngagedSessions > row.Sessions)
        {
            throw Invalid("observation.metric_invalid", "GA4 row metrics are invalid.");
        }
    }

    private static void RejectUnknown(JsonElement value, IReadOnlySet<string> allowed)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw Invalid(
                    "observation.duplicate_field",
                    "Observation object contains a duplicate field.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw Invalid("observation.unknown_field", $"Unknown observation field '{property.Name}'.");
            }
        }
    }

    private static void Require(JsonElement value, IEnumerable<string> required)
    {
        foreach (var property in required)
        {
            if (!value.TryGetProperty(property, out _))
            {
                throw Invalid("observation.field_required", $"Observation field '{property}' is required.");
            }
        }
    }

    private static string ReadRequiredString(JsonElement value, string property, string code)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid(code, $"Observation field '{property}' must be a string.");
        }

        return element.GetString()!;
    }

    private static bool IsRemoteUri(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is not "file";

    private static InvalidDataException Invalid(string code, string detail, Exception? inner = null)
        => new($"{code}: {detail}", inner);
}
