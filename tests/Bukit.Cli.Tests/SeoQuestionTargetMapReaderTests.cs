using Bukit.Cli.Commands.SeoQuestionInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoQuestionTargetMapReaderTests
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string TopicKey = "topic:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string RouteKey = "route:sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    [Fact]
    public void Read_ValidTargetMapReturnsTypedQuestions()
    {
        var map = Read(TargetMap());

        Assert.Equal("https://bukit.dev/schemas/seo-question-target-map.v1.json", map.Schema);
        Assert.Equal("1.0", map.SchemaVersion);
        var target = Assert.Single(map.Questions);
        Assert.Equal(QuestionKey, target.QuestionKey);
        Assert.Equal(TopicKey, target.TopicKey);
        Assert.Equal("informational", target.Intent);
        Assert.Equal("zh-CN", target.Locale);
        Assert.Equal("P1", target.Priority);
        Assert.Equal([RouteKey], target.CoveredRouteKeys);
    }

    [Theory]
    [InlineData("\"schema\": \"https://bukit.dev/schemas/seo-question-target-map.v1.json\"", "\"schema\": \"wrong\"", "target_map.schema_invalid")]
    [InlineData("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"2.0\"", "target_map.schema_invalid")]
    [InlineData("\"intent\": \"informational\"", "\"intent\": \"unknown\"", "target_map.intent_invalid")]
    [InlineData("\"priority\": \"P1\"", "\"priority\": \"P9\"", "target_map.priority_invalid")]
    [InlineData("\"locale\": \"zh-CN\"", "\"locale\": \" \"", "target_map.locale_invalid")]
    public void Read_InvalidContractIsRejected(string oldValue, string newValue, string code)
    {
        var json = TargetMap().Replace(oldValue, newValue, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith(code, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(json, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"questionKey\":", QuestionKey, "question:sha256:zzz")]
    [InlineData("\"topicKey\":", TopicKey, "topic:sha256:zzz")]
    public void Read_InvalidKeyFormatIsRejected(string field, string oldValue, string newValue)
    {
        var json = TargetMap().Replace(oldValue, newValue, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith(field == "\"questionKey\":" ? "target_map.question_key_invalid" : "target_map.topic_key_invalid",
            exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_InvalidCoveredRouteKeyIsRejected()
    {
        var json = TargetMap().Replace(RouteKey, "route:sha256:zzz", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("target_map.route_keys_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_UnknownFieldIsRejected()
    {
        var json = TargetMap().Replace("\"schema\":", "\"extra\": true, \"schema\":", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("target_map.unknown_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_DuplicateFieldIsRejected()
    {
        var json = TargetMap().Replace(
            "\"schemaVersion\": \"1.0\"",
            "\"schemaVersion\": \"1.0\", \"schemaVersion\": \"1.0\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("target_map.duplicate_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingRequiredFieldIsRejected()
    {
        var json = TargetMap().Replace("\"priority\": \"P1\",", string.Empty, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("target_map.field_required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MalformedJsonIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() => Read("{ not json"));

        Assert.StartsWith("target_map.json_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RemoteUriIsRejectedWithoutNetworkAccess()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => SeoQuestionTargetMapReader.Read("https://attacker.example/target-map.json"));

        Assert.StartsWith("target_map.path_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingFileIsRejectedAsUnavailable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-target-map-missing-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => SeoQuestionTargetMapReader.Read(path));
    }

    [Fact]
    public void Read_MoreThanOneHundredThousandQuestionsIsRejected()
    {
        var json = TargetMap(questionCount: SeoQuestionTargetMapReader.MaximumRows + 1);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("target_map.row_limit_exceeded", exception.Message, StringComparison.Ordinal);
    }

    private static SeoQuestionTargetMap Read(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-target-map-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            return SeoQuestionTargetMapReader.Read(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string TargetMap(int questionCount = 1)
    {
        var questions = string.Join(
            ",",
            Enumerable.Range(0, questionCount).Select(_ => $$"""
                {
                  "questionKey": "{{QuestionKey}}",
                  "topicKey": "{{TopicKey}}",
                  "intent": "informational",
                  "locale": "zh-CN",
                  "priority": "P1",
                  "coveredRouteKeys": ["{{RouteKey}}"]
                }
                """));
        return $$"""
            {
              "schema": "https://bukit.dev/schemas/seo-question-target-map.v1.json",
              "schemaVersion": "1.0",
              "generatedAt": "2026-08-05T00:00:00Z",
              "questions": [{{questions}}]
            }
            """;
    }
}
