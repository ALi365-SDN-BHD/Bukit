using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands.SeoGenerativeInsights;

internal static partial class GenerativeAnswerObservationReader
{
    internal const long MaximumFileBytes = 50L * 1024 * 1024;
    internal const int MaximumRows = 100_000;
    internal const int MaximumCitedUrlsPerRow = 100;
    internal const int MaximumRunBound = 9999;
    internal const string Schema = "https://bukit.dev/schemas/generative-answer-observation.v1.json";
    internal const string SchemaVersion = "1.0";

    private static readonly HashSet<string> RootProperties =
        ["schema", "schemaVersion", "engine", "promptSetVersion", "locale", "collectedAt", "collectionMethod", "rows"];
    private static readonly HashSet<string> RowProperties =
        ["questionKey", "promptVariant", "runIndex", "brandMentioned", "siteCited", "citedUrls", "citationPosition", "answerHash"];
    private static readonly HashSet<string> AllowedCollectionMethods = ["api", "browser-export", "manual"];

    internal static GenerativeAnswerObservationDataset Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRemoteUri(path))
        {
            throw Invalid("generative_observation.path_invalid", "A local generative observation file path is required.");
        }

        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        if (file.Length > MaximumFileBytes)
        {
            throw Invalid("generative_observation.file_too_large", $"Generative observation file exceeds {MaximumFileBytes} bytes.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileBytes)
            {
                throw Invalid("generative_observation.file_too_large", $"Generative observation file exceeds {MaximumFileBytes} bytes.");
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
            throw Invalid("generative_observation.json_invalid", "Generative observation file is not valid JSON.", exception);
        }
    }

    private static GenerativeAnswerObservationDataset ReadDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("generative_observation.json_invalid", "Generative observation root must be an object.");
        }

        RejectUnknown(root, RootProperties);
        Require(root, RootProperties);

        ReadNonBlankString(root, "engine", "generative_observation.engine_invalid");
        ReadNonBlankString(root, "promptSetVersion", "generative_observation.version_invalid");
        ReadNonBlankString(root, "locale", "generative_observation.locale_invalid");

        var collectionMethod = ReadNonBlankString(root, "collectionMethod", "generative_observation.collection_method_invalid");
        if (!AllowedCollectionMethods.Contains(collectionMethod))
        {
            throw Invalid("generative_observation.collection_method_invalid", "Collection method must be api, browser-export, or manual.");
        }

        var rows = root.GetProperty("rows");
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("generative_observation.rows_invalid", "Generative observation rows must be an array.");
        }

        if (rows.GetArrayLength() > MaximumRows)
        {
            throw Invalid("generative_observation.row_limit_exceeded", $"Generative observation dataset exceeds {MaximumRows} rows.");
        }

        foreach (var row in rows.EnumerateArray())
        {
            ValidateRowShape(row);
        }

        GenerativeAnswerObservationDataset? dataset;
        try
        {
            dataset = root.Deserialize(SeoGenerativeInsightsJsonContext.Default.GenerativeAnswerObservationDataset);
        }
        catch (JsonException exception)
        {
            throw Invalid("generative_observation.json_invalid", "Generative observation values do not match the v1 contract.", exception);
        }

        if (dataset is null)
        {
            throw Invalid("generative_observation.json_invalid", "Generative observation dataset is empty.");
        }

        ValidateDataset(dataset);
        return dataset;
    }

    private static void ValidateRowShape(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("generative_observation.rows_invalid", "Each generative observation row must be an object.");
        }

        RejectUnknown(row, RowProperties);
        Require(row, RowProperties);

        var questionKey = ReadNonBlankString(row, "questionKey", "generative_observation.question_key_invalid");
        if (!QuestionKeyRegex().IsMatch(questionKey))
        {
            throw Invalid("generative_observation.question_key_invalid", "Question key must be a question:sha256 identity.");
        }

        var answerHash = ReadNonBlankString(row, "answerHash", "generative_observation.answer_hash_invalid");
        if (!AnswerHashRegex().IsMatch(answerHash))
        {
            throw Invalid("generative_observation.answer_hash_invalid", "Answer hash must be an answer:sha256 identity.");
        }

        ReadBoundedInteger(row, "promptVariant", "generative_observation.variant_invalid");
        ReadBoundedInteger(row, "runIndex", "generative_observation.run_index_invalid");

        foreach (var flag in new[] { "brandMentioned", "siteCited" })
        {
            if (row.GetProperty(flag).ValueKind != JsonValueKind.True && row.GetProperty(flag).ValueKind != JsonValueKind.False)
            {
                throw Invalid("generative_observation.rows_invalid", $"Generative observation field '{flag}' must be a boolean.");
            }
        }

        var siteCited = row.GetProperty("siteCited").GetBoolean();

        var citedUrls = row.GetProperty("citedUrls");
        if (citedUrls.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("generative_observation.rows_invalid", "Cited URLs must be an array.");
        }

        if (citedUrls.GetArrayLength() > MaximumCitedUrlsPerRow)
        {
            throw Invalid("generative_observation.rows_invalid", $"Cited URLs exceed {MaximumCitedUrlsPerRow} entries per row.");
        }

        foreach (var citedUrl in citedUrls.EnumerateArray())
        {
            if (citedUrl.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(citedUrl.GetString()))
            {
                throw Invalid("generative_observation.rows_invalid", "Each cited URL must be a non-blank string.");
            }
        }

        var citationPosition = row.GetProperty("citationPosition");
        if (citationPosition.ValueKind is not JsonValueKind.Null)
        {
            if (!citationPosition.TryGetInt64(out var position) || position < 1)
            {
                throw Invalid("generative_observation.citation_position_invalid", "Citation position must be a positive integer or null.");
            }

            if (!siteCited)
            {
                throw Invalid("generative_observation.citation_position_invalid", "Citation position requires a site citation.");
            }
        }
    }

    private static void ValidateDataset(GenerativeAnswerObservationDataset dataset)
    {
        if (!string.Equals(dataset.Schema, Schema, StringComparison.Ordinal) ||
            !string.Equals(dataset.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid("generative_observation.schema_invalid", "Generative observation schema must be the exact v1 contract.");
        }

        if (dataset.Rows is null)
        {
            throw Invalid("generative_observation.rows_invalid", "Generative observation rows are required.");
        }

        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in dataset.Rows)
        {
            if (row is null)
            {
                throw Invalid("generative_observation.rows_invalid", "Each generative observation row must be an object.");
            }

            if (!identities.Add($"{row.QuestionKey}|{row.PromptVariant}|{row.RunIndex}"))
            {
                throw Invalid("generative_observation.run_identity_duplicate", "Run identity must be unique within a dataset.");
            }
        }
    }

    private static string ReadNonBlankString(JsonElement value, string property, string code)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw Invalid(code, $"Generative observation field '{property}' must be a non-blank string.");
        }

        return element.GetString()!;
    }

    private static void ReadBoundedInteger(JsonElement row, string property, string code)
    {
        var element = row.GetProperty(property);
        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt32(out var value) ||
            value < 0 || value > MaximumRunBound)
        {
            throw Invalid(code, $"Generative observation field '{property}' must be an integer between 0 and {MaximumRunBound}.");
        }
    }

    private static void RejectUnknown(JsonElement value, IReadOnlySet<string> allowed)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw Invalid("generative_observation.duplicate_field", "Generative observation object contains a duplicate field.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw Invalid("generative_observation.unknown_field", $"Unknown generative observation field '{property.Name}'.");
            }
        }
    }

    private static void Require(JsonElement value, IEnumerable<string> required)
    {
        foreach (var property in required)
        {
            if (!value.TryGetProperty(property, out _))
            {
                throw Invalid("generative_observation.field_required", $"Generative observation field '{property}' is required.");
            }
        }
    }

    private static bool IsRemoteUri(string path)
        => Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is not "file";

    private static InvalidDataException Invalid(string code, string detail, Exception? inner = null)
        => new($"{code}: {detail}", inner);

    [GeneratedRegex("^question:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex QuestionKeyRegex();

    [GeneratedRegex("^answer:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex AnswerHashRegex();
}
