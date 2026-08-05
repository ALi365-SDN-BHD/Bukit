using Bukit.Cli.Commands.SeoQuestionInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SearchQuestionObservationReaderTests
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string TopicKey = "topic:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void Read_ValidDatasetReturnsTypedRows()
    {
        var dataset = Read(Dataset());

        Assert.Equal("https://bukit.dev/schemas/search-question-observation.v1.json", dataset.Schema);
        Assert.Equal("1.0", dataset.SchemaVersion);
        Assert.Equal("google-search-console", dataset.Provider);
        Assert.Equal("google-organic", dataset.Scope);
        Assert.Equal("api", dataset.CollectionMethod);
        Assert.Equal(new DateOnly(2026, 8, 1), dataset.Window.StartDate);
        Assert.Equal(new DateOnly(2026, 8, 2), dataset.Window.EndDate);
        Assert.Equal("Asia/Kuala_Lumpur", dataset.Window.TimeZone);
        var row = Assert.Single(dataset.Rows);
        Assert.Equal(QuestionKey, row.QuestionKey);
        Assert.Equal(TopicKey, row.TopicKey);
        Assert.Equal("https://example.com/a/", row.Url);
        Assert.Equal("zh-CN", row.Locale);
        Assert.Equal("desktop", row.Device);
        Assert.Equal(10, row.Impressions);
        Assert.Equal(2, row.Clicks);
        Assert.Equal(3.5, row.AveragePosition);
    }

    [Theory]
    [InlineData("\"schema\": \"https://bukit.dev/schemas/search-question-observation.v1.json\"", "\"schema\": \"wrong\"", "question_observation.schema_invalid")]
    [InlineData("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"2.0\"", "question_observation.schema_invalid")]
    [InlineData("\"provider\": \"google-search-console\"", "\"provider\": \"other\"", "question_observation.provider_invalid")]
    [InlineData("\"scope\": \"google-organic\"", "\"scope\": \"all\"", "question_observation.scope_invalid")]
    [InlineData("\"collectionMethod\": \"api\"", "\"collectionMethod\": \"scrape\"", "question_observation.collection_method_invalid")]
    [InlineData("\"timeZone\": \"Asia/Kuala_Lumpur\"", "\"timeZone\": \" \"", "question_observation.window_invalid")]
    [InlineData("\"endDate\": \"2026-08-02\"", "\"endDate\": \"2026-07-31\"", "question_observation.window_invalid")]
    [InlineData("\"url\": \"https://example.com/a/\"", "\"url\": \" \"", "question_observation.url_invalid")]
    [InlineData("\"device\": \"desktop\"", "\"device\": \"watch\"", "question_observation.device_invalid")]
    [InlineData("\"impressions\": 10", "\"impressions\": -1", "question_observation.metric_invalid")]
    [InlineData("\"clicks\": 2", "\"clicks\": -1", "question_observation.metric_invalid")]
    [InlineData("\"averagePosition\": 3.5", "\"averagePosition\": -0.1", "question_observation.metric_invalid")]
    [InlineData("\"clicks\": 2", "\"clicks\": 11", "question_observation.metric_invalid")]
    public void Read_InvalidContractIsRejected(string oldValue, string newValue, string code)
    {
        var json = Dataset().Replace(oldValue, newValue, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith(code, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(json, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_NonFiniteAveragePositionIsRejected()
    {
        var json = Dataset().Replace("\"averagePosition\": 3.5", "\"averagePosition\": 1e999", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("question_observation.metric_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_UnknownFieldIsRejected()
    {
        var json = Dataset().Replace("\"schema\":", "\"extra\": true, \"schema\":", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("question_observation.unknown_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_DuplicateFieldIsRejected()
    {
        var json = Dataset().Replace(
            "\"schemaVersion\": \"1.0\"",
            "\"schemaVersion\": \"1.0\", \"schemaVersion\": \"1.0\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("question_observation.duplicate_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingRequiredFieldIsRejected()
    {
        var json = Dataset().Replace("\"device\": \"desktop\",", string.Empty, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("question_observation.field_required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MalformedJsonIsRejected()
    {
        var exception = Assert.Throws<InvalidDataException>(() => Read("{ not json"));

        Assert.StartsWith("question_observation.json_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RemoteUriIsRejectedWithoutNetworkAccess()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => SearchQuestionObservationReader.Read("https://attacker.example/questions.json"));

        Assert.StartsWith("question_observation.path_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_MissingFileIsRejectedAsUnavailable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-question-observation-missing-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => SearchQuestionObservationReader.Read(path));
    }

    [Fact]
    public void Read_MoreThanOneHundredThousandRowsIsRejected()
    {
        var json = Dataset(rowCount: SearchQuestionObservationReader.MaximumRows + 1);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("question_observation.row_limit_exceeded", exception.Message, StringComparison.Ordinal);
    }

    private static SearchQuestionObservationDataset Read(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-question-observation-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            return SearchQuestionObservationReader.Read(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string Dataset(int rowCount = 1)
    {
        var rows = string.Join(
            ",",
            Enumerable.Range(0, rowCount).Select(_ => $$"""
                {
                  "questionKey": "{{QuestionKey}}",
                  "topicKey": "{{TopicKey}}",
                  "url": "https://example.com/a/",
                  "locale": "zh-CN",
                  "device": "desktop",
                  "impressions": 10,
                  "clicks": 2,
                  "averagePosition": 3.5
                }
                """));
        return $$"""
            {
              "schema": "https://bukit.dev/schemas/search-question-observation.v1.json",
              "schemaVersion": "1.0",
              "provider": "google-search-console",
              "scope": "google-organic",
              "collectedAt": "2026-08-03T00:00:00Z",
              "collectionMethod": "api",
              "window": {
                "startDate": "2026-08-01",
                "endDate": "2026-08-02",
                "timeZone": "Asia/Kuala_Lumpur"
              },
              "rows": [{{rows}}]
            }
            """;
    }
}
