using System.Text;
using Bukit.Cli.Commands.SeoGenerativeInsights;
using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class GenerativeAnswerObservationReaderTests : IDisposable
{
    private const string QuestionKey = "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OtherQuestionKey = "question:sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    private const string AnswerHash = "answer:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "bukit-tests", $"generative-reader-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Read_ValidDatasetReturnsTypedRows()
    {
        var dataset = Read(Dataset());

        Assert.Equal("https://bukit.dev/schemas/generative-answer-observation.v1.json", dataset.Schema);
        Assert.Equal("1.0", dataset.SchemaVersion);
        Assert.Equal("provider-model-channel", dataset.Engine);
        Assert.Equal("2026-08-05.1", dataset.PromptSetVersion);
        Assert.Equal("zh-CN", dataset.Locale);
        Assert.Equal("api", dataset.CollectionMethod);
        var row = Assert.Single(dataset.Rows);
        Assert.Equal(QuestionKey, row.QuestionKey);
        Assert.Equal(0, row.PromptVariant);
        Assert.Equal(0, row.RunIndex);
        Assert.True(row.BrandMentioned);
        Assert.True(row.SiteCited);
        Assert.Equal(["https://www.example.com/a/"], row.CitedUrls);
        Assert.Equal(1, row.CitationPosition);
        Assert.Equal(AnswerHash, row.AnswerHash);
    }

    [Theory]
    [InlineData("\"schema\": \"https://bukit.dev/schemas/generative-answer-observation.v1.json\"", "\"schema\": \"wrong\"", "generative_observation.schema_invalid")]
    [InlineData("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"2.0\"", "generative_observation.schema_invalid")]
    [InlineData("\"engine\": \"provider-model-channel\"", "\"engine\": \" \"", "generative_observation.engine_invalid")]
    [InlineData("\"promptSetVersion\": \"2026-08-05.1\"", "\"promptSetVersion\": \" \"", "generative_observation.version_invalid")]
    [InlineData("\"locale\": \"zh-CN\"", "\"locale\": \" \"", "generative_observation.locale_invalid")]
    [InlineData("\"collectionMethod\": \"api\"", "\"collectionMethod\": \"scrape\"", "generative_observation.collection_method_invalid")]
    [InlineData("\"questionKey\": \"" + QuestionKey + "\"", "\"questionKey\": \"plain\"", "generative_observation.question_key_invalid")]
    [InlineData("\"answerHash\": \"" + AnswerHash + "\"", "\"answerHash\": \"answer:sha256:zzz\"", "generative_observation.answer_hash_invalid")]
    [InlineData("\"promptVariant\": 0", "\"promptVariant\": 10000", "generative_observation.variant_invalid")]
    [InlineData("\"promptVariant\": 0", "\"promptVariant\": -1", "generative_observation.variant_invalid")]
    [InlineData("\"runIndex\": 0", "\"runIndex\": 10000", "generative_observation.run_index_invalid")]
    [InlineData("\"runIndex\": 0", "\"runIndex\": -1", "generative_observation.run_index_invalid")]
    [InlineData("\"citationPosition\": 1", "\"citationPosition\": 0", "generative_observation.citation_position_invalid")]
    public void Read_InvalidContractIsRejected(string oldValue, string newValue, string code)
    {
        var json = Dataset().Replace(oldValue, newValue, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith(code, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(json, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_CitationPositionWithoutSiteCitationIsRejected()
    {
        var json = Dataset()
            .Replace("\"siteCited\": true", "\"siteCited\": false", StringComparison.Ordinal)
            .Replace("\"citedUrls\": [\n        \"https://www.example.com/a/\"\n      ]", "\"citedUrls\": []", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("generative_observation.citation_position_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_UnknownFieldIsRejected()
    {
        var json = Dataset().Replace("\"schema\":", "\"extra\": true, \"schema\":", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("generative_observation.unknown_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_DuplicateFieldIsRejected()
    {
        var json = Dataset().Replace("\"engine\": \"provider-model-channel\"", "\"engine\": \"provider-model-channel\", \"engine\": \"provider-model-channel\"", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("generative_observation.duplicate_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_DuplicateRunIdentityIsRejected()
    {
        var json = Dataset(rows: 2, sameIdentity: true);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("generative_observation.run_identity_duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RemoteUriIsRejectedWithoutNetworkAccess()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => GenerativeAnswerObservationReader.Read("https://example.com/observations.json"));

        Assert.StartsWith("generative_observation.path_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_OverRowLimitIsRejected()
    {
        var json = Dataset(rowCount: GenerativeAnswerObservationReader.MaximumRows + 1);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("generative_observation.row_limit_exceeded", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AllowedHostCitationIsClassifiedAllowed()
    {
        var dataset = Read(Dataset());

        var validation = GenerativeAnswerObservationValidator.Validate(dataset, Options());

        var classification = Assert.Single(Assert.Single(validation.Rows).CitedUrls);
        Assert.Equal("allowed", classification.Kind);
        Assert.Null(classification.ErrorCode);
    }

    [Fact]
    public void Validate_ExternalHttpCitationIsClassifiedExternalWithoutFailing()
    {
        var json = Dataset().Replace(
            "\"citedUrls\": [\n        \"https://www.example.com/a/\"\n      ]",
            "\"citedUrls\": [\n        \"https://www.example.com/a/\",\n        \"https://third-party.example.org/source/\"\n      ]",
            StringComparison.Ordinal);
        var dataset = Read(json);

        var validation = GenerativeAnswerObservationValidator.Validate(dataset, Options());

        var classifications = Assert.Single(validation.Rows).CitedUrls;
        Assert.Equal(2, classifications.Count);
        Assert.Equal("allowed", classifications[0].Kind);
        Assert.Equal("external", classifications[1].Kind);
        Assert.Null(classifications[1].ErrorCode);
    }

    [Fact]
    public void Validate_SiteCitedFalseWithAllowedHostUrlIsRejected()
    {
        var json = Dataset()
            .Replace("\"siteCited\": true", "\"siteCited\": false", StringComparison.Ordinal)
            .Replace("\"citationPosition\": 1", "\"citationPosition\": null", StringComparison.Ordinal);

        var dataset = Read(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => GenerativeAnswerObservationValidator.Validate(dataset, Options()));

        Assert.StartsWith("generative_observation.site_cited_contradiction", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SiteCitedTrueWithoutAllowedHostUrlIsRejected()
    {
        var json = Dataset().Replace("https://www.example.com/a/", "https://third-party.example.org/source/", StringComparison.Ordinal);

        var dataset = Read(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => GenerativeAnswerObservationValidator.Validate(dataset, Options()));

        Assert.StartsWith("generative_observation.site_cited_contradiction", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DuplicateCitedUrlIsRejected()
    {
        var json = Dataset().Replace(
            "\"citedUrls\": [\n        \"https://www.example.com/a/\"\n      ]",
            "\"citedUrls\": [\n        \"https://www.example.com/a/\",\n        \"https://www.example.com/a/\"\n      ]",
            StringComparison.Ordinal);

        var dataset = Read(json);

        var exception = Assert.Throws<InvalidDataException>(
            () => GenerativeAnswerObservationValidator.Validate(dataset, Options()));

        Assert.StartsWith("generative_observation.cited_url_duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NonHttpCitedUrlIsClassifiedInvalid()
    {
        var json = Dataset()
            .Replace("\"siteCited\": true", "\"siteCited\": false", StringComparison.Ordinal)
            .Replace("\"citationPosition\": 1", "\"citationPosition\": null", StringComparison.Ordinal)
            .Replace("https://www.example.com/a/", "ftp://example.com/file", StringComparison.Ordinal);

        var dataset = Read(json);

        var validation = GenerativeAnswerObservationValidator.Validate(dataset, Options());

        var classification = Assert.Single(Assert.Single(validation.Rows).CitedUrls);
        Assert.Equal("invalid", classification.Kind);
        Assert.NotNull(classification.ErrorCode);
    }

    private static SeoObservationUrlOptions Options()
        => new(
            "www.example.com",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "example.com" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "utm_source" });

    private GenerativeAnswerObservationDataset Read(string json)
        => GenerativeAnswerObservationReader.Read(Write(json));

    private string Write(string json)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "observations.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Dataset(
        int rowCount = 1,
        int rows = 0,
        bool sameIdentity = false)
    {
        var effectiveRows = rows == 0 ? rowCount : rows;
        var builder = new StringBuilder();
        builder.Append(
            """
            {
              "schema": "https://bukit.dev/schemas/generative-answer-observation.v1.json",
              "schemaVersion": "1.0",
              "engine": "provider-model-channel",
              "promptSetVersion": "2026-08-05.1",
              "locale": "zh-CN",
              "collectedAt": "2026-08-05T00:00:00Z",
              "collectionMethod": "api",
              "rows": [

            """);
        for (var index = 0; index < effectiveRows; index++)
        {
            var questionKey = sameIdentity || index == 0 ? QuestionKey : OtherQuestionKey;
            var runIndex = sameIdentity ? 0 : index;
            if (index > 0)
            {
                builder.Append(",\n");
            }

            builder.Append(
                $$$"""
                      {
                        "questionKey": "{{{questionKey}}}",
                        "promptVariant": 0,
                        "runIndex": {{{runIndex}}},
                        "brandMentioned": true,
                        "siteCited": true,
                        "citedUrls": [
                          "https://www.example.com/a/"
                        ],
                        "citationPosition": 1,
                        "answerHash": "{{{AnswerHash}}}"
                      }
                  """);
        }

        builder.Append(
            """

              ]
            }
            """);
        return builder.ToString();
    }
}
