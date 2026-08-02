using System.Text.Json.Nodes;
using Bukit.Cli.Commands.SeoInsights;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoInsightsRuleProfileReaderTests
{
    private const string ValidProfileJson = """
        {
          "schema": "https://bukit.dev/schemas/seo-insights-rules.v1.json",
          "schemaVersion": "1.0",
          "siteHost": "Example.COM.",
          "hostAliases": ["z.example.com", "A.example.com"],
          "ignoredQueryParameters": ["utm_source", "gclid"],
          "thresholds": {
            "minimumSearchImpressions": 100,
            "maximumLowImpressions": 20,
            "minimumAnalyticsSessions": 10,
            "lowCtr": 0.2,
            "lowEngagementRate": 0.3,
            "highEngagementRate": 0.7,
            "opportunityPositionMinimum": 4.0,
            "opportunityPositionMaximum": 12.0
          },
          "priorities": {
            "snippetMismatch": "P0",
            "landingQuality": "P1",
            "discoverability": "P2",
            "positionOpportunity": "P1"
          }
        }
        """;

    [Fact]
    public void Read_ValidProfileNormalizesHostsAndSortsCollections()
    {
        var profile = Read(ValidProfileJson);

        Assert.Equal("example.com", profile.SiteHost);
        Assert.Equal(["a.example.com", "z.example.com"], profile.HostAliases);
        Assert.Equal(["gclid", "utm_source"], profile.IgnoredQueryParameters);
        Assert.Equal(new SeoInsightsThresholds(100, 20, 10, 0.2, 0.3, 0.7, 4, 12), profile.Thresholds);
        Assert.Equal(new SeoInsightsPriorities("P0", "P1", "P2", "P1"), profile.Priorities);
    }

    [Fact]
    public void Read_RequiresEveryFieldAtEveryObjectLevel()
    {
        var root = JsonNode.Parse(ValidProfileJson)!.AsObject();
        var cases = new (string? Parent, string Property)[]
        {
            (null, "schema"), (null, "schemaVersion"), (null, "siteHost"),
            (null, "hostAliases"), (null, "ignoredQueryParameters"), (null, "thresholds"), (null, "priorities"),
            ("thresholds", "minimumSearchImpressions"), ("thresholds", "maximumLowImpressions"),
            ("thresholds", "minimumAnalyticsSessions"), ("thresholds", "lowCtr"),
            ("thresholds", "lowEngagementRate"), ("thresholds", "highEngagementRate"),
            ("thresholds", "opportunityPositionMinimum"), ("thresholds", "opportunityPositionMaximum"),
            ("priorities", "snippetMismatch"), ("priorities", "landingQuality"),
            ("priorities", "discoverability"), ("priorities", "positionOpportunity")
        };

        foreach (var (parent, property) in cases)
        {
            var value = root.DeepClone().AsObject();
            var target = parent is null ? value : value[parent]!.AsObject();
            Assert.True(target.Remove(property));

            var exception = Assert.Throws<InvalidDataException>(() => Read(value.ToJsonString()));
            Assert.StartsWith("rules.field_required", exception.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("\"schemaVersion\": \"1.0\",", "\"schemaVersion\": \"1.0\", \"secretRoot\": \"do-not-leak\",")]
    [InlineData("\"minimumSearchImpressions\": 100,", "\"minimumSearchImpressions\": 100, \"secretThreshold\": \"do-not-leak\",")]
    [InlineData("\"snippetMismatch\": \"P0\",", "\"snippetMismatch\": \"P0\", \"secretPriority\": \"do-not-leak\",")]
    public void Read_RejectsUnknownFieldsAtEveryObjectLevelWithoutLeakingValues(string current, string replacement)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Read(ValidProfileJson.Replace(current, replacement, StringComparison.Ordinal)));

        Assert.StartsWith("rules.unknown_field", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-leak", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"schemaVersion\": \"1.0\",", "\"schemaVersion\": \"1.0\", \"schemaVersion\": \"9.9\",")]
    [InlineData("\"minimumSearchImpressions\": 100,", "\"minimumSearchImpressions\": 100, \"minimumSearchImpressions\": 200,")]
    [InlineData("\"snippetMismatch\": \"P0\",", "\"snippetMismatch\": \"P0\", \"snippetMismatch\": \"P2\",")]
    public void Read_RejectsDuplicateFieldsAtEveryObjectLevel(string current, string replacement)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Read(ValidProfileJson.Replace(current, replacement, StringComparison.Ordinal)));

        Assert.StartsWith("rules.duplicate_field", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"2.0\"")]
    [InlineData("https://bukit.dev/schemas/seo-insights-rules.v1.json", "https://bukit.dev/schemas/seo-insights-rules.v2.json")]
    public void Read_RequiresExactSchemaAndVersion(string current, string replacement)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Read(ValidProfileJson.Replace(current, replacement, StringComparison.Ordinal)));

        Assert.StartsWith("rules.schema_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"minimumSearchImpressions\": 100", "\"minimumSearchImpressions\": -1")]
    [InlineData("\"minimumSearchImpressions\": 100", "\"minimumSearchImpressions\": 9223372036854775808")]
    [InlineData("\"lowCtr\": 0.2", "\"lowCtr\": -0.01")]
    [InlineData("\"lowCtr\": 0.2", "\"lowCtr\": 1.01")]
    [InlineData("\"lowCtr\": 0.2", "\"lowCtr\": 1e309")]
    [InlineData("\"opportunityPositionMinimum\": 4.0", "\"opportunityPositionMinimum\": 0")]
    [InlineData("\"opportunityPositionMaximum\": 12.0", "\"opportunityPositionMaximum\": 1e309")]
    public void Read_RejectsInvalidOrOutOfRangeThresholds(string current, string replacement)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Read(ValidProfileJson.Replace(current, replacement, StringComparison.Ordinal)));

        Assert.StartsWith("rules.threshold_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsReversedOpportunityInterval()
    {
        var json = ValidProfileJson
            .Replace("\"opportunityPositionMinimum\": 4.0", "\"opportunityPositionMinimum\": 13", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("rules.threshold_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("P3")]
    [InlineData("p0")]
    [InlineData("")]
    public void Read_AcceptsOnlyExactConfiguredPriorities(string priority)
    {
        var json = ValidProfileJson.Replace("\"snippetMismatch\": \"P0\"", $"\"snippetMismatch\": \"{priority}\"", StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("rules.priority_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com:443")]
    [InlineData("user@example.com")]
    [InlineData("example.com/path")]
    [InlineData("example.com?x=1")]
    [InlineData("example.com#fragment")]
    [InlineData("bad_host.example.com")]
    [InlineData("192.0.2.1")]
    public void Read_RejectsNonDnsSiteHosts(string host)
    {
        var json = ValidProfileJson.Replace("Example.COM.", host, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith("rules.host_invalid", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[\"z.example.com\", \"Z.EXAMPLE.COM.\"]", "rules.alias_duplicate")]
    [InlineData("[\"z.example.com\", \"example.com\"]", "rules.alias_duplicate")]
    [InlineData("[\"bad_host.example.com\"]", "rules.host_invalid")]
    public void Read_RejectsInvalidOrAmbiguousAliases(string aliases, string code)
    {
        var json = ValidProfileJson.Replace("[\"z.example.com\", \"A.example.com\"]", aliases, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith(code, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[\"utm_source\", \"UTM_SOURCE\"]", "rules.parameter_duplicate")]
    [InlineData("[\"utm source\"]", "rules.parameter_invalid")]
    [InlineData("[\"utm[source]\"]", "rules.parameter_invalid")]
    [InlineData("[\"\"]", "rules.parameter_invalid")]
    public void Read_RejectsInvalidOrAmbiguousIgnoredParameterNames(string parameters, string code)
    {
        var json = ValidProfileJson.Replace("[\"utm_source\", \"gclid\"]", parameters, StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(() => Read(json));

        Assert.StartsWith(code, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsRemotePaths()
    {
        var exception = Assert.Throws<InvalidDataException>(() => SeoInsightsRuleProfileReader.Read("https://example.com/rules.json"));

        Assert.StartsWith("rules.path_invalid", exception.Message, StringComparison.Ordinal);
    }

    private static SeoInsightsRuleProfile Read(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-seo-rules-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            return SeoInsightsRuleProfileReader.Read(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
