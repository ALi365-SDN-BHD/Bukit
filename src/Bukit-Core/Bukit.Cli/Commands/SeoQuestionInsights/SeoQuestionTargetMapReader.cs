using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands.SeoQuestionInsights;

internal static partial class SeoQuestionTargetMapReader
{
    internal const long MaximumFileBytes = 50L * 1024 * 1024;
    internal const int MaximumRows = 100_000;
    internal const string Schema = "https://bukit.dev/schemas/seo-question-target-map.v1.json";
    internal const string SchemaVersion = "1.0";

    private static readonly HashSet<string> RootProperties =
        ["schema", "schemaVersion", "generatedAt", "questions"];
    private static readonly HashSet<string> QuestionProperties =
        ["questionKey", "topicKey", "intent", "locale", "priority", "coveredRouteKeys"];
    private static readonly HashSet<string> AllowedIntents =
        ["informational", "navigational", "commercial", "transactional", "other"];
    private static readonly HashSet<string> AllowedPriorities = ["P0", "P1", "P2"];

    internal static SeoQuestionTargetMap Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRemoteUri(path))
        {
            throw Invalid("target_map.path_invalid", "A local target map file path is required.");
        }

        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        if (file.Length > MaximumFileBytes)
        {
            throw Invalid("target_map.file_too_large", $"Target map file exceeds {MaximumFileBytes} bytes.");
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileBytes)
            {
                throw Invalid("target_map.file_too_large", $"Target map file exceeds {MaximumFileBytes} bytes.");
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
            throw Invalid("target_map.json_invalid", "Target map file is not valid JSON.", exception);
        }
    }

    private static SeoQuestionTargetMap ReadDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("target_map.json_invalid", "Target map root must be an object.");
        }

        RejectUnknown(root, RootProperties);
        Require(root, RootProperties);

        var questions = root.GetProperty("questions");
        if (questions.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("target_map.questions_invalid", "Target map questions must be an array.");
        }

        if (questions.GetArrayLength() > MaximumRows)
        {
            throw Invalid("target_map.row_limit_exceeded", $"Target map exceeds {MaximumRows} questions.");
        }

        foreach (var question in questions.EnumerateArray())
        {
            ValidateQuestionShape(question);
        }

        SeoQuestionTargetMap? map;
        try
        {
            map = root.Deserialize(SeoQuestionInsightsJsonContext.Default.SeoQuestionTargetMap);
        }
        catch (JsonException exception)
        {
            throw Invalid("target_map.json_invalid", "Target map values do not match the v1 contract.", exception);
        }

        if (map is null)
        {
            throw Invalid("target_map.json_invalid", "Target map is empty.");
        }

        ValidateMap(map);
        return map;
    }

    private static void ValidateQuestionShape(JsonElement question)
    {
        if (question.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("target_map.questions_invalid", "Each question target must be an object.");
        }

        RejectUnknown(question, QuestionProperties);
        Require(question, QuestionProperties);

        var questionKey = ReadString(question, "questionKey");
        var topicKey = ReadString(question, "topicKey");
        var intent = ReadString(question, "intent");
        var locale = ReadString(question, "locale");
        var priority = ReadString(question, "priority");

        if (!QuestionKeyRegex().IsMatch(questionKey))
        {
            throw Invalid("target_map.question_key_invalid", "Question key must be a question:sha256 identity.");
        }

        if (!TopicKeyRegex().IsMatch(topicKey))
        {
            throw Invalid("target_map.topic_key_invalid", "Topic key must be a topic:sha256 identity.");
        }

        if (!AllowedIntents.Contains(intent))
        {
            throw Invalid("target_map.intent_invalid", "Question intent is not supported by v1.");
        }

        if (string.IsNullOrWhiteSpace(locale))
        {
            throw Invalid("target_map.locale_invalid", "Question locale must not be blank.");
        }

        if (!AllowedPriorities.Contains(priority))
        {
            throw Invalid("target_map.priority_invalid", "Question priority must be P0, P1, or P2.");
        }

        var coveredRouteKeys = question.GetProperty("coveredRouteKeys");
        if (coveredRouteKeys.ValueKind != JsonValueKind.Array)
        {
            throw Invalid("target_map.route_keys_invalid", "coveredRouteKeys must be an array.");
        }

        foreach (var routeKey in coveredRouteKeys.EnumerateArray())
        {
            if (routeKey.ValueKind != JsonValueKind.String || !RouteKeyRegex().IsMatch(routeKey.GetString()!))
            {
                throw Invalid("target_map.route_keys_invalid", "coveredRouteKeys entries must be route:sha256 identities.");
            }
        }
    }

    private static void ValidateMap(SeoQuestionTargetMap map)
    {
        if (!string.Equals(map.Schema, Schema, StringComparison.Ordinal) ||
            !string.Equals(map.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid("target_map.schema_invalid", "Target map schema must be the exact v1 contract.");
        }

        if (map.Questions is null)
        {
            throw Invalid("target_map.questions_invalid", "Target map questions are required.");
        }
    }

    private static string ReadString(JsonElement value, string property)
    {
        var element = value.GetProperty(property);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid("target_map.field_invalid", $"Target map field '{property}' must be a string.");
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
                throw Invalid("target_map.duplicate_field", "Target map object contains a duplicate field.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw Invalid("target_map.unknown_field", $"Unknown target map field '{property.Name}'.");
            }
        }
    }

    private static void Require(JsonElement value, IEnumerable<string> required)
    {
        foreach (var property in required)
        {
            if (!value.TryGetProperty(property, out _))
            {
                throw Invalid("target_map.field_required", $"Target map field '{property}' is required.");
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

    [GeneratedRegex("^route:sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex RouteKeyRegex();
}
