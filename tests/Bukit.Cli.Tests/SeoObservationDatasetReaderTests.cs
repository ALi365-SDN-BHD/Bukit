using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoObservationDatasetReaderTests
{
    [Theory]
    [InlineData("google-search-console")]
    [InlineData("google-analytics-4")]
    public void Read_ValidProviderDatasetReturnsTypedRows(string provider)
    {
        var dataset = Read(Dataset(provider));

        Assert.Equal("https://bukit.dev/schemas/seo-observation.v1.json", dataset.Schema);
        Assert.Equal("1.0", dataset.SchemaVersion);
        Assert.Equal(provider, dataset.Provider);
        Assert.Equal("google-organic", dataset.Scope);
        Assert.Equal(new DateOnly(2026, 8, 1), dataset.Window.StartDate);
        Assert.Equal(new DateOnly(2026, 8, 2), dataset.Window.EndDate);
        Assert.Equal("Asia/Kuala_Lumpur", dataset.Window.TimeZone);
        Assert.Single(dataset.Rows);
    }

    [Theory]
    [InlineData("\"schema\": \"https://bukit.dev/schemas/seo-observation.v1.json\"", "\"schema\": \"wrong\"", "observation.schema_invalid")]
    [InlineData("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"2.0\"", "observation.schema_invalid")]
    [InlineData("\"provider\": \"google-search-console\"", "\"provider\": \"\"", "observation.provider_invalid")]
    [InlineData("\"provider\": \"google-search-console\"", "\"provider\": \"other\"", "observation.provider_invalid")]
    [InlineData("\"scope\": \"google-organic\"", "\"scope\": \"\"", "observation.scope_invalid")]
    [InlineData("\"scope\": \"google-organic\"", "\"scope\": \"all\"", "observation.scope_invalid")]
    [InlineData("\"timeZone\": \"Asia/Kuala_Lumpur\"", "\"timeZone\": \" \"", "observation.window_invalid")]
    [InlineData("\"endDate\": \"2026-08-02\"", "\"endDate\": \"2026-07-31\"", "observation.window_invalid")]
    [InlineData("\"url\": \"https://example.com/a/\"", "\"url\": \" \"", "observation.url_invalid")]
    [InlineData("\"impressions\": 10", "\"impressions\": -1", "observation.metric_invalid")]
    [InlineData("\"clicks\": 2", "\"clicks\": -1", "observation.metric_invalid")]
    [InlineData("\"averagePosition\": 3.5", "\"averagePosition\": -0.1", "observation.metric_invalid")]
    [InlineData("\"clicks\": 2", "\"clicks\": 11", "observation.metric_invalid")]
    public void Read_InvalidGscContractIsRejectedIndependently(string oldValue, string newValue, string code)
    {
        var json = Dataset("google-search-console").Replace(oldValue, newValue, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith(code, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(json, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"sessions\": 10", "\"sessions\": -1")]
    [InlineData("\"engagedSessions\": 6", "\"engagedSessions\": -1")]
    [InlineData("\"keyEvents\": 2", "\"keyEvents\": -1")]
    [InlineData("\"engagedSessions\": 6", "\"engagedSessions\": 11")]
    public void Read_InvalidGa4MetricsAreRejected(string oldValue, string newValue)
    {
        var json = Dataset("google-analytics-4").Replace(oldValue, newValue, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("observation.metric_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"schema\":", "\"extra\": true, \"schema\":")]
    [InlineData("\"startDate\":", "\"extra\": true, \"startDate\":")]
    [InlineData("\"url\":", "\"extra\": true, \"url\":")]
    public void Read_UnknownPropertyAtAnyObjectLevelIsRejected(string oldValue, string newValue)
    {
        var json = Dataset("google-search-console").Replace(oldValue, newValue, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("observation.unknown_field", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("google-search-console", "\"clicks\": 2,", "")]
    [InlineData("google-search-console", "\"averagePosition\": 3.5", "\"averagePositionOmitted\": 3.5")]
    [InlineData("google-analytics-4", "\"sessions\": 10,", "")]
    [InlineData("google-analytics-4", "\"keyEvents\": 2", "\"keyEventsOmitted\": 2")]
    public void Read_MissingProviderMetricIsRejected(string provider, string oldValue, string newValue)
    {
        var json = Dataset(provider).Replace(oldValue, newValue, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith(
            newValue.Length == 0 ? "observation.provider_metrics_invalid" : "observation.unknown_field",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("google-search-console", "\"sessions\": 1")]
    [InlineData("google-analytics-4", "\"impressions\": 1")]
    public void Read_ProviderForeignMetricIsRejected(string provider, string foreignMetric)
    {
        var json = Dataset(provider).Replace("\"url\":", foreignMetric + ", \"url\":", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("observation.provider_metrics_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_NonFiniteAveragePositionIsRejected()
    {
        var json = Dataset("google-search-console").Replace("3.5", "1e400", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("observation.metric_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_FileLargerThanFiftyMebibytesIsRejectedBeforeParsing()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                stream.SetLength((50L * 1024 * 1024) + 1);
            }

            var exception = Assert.Throws<InvalidDataException>(() => SeoObservationDatasetReader.Read(path));
            Assert.StartsWith("observation.file_too_large", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_MoreThanOneHundredThousandRowsIsRejected()
    {
        var row = "{\"url\":\"https://example.com/a/\",\"impressions\":0,\"clicks\":0,\"averagePosition\":0}";
        var rows = string.Join(',', Enumerable.Repeat(row, 100_001));
        var json = Dataset("google-search-console").Replace(Row("google-search-console"), rows, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("observation.row_limit_exceeded", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RemoteUriIsRejectedWithoutNetworkAccess()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => SeoObservationDatasetReader.Read("https://example.com/observations.json"));

        Assert.StartsWith("observation.path_invalid", exception.Message, StringComparison.Ordinal);
    }

    private static SeoObservationDataset Read(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            return SeoObservationDatasetReader.Read(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Dataset(string provider)
        => $$"""
        {
          "schema": "https://bukit.dev/schemas/seo-observation.v1.json",
          "schemaVersion": "1.0",
          "provider": "{{provider}}",
          "scope": "google-organic",
          "collectedAt": "2026-08-03T00:00:00Z",
          "window": {
            "startDate": "2026-08-01",
            "endDate": "2026-08-02",
            "timeZone": "Asia/Kuala_Lumpur"
          },
          "rows": [{{Row(provider)}}]
        }
        """;

    private static string Row(string provider)
        => provider == "google-search-console"
            ? "{\"url\": \"https://example.com/a/\", \"impressions\": 10, \"clicks\": 2, \"averagePosition\": 3.5}"
            : "{\"url\": \"https://example.com/a/\", \"sessions\": 10, \"engagedSessions\": 6, \"keyEvents\": 2}";
}
