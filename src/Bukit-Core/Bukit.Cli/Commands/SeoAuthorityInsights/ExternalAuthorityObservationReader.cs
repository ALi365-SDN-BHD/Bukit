using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands.SeoAuthorityInsights;

internal static partial class ExternalAuthorityObservationReader
{
    internal const long MaximumFileBytes = 50L * 1024 * 1024;
    internal const int MaximumRows = 100_000;
    internal const int MaximumCitedUrlsPerRow = 100;
    internal const string Schema = "https://bukit.dev/schemas/external-authority-observation.v1.json";
    internal const string SchemaVersion = "1.0";
    internal const string ActiveStatus = "active";

    private static readonly HashSet<string> RootProperties =
        ["schema", "schemaVersion", "provider", "collectedAt", "collectionMethod", "rows"];
    private static readonly HashSet<string> RowProperties =
        ["sourceUrl", "sourceType", "observedAt", "status", "questionKey", "topicKey", "entityKey", "contextHash", "citedUrls"];
    private static readonly HashSet<string> AllowedCollectionMethods = ["api", "export", "manual"];
    private static readonly HashSet<string> AllowedSourceTypes =
        ["official", "regulator", "research", "news", "association", "repository", "forum", "other"];
    private static readonly HashSet<string> AllowedStatuses = ["active", "deleted", "unavailable"];

    internal static ExternalAuthorityObservationDataset Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRemoteUri(path))
        {
            throw Invalid("external_authority_observation.path_invalid", "A local external authority observation file path is required.");
        }

        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        if (file.Length > MaximumFileBytes)
        {
            throw Invalid("external_authority_observation.file_too_large", $"External authority observation file exceeds {MaximumFileBytes} bytes.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileBytes)
            {
                throw Invalid("external_authority_observation.file_too_large", $"External authority observation file exceeds {MaximumFileBytes} bytes.");
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
            throw Invalid("external_authority_observation.json_invalid", "External authority observation file is not valid JSON.", exception);
        }
    }

    private static ExternalAuthorityObservationDataset ReadDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("external_authority_observation.json_invalid", "External authority observation root must be an object.");
        }

        RejectUnknown(root, RootProperties);
        Require(root, RootProperties);

        ReadNonBlankString(root, "provider", "external_authority_observation.provider_invalid");

        var collectedAt = ReadTimestamp(root, "collectedAt", "external_authority_observation.collected_at_invalid");

        var collectionMethod = ReadNonBlankString(root, "collectionMethod", "external_authority_observation.collection_method_invalid");
        if (!AllowedCollectionMethods.Contains(collectionMethod))
        {
            throw Invalid("external_authority_observation.collection_method_invalid", "Collection method must be api, export, or manual.");
        }

        var rows = root.GetProperty("rows");
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("external_authority_observation.rows_invalid", "External authority observation rows must be an array.");
        }

        if (rows.GetArrayLength() > MaximumRows)
        {
            throw Invalid("external_authority_observation.row_limit_exceeded", $"External authority observation dataset exceeds {MaximumRows} rows.");
        }

        foreach (var row in rows.EnumerateArray())
        {
            ValidateRowShape(row, collectedAt);
        }

        ExternalAuthorityObservationDataset? dataset;
        try
        {
            dataset = root.Deserialize(SeoAuthorityInsightsJsonContext.Default.ExternalAuthorityObservationDataset);
        }
        catch (JsonException exception)
        {
            throw Invalid("external_authority_observation.json_invalid", "External authority observation values do not match the v1 contract.", exception);
        }

        if (dataset is null)
        {
            throw Invalid("external_authority_observation.json_invalid", "External authority observation dataset is empty.");
        }

        ValidateDataset(dataset);
        return dataset;
    }

    private static void ValidateRowShape(JsonElement row, DateTimeOffset collectedAt)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("external_authority_observation.rows_invalid", "Each external authority observation row must be an object.");
        }

        RejectUnknown(row, RowProperties);
        Require(row, RowProperties);

        var sourceUrl = ReadNonBlankString(row, "sourceUrl", "external_authority_observation.source_url_invalid");
        if (!IsAbsoluteHttpUrlWithoutCredentials(sourceUrl))
        {
            throw Invalid("external_authority_observation.source_url_invalid", "Source URL must be an absolute HTTP(S) URL.");
        }

        var sourceType = ReadNonBlankString(row, "sourceType", "external_authority_observation.source_type_invalid");
        if (!AllowedSourceTypes.Contains(sourceType))
        {
            throw Invalid("external_authority_observation.source_type_invalid", "Source type must be one of the fixed v1 categories.");
        }

        var observedAt = ReadTimestamp(row, "observedAt", "external_authority_observation.observed_at_invalid");
        if (observedAt > collectedAt)
        {
            throw Invalid("external_authority_observation.observed_at_invalid", "Row observedAt must not be later than dataset collectedAt.");
        }

        var status = ReadNonBlankString(row, "status", "external_authority_observation.status_invalid");
        if (!AllowedStatuses.Contains(status))
        {
            throw Invalid("external_authority_observation.status_invalid", "Status must be active, deleted, or unavailable.");
        }

        var questionKey = ReadOptionalKey(row, "questionKey", QuestionKeyRegex());
        var topicKey = ReadOptionalKey(row, "topicKey", TopicKeyRegex());
        var entityKey = ReadOptionalKey(row, "entityKey", EntityKeyRegex());
        if (questionKey is null && topicKey is null && entityKey is null)
        {
            throw Invalid("external_authority_observation.identity_keys_missing", "Each row requires at least one identity key.");
        }

        var contextHash = ReadNonBlankString(row, "contextHash", "external_authority_observation.context_hash_invalid");
        if (!ContextHashRegex().IsMatch(contextHash))
        {
            throw Invalid("external_authority_observation.context_hash_invalid", "Context hash must be a context:sha256 identity.");
        }

        var citedUrls = row.GetProperty("citedUrls");
        if (citedUrls.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("external_authority_observation.cited_urls_invalid", "Cited URLs must be an array.");
        }

        if (citedUrls.GetArrayLength() > MaximumCitedUrlsPerRow)
        {
            throw Invalid("external_authority_observation.cited_urls_invalid", $"Cited URLs exceed {MaximumCitedUrlsPerRow} entries per row.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var citedUrl in citedUrls.EnumerateArray())
        {
            var value = citedUrl.ValueKind == JsonValueKind.String ? citedUrl.GetString() : null;
            if (value is null || !IsAbsoluteHttpUrlWithoutCredentials(value))
            {
                throw Invalid("external_authority_observation.cited_urls_invalid", "Each cited URL must be an absolute HTTP(S) URL.");
            }

            if (!seen.Add(value))
            {
                throw Invalid("external_authority_observation.cited_url_duplicate", "Cited URLs must be unique within a row.");
            }
        }
    }

    private static void ValidateDataset(ExternalAuthorityObservationDataset dataset)
    {
        if (!string.Equals(dataset.Schema, Schema, StringComparison.Ordinal) ||
            !string.Equals(dataset.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid("external_authority_observation.schema_invalid", "External authority observation schema must be the exact v1 contract.");
        }

        if (dataset.Rows is null)
        {
            throw Invalid("external_authority_observation.rows_invalid", "External authority observation rows are required.");
        }

        foreach (var row in dataset.Rows)
        {
            if (row is null)
            {
                throw Invalid("external_authority_observation.rows_invalid", "Each external authority observation row must be an object.");
            }
        }
    }

    private static string ReadNonBlankString(JsonElement value, string property, string code)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw Invalid(code, $"External authority observation field '{property}' must be a non-blank string.");
        }

        return element.GetString()!;
    }

    private static string? ReadOptionalKey(JsonElement row, string property, Regex pattern)
    {
        var element = row.GetProperty(property);
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw Invalid("external_authority_observation.identity_key_invalid", $"Identity field '{property}' must be a hashed key or null.");
        }

        var value = element.GetString()!;
        if (!pattern.IsMatch(value))
        {
            throw Invalid("external_authority_observation.identity_key_invalid", $"Identity field '{property}' must be a hashed identity.");
        }

        return value;
    }

    private static DateTimeOffset ReadTimestamp(JsonElement value, string property, string code)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(element.GetString(), out var timestamp))
        {
            throw Invalid(code, $"External authority observation field '{property}' must be a date-time string.");
        }

        return timestamp;
    }

    private static void RejectUnknown(JsonElement value, IReadOnlySet<string> allowed)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw Invalid("external_authority_observation.duplicate_field", "External authority observation object contains a duplicate field.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw Invalid("external_authority_observation.unknown_field", $"Unknown external authority observation field '{property.Name}'.");
            }
        }
    }

    private static void Require(JsonElement value, IEnumerable<string> required)
    {
        foreach (var property in required)
        {
            if (!value.TryGetProperty(property, out _))
            {
                throw Invalid("external_authority_observation.field_required", $"External authority observation field '{property}' is required.");
            }
        }
    }

    private static bool IsRemoteUri(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is not "file";

    private static bool IsAbsoluteHttpUrlWithoutCredentials(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           uri.Scheme is "http" or "https" &&
           !string.IsNullOrWhiteSpace(uri.Host) &&
           string.IsNullOrEmpty(uri.UserInfo);

    private static InvalidDataException Invalid(string code, string detail, Exception? inner = null)
        => new($"{code}: {detail}", inner);

    [GeneratedRegex("^question:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex QuestionKeyRegex();

    [GeneratedRegex("^topic:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TopicKeyRegex();

    [GeneratedRegex("^entity:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex EntityKeyRegex();

    [GeneratedRegex("^context:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ContextHashRegex();
}
