using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bukit.Config;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class SeoGeoDocumentationContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ActiveSchemas_MatchSeoGeoPublicContracts()
    {
        using var configSchema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var site = configSchema.RootElement.GetProperty("properties").GetProperty("site");
        var organization = site
            .GetProperty("properties")
            .GetProperty("seo")
            .GetProperty("properties")
            .GetProperty("organization")
            .GetProperty("properties");

        Assert.Equal(
            ["Organization", "NewsMediaOrganization"],
            organization.GetProperty("type").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("array", organization.GetProperty("sameAs").GetProperty("type").GetString());

        var collection = site
            .GetProperty("properties")
            .GetProperty("collections")
            .GetProperty("additionalProperties")
            .GetProperty("properties");
        Assert.Equal("boolean", collection.GetProperty("noindexWhenEmpty").GetProperty("type").GetString());

        using var snapshotSchema = ReadJson("docs", "schemas", "publish-url-snapshot.v1.schema.json");
        Assert.Equal(
            "https://bukit.dev/schemas/publish-url-snapshot.v1.json",
            snapshotSchema.RootElement.GetProperty("$id").GetString());
        Assert.Equal(
            ["schema", "siteUrl", "routes"],
            snapshotSchema.RootElement.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var siteUrl = snapshotSchema.RootElement.GetProperty("properties").GetProperty("siteUrl");
        Assert.Equal(string.Empty, siteUrl.GetProperty("oneOf")[0].GetProperty("const").GetString());
        Assert.Equal("^https?://", siteUrl.GetProperty("oneOf")[1].GetProperty("pattern").GetString());
        var route = snapshotSchema.RootElement
            .GetProperty("properties")
            .GetProperty("routes")
            .GetProperty("items");
        Assert.Equal(
            ["url", "indexable", "semanticHash"],
            route.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^sha256:[0-9a-f]{64}$",
            route.GetProperty("properties").GetProperty("semanticHash").GetProperty("pattern").GetString());
        Assert.Equal(
            "^https?://",
            route.GetProperty("properties").GetProperty("url").GetProperty("pattern").GetString());

        using var changeSetSchema = ReadJson("docs", "schemas", "publish-url-change-set.v1.schema.json");
        var change = changeSetSchema.RootElement
            .GetProperty("properties")
            .GetProperty("changes")
            .GetProperty("items");
        Assert.Equal(
            ["added", "updated", "deleted"],
            change.GetProperty("properties").GetProperty("type").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["type", "url", "semanticHash"], change.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^https?://",
            change.GetProperty("properties").GetProperty("url").GetProperty("pattern").GetString());

        using var routeMapSchema = ReadJson("docs", "schemas", "seo-route-map.v1.schema.json");
        var routeMapRoot = routeMapSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", routeMapRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/seo-route-map.v1.json", routeMapRoot.GetProperty("$id").GetString());
        Assert.False(routeMapRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "generatedAt", "siteUrl", "baseUrl", "routes"],
            routeMapRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var routeMapSiteUrl = routeMapRoot.GetProperty("properties").GetProperty("siteUrl");
        Assert.Equal(string.Empty, routeMapSiteUrl.GetProperty("oneOf")[0].GetProperty("const").GetString());
        Assert.Equal("uri", routeMapSiteUrl.GetProperty("oneOf")[1].GetProperty("format").GetString());
        var absoluteHttpPattern = routeMapSiteUrl.GetProperty("oneOf")[1].GetProperty("pattern").GetString();
        Assert.Equal("^[Hh][Tt][Tt][Pp][Ss]?://(?![^/?#]*@)", absoluteHttpPattern);
        Assert.Matches(absoluteHttpPattern!, "HTTPS://example.com");
        Assert.Matches(absoluteHttpPattern!, "http://example.com");
        Assert.DoesNotMatch(absoluteHttpPattern!, "https://user:secret@example.com");

        var routeMapRoutes = routeMapRoot.GetProperty("properties").GetProperty("routes");
        Assert.False(routeMapRoutes.TryGetProperty("uniqueItems", out _));
        var routeMapEntry = routeMapRoutes.GetProperty("items");
        Assert.False(routeMapEntry.GetProperty("additionalProperties").GetBoolean());
        Assert.DoesNotContain(
            "contentKey",
            routeMapEntry.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^route:sha256:[0-9a-f]{64}$",
            routeMapEntry.GetProperty("properties").GetProperty("routeKey").GetProperty("pattern").GetString());
        Assert.Equal("^/", routeMapEntry.GetProperty("properties").GetProperty("route").GetProperty("pattern").GetString());
        var canonical = routeMapEntry.GetProperty("properties").GetProperty("canonical");
        Assert.Equal("^[Hh][Tt][Tt][Pp][Ss]?://(?![^/?#]*@)", canonical.GetProperty("oneOf")[0].GetProperty("pattern").GetString());
        Assert.Equal("uri", canonical.GetProperty("oneOf")[0].GetProperty("format").GetString());
        Assert.Matches(canonical.GetProperty("oneOf")[0].GetProperty("pattern").GetString()!, "HTTP://example.com/article/");
        Assert.DoesNotMatch(canonical.GetProperty("oneOf")[0].GetProperty("pattern").GetString()!, "https://user@example.com/article/");
        Assert.Equal("^/(?!/)", canonical.GetProperty("oneOf")[1].GetProperty("pattern").GetString());
        Assert.Matches(canonical.GetProperty("oneOf")[1].GetProperty("pattern").GetString()!, "/article/");
        Assert.DoesNotMatch(canonical.GetProperty("oneOf")[1].GetProperty("pattern").GetString()!, "//other.example/article/");
        var contentKey = routeMapEntry.GetProperty("properties").GetProperty("contentKey").GetProperty("oneOf");
        Assert.Equal("^content:sha256:[0-9a-f]{64}$", contentKey[0].GetProperty("pattern").GetString());
        Assert.Equal("null", contentKey[1].GetProperty("type").GetString());

        using var observationSchema = ReadJson("docs", "schemas", "seo-observation.v1.schema.json");
        var observationRoot = observationSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", observationRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/seo-observation.v1.json", observationRoot.GetProperty("$id").GetString());
        Assert.False(observationRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "provider", "scope", "collectedAt", "window", "rows"],
            observationRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            ["google-search-console", "google-analytics-4"],
            observationRoot.GetProperty("properties").GetProperty("provider").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(2, observationRoot.GetProperty("properties").GetProperty("rows").GetProperty("items").GetProperty("oneOf").GetArrayLength());

        using var insightsSchema = ReadJson("docs", "schemas", "seo-insights-report.v1.schema.json");
        var insightsRoot = insightsSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", insightsRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/seo-insights-report.v1.json", insightsRoot.GetProperty("$id").GetString());
        Assert.False(insightsRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "generatedAt", "window", "sources", "joinQuality", "routes", "unmatched", "ambiguous"],
            insightsRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var joinQuality = insightsRoot.GetProperty("properties").GetProperty("joinQuality");
        Assert.Equal(["overall", "providers"], joinQuality.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var counts = insightsRoot.GetProperty("$defs").GetProperty("joinCounts");
        Assert.Equal(
            ["sourceRows", "matchedRows", "unmatchedRows", "ambiguousRows"],
            counts.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("^route:sha256:[0-9a-f]{64}$", insightsRoot.GetProperty("$defs").GetProperty("candidate").GetProperty("properties").GetProperty("routeKey").GetProperty("pattern").GetString());
        foreach (var definitionName in new[] { "candidate", "route" })
        {
            var reportCanonical = insightsRoot.GetProperty("$defs").GetProperty(definitionName)
                .GetProperty("properties").GetProperty("canonical").GetProperty("oneOf");
            var absolutePattern = reportCanonical[0].GetProperty("pattern").GetString()!;
            var relativePattern = reportCanonical[1].GetProperty("pattern").GetString()!;
            Assert.Equal("uri", reportCanonical[0].GetProperty("format").GetString());
            Assert.Equal("^[Hh][Tt][Tt][Pp][Ss]?://(?![^/?#]*@)", absolutePattern);
            Assert.Matches(absolutePattern, "HTTPS://example.com/article/");
            Assert.DoesNotMatch(absolutePattern, "https://user:secret@example.com/article/");
            Assert.Equal("^/(?!/)", relativePattern);
            Assert.Matches(relativePattern, "/article/");
            Assert.DoesNotMatch(relativePattern, "//other.example/article/");
        }
        var metrics = insightsRoot.GetProperty("$defs").GetProperty("metrics").GetProperty("properties");
        Assert.Equal("#/$defs/nullableRatio", metrics.GetProperty("ctr").GetProperty("$ref").GetString());
        Assert.Equal("#/$defs/nullableRatio", metrics.GetProperty("engagementRate").GetProperty("$ref").GetString());
        Assert.Equal("#/$defs/nullableNumber", metrics.GetProperty("keyEventRate").GetProperty("$ref").GetString());

        var reportRoute = insightsRoot.GetProperty("$defs").GetProperty("route");
        Assert.Equal(
            ["routeKey", "contentKey", "route", "canonical", "metrics", "findings"],
            reportRoute.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("#/$defs/finding", reportRoute.GetProperty("properties").GetProperty("findings")
            .GetProperty("items").GetProperty("$ref").GetString());
        var finding = insightsRoot.GetProperty("$defs").GetProperty("finding");
        Assert.False(finding.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["code", "priority", "routeKey", "evidence", "hypothesis", "suggestedAction"],
            finding.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            [
                "seo.insights.snippet_mismatch",
                "seo.insights.landing_quality",
                "seo.insights.discoverability",
                "seo.insights.position_opportunity"
            ],
            finding.GetProperty("properties").GetProperty("code").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            ["P0", "P1", "P2"],
            finding.GetProperty("properties").GetProperty("priority").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()));
        var evidence = insightsRoot.GetProperty("$defs").GetProperty("evidence");
        Assert.False(evidence.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["metric", "actual", "operator", "threshold"],
            evidence.GetProperty("required").EnumerateArray().Select(value => value.GetString()));

        using var rulesSchema = ReadJson("docs", "schemas", "seo-insights-rules.v1.schema.json");
        var rulesRoot = rulesSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", rulesRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/seo-insights-rules.v1.json", rulesRoot.GetProperty("$id").GetString());
        Assert.False(rulesRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "siteHost", "hostAliases", "ignoredQueryParameters", "thresholds", "priorities"],
            rulesRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var hostAliases = rulesRoot.GetProperty("properties").GetProperty("hostAliases");
        Assert.True(hostAliases.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal("#/$defs/dnsHost", hostAliases.GetProperty("items").GetProperty("$ref").GetString());
        var dnsHost = rulesRoot.GetProperty("$defs").GetProperty("dnsHost");
        Assert.Equal(2, dnsHost.GetProperty("oneOf").GetArrayLength());
        var ignoredParameters = rulesRoot.GetProperty("properties").GetProperty("ignoredQueryParameters");
        Assert.True(ignoredParameters.GetProperty("uniqueItems").GetBoolean());
        Assert.Equal("^[A-Za-z0-9_.-]+$", ignoredParameters.GetProperty("items").GetProperty("pattern").GetString());
        var thresholds = rulesRoot.GetProperty("$defs").GetProperty("thresholds");
        Assert.False(thresholds.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            [
                "minimumSearchImpressions", "maximumLowImpressions", "minimumAnalyticsSessions", "lowCtr",
                "lowEngagementRate", "highEngagementRate", "opportunityPositionMinimum", "opportunityPositionMaximum"
            ],
            thresholds.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "#/$defs/count",
            thresholds.GetProperty("properties").GetProperty("minimumSearchImpressions").GetProperty("$ref").GetString());
        Assert.Equal(9223372036854775807, rulesRoot.GetProperty("$defs").GetProperty("count").GetProperty("maximum").GetInt64());
        Assert.Equal("#/$defs/ratio", thresholds.GetProperty("properties").GetProperty("lowCtr").GetProperty("$ref").GetString());
        Assert.Equal(1, rulesRoot.GetProperty("$defs").GetProperty("ratio").GetProperty("maximum").GetDouble());
        Assert.Equal(
            "#/$defs/positiveFiniteNumber",
            thresholds.GetProperty("properties").GetProperty("opportunityPositionMinimum").GetProperty("$ref").GetString());
        Assert.Equal(
            0,
            rulesRoot.GetProperty("$defs").GetProperty("positiveFiniteNumber").GetProperty("exclusiveMinimum").GetDouble());
        var priorities = rulesRoot.GetProperty("$defs").GetProperty("priorities");
        Assert.False(priorities.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["snippetMismatch", "landingQuality", "discoverability", "positionOpportunity"],
            priorities.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.All(
            priorities.GetProperty("properties").EnumerateObject(),
            property => Assert.Equal(
                ["P0", "P1", "P2"],
                property.Value.GetProperty("enum").EnumerateArray().Select(value => value.GetString())));
        var semanticLayer = rulesRoot.GetProperty("$comment").GetString()!;
        Assert.Contains("SeoInsightsRuleProfileReader", semanticLayer, StringComparison.Ordinal);
        Assert.Contains("opportunityPositionMinimum <= opportunityPositionMaximum", semanticLayer, StringComparison.Ordinal);
        Assert.Contains("case-insensitive", semanticLayer, StringComparison.Ordinal);
        Assert.Contains("root-dot-normalized", semanticLayer, StringComparison.Ordinal);
    }

    [Fact]
    public void SeoInsightsRulesSchema_EnforcesExpressibleDnsHostBoundaryCorpus()
    {
        using var rulesSchema = ReadJson("docs", "schemas", "seo-insights-rules.v1.schema.json");
        var dnsHost = rulesSchema.RootElement.GetProperty("$defs").GetProperty("dnsHost");
        var maximum = MaximumDnsHost();
        var overlong = maximum + "a";

        Assert.True(DnsHostSchemaAccepts(dnsHost, "example.com"));
        Assert.True(DnsHostSchemaAccepts(dnsHost, "xn--bcher-kva.example"));
        Assert.True(DnsHostSchemaAccepts(dnsHost, "xn--bcher-kva.example."));
        Assert.True(DnsHostSchemaAccepts(dnsHost, maximum));
        Assert.True(DnsHostSchemaAccepts(dnsHost, maximum + "."));
        Assert.False(DnsHostSchemaAccepts(dnsHost, overlong));
        Assert.False(DnsHostSchemaAccepts(dnsHost, overlong + "."));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "127.1"));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "2130706433"));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "2130706433."));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "0x7f000001"));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "999999999999999999999"));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "0xFFFFFFFFFFFFFFFF"));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "192.0.2.1"));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "::1"));
        Assert.False(DnsHostSchemaAccepts(dnsHost, "[::1]"));
    }

    [Fact]
    public void SeoInsightsReportSchema_UnmatchedRequiresErrorCodeAndAllowsOnlyFixedNormalizerCodesOrNull()
    {
        using var reportSchema = ReadJson("docs", "schemas", "seo-insights-report.v1.schema.json");
        var unmatched = reportSchema.RootElement.GetProperty("$defs").GetProperty("unmatched");
        Assert.Contains(
            "errorCode",
            unmatched.GetProperty("required").EnumerateArray().Select(value => value.GetString()));

        var errorCode = unmatched.GetProperty("properties").GetProperty("errorCode");

        Assert.True(errorCode.TryGetProperty("enum", out var enumValues), "errorCode must declare a fixed enum.");
        var actual = enumValues.EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString())
            .ToHashSet(StringComparer.Ordinal);
        var expected = new HashSet<string?>(StringComparer.Ordinal)
        {
            null,
            "invalid_url",
            "unsupported_scheme",
            "credentials_not_allowed",
            "host_not_allowed"
        };

        Assert.Equal(expected.Count, actual.Count);
        Assert.True(expected.SetEquals(actual));
    }

    [Fact]
    public void SeoObservationSchemas_RejectWhitespaceOnlyStrings()
    {
        using var observationSchema = ReadJson("docs", "schemas", "seo-observation.v1.schema.json");
        var observationDefs = observationSchema.RootElement.GetProperty("$defs");
        AssertStringSchemaAccepts(
            observationDefs.GetProperty("window").GetProperty("properties").GetProperty("timeZone"),
            "Asia/Kuala_Lumpur",
            " \t");
        AssertStringSchemaAccepts(
            observationDefs.GetProperty("gscRow").GetProperty("properties").GetProperty("url"),
            "https://example.com/a/",
            "   ");

        using var reportSchema = ReadJson("docs", "schemas", "seo-insights-report.v1.schema.json");
        var reportDefs = reportSchema.RootElement.GetProperty("$defs");
        AssertStringSchemaAccepts(
            reportDefs.GetProperty("window").GetProperty("properties").GetProperty("timeZone"),
            "UTC",
            " ");
        AssertStringSchemaAccepts(
            reportDefs.GetProperty("unmatched").GetProperty("properties").GetProperty("originalUrl"),
            "https://example.com/missing/",
            "\n");
        AssertStringSchemaAccepts(
            reportDefs.GetProperty("ambiguous").GetProperty("properties").GetProperty("originalUrl"),
            "https://example.com/shared/",
            "\t ");
    }

    [Fact]
    public void SeoObservationSchemas_RejectNumericOverflow()
    {
        using var observationSchema = ReadJson("docs", "schemas", "seo-observation.v1.schema.json");
        var observationDefs = observationSchema.RootElement.GetProperty("$defs");
        AssertIntegerSchemaAccepts(
            observationDefs.GetProperty("gscRow").GetProperty("properties").GetProperty("impressions"),
            "9223372036854775807",
            "9223372036854775808");
        AssertNumberSchemaAccepts(
            observationDefs.GetProperty("gscRow").GetProperty("properties").GetProperty("averagePosition"),
            "1.7976931348623157e308",
            "1.7976931348623159e308");

        using var reportSchema = ReadJson("docs", "schemas", "seo-insights-report.v1.schema.json");
        var reportDefs = reportSchema.RootElement.GetProperty("$defs");
        AssertIntegerSchemaAccepts(
            reportDefs.GetProperty("joinCounts").GetProperty("properties").GetProperty("sourceRows"),
            "9223372036854775807",
            "9223372036854775808");
        AssertNumberSchemaAccepts(
            reportDefs.GetProperty("nullableNumber"),
            "1.7976931348623157e308",
            "1e309");
    }

    [Fact]
    public void ActiveGuide_DocumentsSeoGeoAndIndexNowContracts()
    {
        var siteConfig = ReadText("guide", "user", "04-site-yaml-config.md");
        var seo = ReadText("guide", "user", "11-i18n-seo.md");
        var parameters = ReadText("guide", "user", "16-parameter-cheatsheet.md");
        var pluginGuide = ReadText("guide", "dev", "plugins.md");

        Assert.Contains("organization.type", siteConfig, StringComparison.Ordinal);
        Assert.Contains("organization.sameAs", siteConfig, StringComparison.Ordinal);
        Assert.Contains("noindexWhenEmpty", siteConfig, StringComparison.Ordinal);
        Assert.Contains("NewsMediaOrganization", seo, StringComparison.Ordinal);
        Assert.Contains("noindexWhenEmpty", parameters, StringComparison.Ordinal);

        Assert.Contains("indexnow submit", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("INDEXNOW_KEY", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("publish-url-snapshot.v1", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("publish-url-change-set.v1", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("osx-arm64", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("SHA-256", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("internal", pluginGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phase 1", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("Deploy the generated output to GitHub Pages", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("rerun the same command", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("HTTP 200", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("response body exactly equals `INDEXNOW_KEY`", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("only then", pluginGuide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActiveGuide_DocumentsOfflineSeoObservabilityContract()
    {
        var guide = ReadText("guide", "user", "21-seo-insights.md");
        var index = ReadText("guide", "user", "README.md");
        var outputs = ReadText("guide", "user", "10-built-in-outputs.md");
        var cli = ReadText("guide", "user", "12-cli-reference.md");

        Assert.True(File.Exists(Path.Combine(RepoRoot, "guide", "user", "21-seo-insights.md")));
        Assert.Contains("[21 SEO Insights](21-seo-insights.md)", index, StringComparison.Ordinal);
        Assert.Contains(".bukit/seo-route-map.json", outputs, StringComparison.Ordinal);
        Assert.Contains(".bukit/seo-insights-report.json", outputs, StringComparison.Ordinal);

        Assert.Contains("seo-route-map.v1", guide, StringComparison.Ordinal);
        Assert.Contains("seo-observation.v1", guide, StringComparison.Ordinal);
        Assert.Contains("seo-insights-rules.v1", guide, StringComparison.Ordinal);
        Assert.Contains("seo-insights-report.v1", guide, StringComparison.Ordinal);
        Assert.Contains("https://developers.google.com/webmaster-tools/v1/searchanalytics/query", guide, StringComparison.Ordinal);
        Assert.Contains("build -> route map -> external collector/plugin -> observations -> insights", guide, StringComparison.Ordinal);
        Assert.Contains("offline", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not authenticate to Google", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not access the network", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not prove causation", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ranking guarantee", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("automatic edit", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unmatched", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ambiguous", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never chooses a winner", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Observation-row `url` values must be absolute HTTP(S) URLs", guide, StringComparison.Ordinal);
        Assert.Contains("`siteHost` or `hostAliases`", guide, StringComparison.Ordinal);
        Assert.Contains("relative observation values are `invalid_url` and remain `unmatched`", guide, StringComparison.Ordinal);
        Assert.Contains("Route-map `canonical` values may be a leading-slash relative path or an absolute HTTP(S) URL", guide, StringComparison.Ordinal);
        Assert.Contains("`keyEvents` may exceed `sessions`", guide, StringComparison.Ordinal);
        Assert.Contains("`keyEventRate` may exceed 1", guide, StringComparison.Ordinal);
        Assert.Contains("sessions >= `minimumAnalyticsSessions`", guide, StringComparison.Ordinal);
        Assert.Contains("engagement rate >= `highEngagementRate`", guide, StringComparison.Ordinal);
        Assert.Contains("not Core defaults", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("write", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before returning exit code 1", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not publishable", guide, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("\"provider\": \"google-search-console\"", guide, StringComparison.Ordinal);
        Assert.Contains("\"provider\": \"google-analytics-4\"", guide, StringComparison.Ordinal);
        Assert.Contains("\"scope\": \"google-organic\"", guide, StringComparison.Ordinal);
        Assert.Contains("\"schema\": \"https://bukit.dev/schemas/seo-insights-rules.v1.json\"", guide, StringComparison.Ordinal);

        foreach (var option in new[] { "--dir", "--routes", "--observations", "--rules", "--out", "--strict-join" })
        {
            Assert.Contains(option, cli, StringComparison.Ordinal);
        }

        Assert.Contains("bukit seo insights", cli, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGuide_DocumentsArticleTrustGraphContract()
    {
        var guide = ReadText("guide", "user", "17-geo.md");
        var seo = ReadText("docs", "seo.md");

        Assert.Contains("relation: citation", guide, StringComparison.Ordinal);
        Assert.Contains("relation: based-on", guide, StringComparison.Ordinal);
        Assert.Contains("mainEntityOfPage", guide, StringComparison.Ordinal);
        Assert.Contains("does not prove authority or ranking", guide, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("mainEntityOfPage", seo, StringComparison.Ordinal);
        Assert.Contains("isBasedOn", seo, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGuide_DocumentsLlmsCurationContract()
    {
        var guide = ReadText("guide", "user", "17-geo.md");

        Assert.Contains("visibility: auto", guide, StringComparison.Ordinal);
        Assert.Contains("visibility: include", guide, StringComparison.Ordinal);
        Assert.Contains("visibility: exclude", guide, StringComparison.Ordinal);
        Assert.Contains("tier: primary", guide, StringComparison.Ordinal);
        Assert.Contains("tier: optional", guide, StringComparison.Ordinal);
        Assert.Contains("priority:", guide, StringComparison.Ordinal);
        Assert.Contains("-100", guide, StringComparison.Ordinal);
        Assert.Contains("100", guide, StringComparison.Ordinal);
        Assert.Contains("non-indexable pages are always excluded", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("include", guide, StringComparison.Ordinal);
        Assert.Contains("noindex", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not signal priority to external AI", guide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActiveGuide_DocumentsMinimumCollectionIndexPolicyContract()
    {
        var siteConfig = ReadText("guide", "user", "04-site-yaml-config.md");
        var seo = ReadText("docs", "seo.md");

        Assert.Contains("indexPolicy", siteConfig, StringComparison.Ordinal);
        Assert.Contains("minimumItems", siteConfig, StringComparison.Ordinal);
        Assert.Contains("belowMinimum", siteConfig, StringComparison.Ordinal);
        Assert.Contains("noindex-follow", siteConfig, StringComparison.Ordinal);
        Assert.Contains("strictly below", siteConfig, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("minimumItems: 1", siteConfig, StringComparison.Ordinal);
        Assert.Contains("fails configuration validation", siteConfig, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("indexPolicy", seo, StringComparison.Ordinal);
        Assert.Contains("strictly below", seo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("indexable: false", seo, StringComparison.Ordinal);
        Assert.Contains("feeds", seo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuestionCoverageSchemas_MatchPublicContracts()
    {
        using var targetMapSchema = ReadJson("docs", "schemas", "seo-question-target-map.v1.schema.json");
        var targetRoot = targetMapSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", targetRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/seo-question-target-map.v1.json", targetRoot.GetProperty("$id").GetString());
        Assert.False(targetRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "generatedAt", "questions"],
            targetRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("1.0", targetRoot.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        var questions = targetRoot.GetProperty("properties").GetProperty("questions");
        Assert.Equal(100000, questions.GetProperty("maxItems").GetInt32());
        var questionItem = questions.GetProperty("items");
        Assert.False(questionItem.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["questionKey", "topicKey", "intent", "locale", "priority", "coveredRouteKeys"],
            questionItem.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var questionProperties = questionItem.GetProperty("properties");
        Assert.Equal("^question:sha256:[0-9a-f]{64}$", questionProperties.GetProperty("questionKey").GetProperty("pattern").GetString());
        Assert.Equal("^topic:sha256:[0-9a-f]{64}$", questionProperties.GetProperty("topicKey").GetProperty("pattern").GetString());
        Assert.Equal(
            ["informational", "navigational", "commercial", "transactional", "other"],
            questionProperties.GetProperty("intent").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            ["P0", "P1", "P2"],
            questionProperties.GetProperty("priority").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^route:sha256:[0-9a-f]{64}$",
            questionProperties.GetProperty("coveredRouteKeys").GetProperty("items").GetProperty("pattern").GetString());

        using var observationSchema = ReadJson("docs", "schemas", "search-question-observation.v1.schema.json");
        var observationRoot = observationSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", observationRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/search-question-observation.v1.json", observationRoot.GetProperty("$id").GetString());
        Assert.False(observationRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "provider", "scope", "collectedAt", "collectionMethod", "window", "rows"],
            observationRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var observationProperties = observationRoot.GetProperty("properties");
        Assert.Equal("google-search-console", observationProperties.GetProperty("provider").GetProperty("const").GetString());
        Assert.Equal("google-organic", observationProperties.GetProperty("scope").GetProperty("const").GetString());
        Assert.Equal(
            ["api", "export", "manual"],
            observationProperties.GetProperty("collectionMethod").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        var observationRows = observationProperties.GetProperty("rows");
        Assert.Equal(100000, observationRows.GetProperty("maxItems").GetInt32());
        var observationRow = observationRows.GetProperty("items");
        Assert.False(observationRow.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["questionKey", "topicKey", "url", "locale", "device", "impressions", "clicks", "averagePosition"],
            observationRow.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var observationRowProperties = observationRow.GetProperty("properties");
        Assert.Equal(
            ["desktop", "mobile", "tablet", "unknown"],
            observationRowProperties.GetProperty("device").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(0, observationRowProperties.GetProperty("impressions").GetProperty("minimum").GetInt32());
        Assert.Equal(0, observationRowProperties.GetProperty("clicks").GetProperty("minimum").GetInt32());
        Assert.Equal(0, observationRowProperties.GetProperty("averagePosition").GetProperty("minimum").GetInt32());
        var observationWindow = observationRoot.GetProperty("$defs").GetProperty("window");
        Assert.Equal(
            ["startDate", "endDate", "timeZone"],
            observationWindow.GetProperty("required").EnumerateArray().Select(value => value.GetString()));

        using var reportSchema = ReadJson("docs", "schemas", "seo-question-insights-report.v1.schema.json");
        var reportRoot = reportSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", reportRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/seo-question-insights-report.v1.json", reportRoot.GetProperty("$id").GetString());
        Assert.False(reportRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "generatedAt", "window", "sources", "joinQuality", "questions", "unmatchedTargets", "unmatchedObservations", "ambiguousObservations"],
            reportRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var joinQuality = reportRoot.GetProperty("properties").GetProperty("joinQuality");
        Assert.Equal(
            ["overall", "targets", "observations"],
            joinQuality.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var joinCounts = reportRoot.GetProperty("$defs").GetProperty("joinCounts");
        Assert.Equal(
            ["sourceRows", "matchedRows", "unmatchedRows", "ambiguousRows"],
            joinCounts.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var unmatchedTarget = reportRoot.GetProperty("properties").GetProperty("unmatchedTargets").GetProperty("items");
        Assert.False(unmatchedTarget.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["questionKey", "routeKey", "errorCode"],
            unmatchedTarget.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("route_key_not_found", unmatchedTarget.GetProperty("properties").GetProperty("errorCode").GetProperty("const").GetString());
        Assert.Equal(
            "^(?![Hh][Tt][Tt][Pp][Ss]?://[^/?#]*@)\\S+$",
            reportRoot.GetProperty("properties").GetProperty("unmatchedObservations").GetProperty("items")
                .GetProperty("properties").GetProperty("url").GetProperty("pattern").GetString());
        Assert.Equal(
            ["invalid_url", "unsupported_scheme", "credentials_not_allowed", "host_not_allowed", "question_key_not_found"],
            reportRoot.GetProperty("properties").GetProperty("unmatchedObservations").GetProperty("items")
                .GetProperty("properties").GetProperty("errorCode").GetProperty("oneOf")[0]
                .GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void GenerativeCitationSchemas_MatchPublicContracts()
    {
        using var observationSchema = ReadJson("docs", "schemas", "generative-answer-observation.v1.schema.json");
        var observationRoot = observationSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", observationRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/generative-answer-observation.v1.json", observationRoot.GetProperty("$id").GetString());
        Assert.False(observationRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "engine", "promptSetVersion", "locale", "collectedAt", "collectionMethod", "rows"],
            observationRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("1.0", observationRoot.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        var observationProperties = observationRoot.GetProperty("properties");
        Assert.Equal(1, observationProperties.GetProperty("engine").GetProperty("minLength").GetInt32());
        Assert.Equal(1, observationProperties.GetProperty("promptSetVersion").GetProperty("minLength").GetInt32());
        Assert.Equal(1, observationProperties.GetProperty("locale").GetProperty("minLength").GetInt32());
        Assert.Equal(
            ["api", "browser-export", "manual"],
            observationProperties.GetProperty("collectionMethod").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        var rows = observationProperties.GetProperty("rows");
        Assert.Equal(100000, rows.GetProperty("maxItems").GetInt32());
        var row = rows.GetProperty("items");
        Assert.False(row.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["questionKey", "promptVariant", "runIndex", "brandMentioned", "siteCited", "citedUrls", "citationPosition", "answerHash"],
            row.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var rowProperties = row.GetProperty("properties");
        Assert.Equal("^question:sha256:[0-9a-f]{64}$", rowProperties.GetProperty("questionKey").GetProperty("pattern").GetString());
        Assert.Equal(0, rowProperties.GetProperty("promptVariant").GetProperty("minimum").GetInt32());
        Assert.Equal(9999, rowProperties.GetProperty("promptVariant").GetProperty("maximum").GetInt32());
        Assert.Equal(0, rowProperties.GetProperty("runIndex").GetProperty("minimum").GetInt32());
        Assert.Equal(9999, rowProperties.GetProperty("runIndex").GetProperty("maximum").GetInt32());
        Assert.Equal("^answer:sha256:[0-9a-f]{64}$", rowProperties.GetProperty("answerHash").GetProperty("pattern").GetString());
        Assert.Equal(1, rowProperties.GetProperty("citationPosition").GetProperty("minimum").GetInt32());
        Assert.Equal(100, rowProperties.GetProperty("citedUrls").GetProperty("maxItems").GetInt32());

        using var reportSchema = ReadJson("docs", "schemas", "generative-citation-report.v1.schema.json");
        var reportRoot = reportSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", reportRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/generative-citation-report.v1.json", reportRoot.GetProperty("$id").GetString());
        Assert.False(reportRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "generatedAt", "sources", "overall", "engines", "questions", "unmatchedCitedUrls", "ambiguousCitedUrls", "externalCitedUrls", "joinQuality"],
            reportRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var stats = reportRoot.GetProperty("$defs").GetProperty("stats");
        Assert.Equal(
            ["runs", "brandMentions", "brandMentionRate", "siteCitations", "siteCitationRate"],
            stats.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var joinQuality = reportRoot.GetProperty("properties").GetProperty("joinQuality");
        Assert.False(joinQuality.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["sourceRows", "matchedRows", "unmatchedRows", "ambiguousRows"],
            joinQuality.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var unmatched = reportRoot.GetProperty("properties").GetProperty("unmatchedCitedUrls").GetProperty("items");
        Assert.False(unmatched.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["url", "errorCode"],
            unmatched.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^(?![Hh][Tt][Tt][Pp][Ss]?://[^/?#]*@)\\S+$",
            unmatched.GetProperty("properties").GetProperty("url").GetProperty("pattern").GetString());
        var ambiguous = reportRoot.GetProperty("properties").GetProperty("ambiguousCitedUrls").GetProperty("items");
        Assert.False(ambiguous.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["url", "candidateRouteKeys"],
            ambiguous.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var external = reportRoot.GetProperty("properties").GetProperty("externalCitedUrls").GetProperty("items");
        Assert.False(external.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["url", "citationRuns"],
            external.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void ExternalAuthoritySchemas_MatchPublicContracts()
    {
        using var observationSchema = ReadJson("docs", "schemas", "external-authority-observation.v1.schema.json");
        var observationRoot = observationSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", observationRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/external-authority-observation.v1.json", observationRoot.GetProperty("$id").GetString());
        Assert.False(observationRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "provider", "collectedAt", "collectionMethod", "rows"],
            observationRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("1.0", observationRoot.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        var observationProperties = observationRoot.GetProperty("properties");
        Assert.Equal(1, observationProperties.GetProperty("provider").GetProperty("minLength").GetInt32());
        Assert.Equal(
            ["api", "export", "manual"],
            observationProperties.GetProperty("collectionMethod").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        var rows = observationProperties.GetProperty("rows");
        Assert.Equal(100000, rows.GetProperty("maxItems").GetInt32());
        var row = rows.GetProperty("items");
        Assert.False(row.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["sourceUrl", "sourceType", "observedAt", "status", "questionKey", "topicKey", "entityKey", "contextHash", "citedUrls"],
            row.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(3, row.GetProperty("anyOf").GetArrayLength());
        var rowProperties = row.GetProperty("properties");
        Assert.Equal("^[Hh][Tt][Tt][Pp][Ss]?://(?![^/?#]*@)", rowProperties.GetProperty("sourceUrl").GetProperty("pattern").GetString());
        Assert.Equal(
            ["official", "regulator", "research", "news", "association", "repository", "forum", "other"],
            rowProperties.GetProperty("sourceType").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            ["active", "deleted", "unavailable"],
            rowProperties.GetProperty("status").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("^question:sha256:[0-9a-f]{64}$", rowProperties.GetProperty("questionKey").GetProperty("pattern").GetString());
        Assert.Equal("^topic:sha256:[0-9a-f]{64}$", rowProperties.GetProperty("topicKey").GetProperty("pattern").GetString());
        Assert.Equal("^entity:sha256:[0-9a-f]{64}$", rowProperties.GetProperty("entityKey").GetProperty("pattern").GetString());
        Assert.Equal("^context:sha256:[0-9a-f]{64}$", rowProperties.GetProperty("contextHash").GetProperty("pattern").GetString());
        Assert.Equal("^[Hh][Tt][Tt][Pp][Ss]?://(?![^/?#]*@)", rowProperties.GetProperty("citedUrls").GetProperty("items").GetProperty("pattern").GetString());

        using var reportSchema = ReadJson("docs", "schemas", "external-authority-report.v1.schema.json");
        var reportRoot = reportSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", reportRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/external-authority-report.v1.json", reportRoot.GetProperty("$id").GetString());
        Assert.False(reportRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "generatedAt", "sources", "overall", "providers", "sourceTypes", "statuses", "routes", "unmatchedCitedUrls", "ambiguousCitedUrls", "joinQuality"],
            reportRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var overall = reportRoot.GetProperty("properties").GetProperty("overall");
        Assert.False(overall.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["sources", "activeSources", "activeCitedRoutes"],
            overall.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var sourceRecord = reportRoot.GetProperty("properties").GetProperty("sources").GetProperty("items");
        Assert.False(sourceRecord.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["provider", "sourceType", "status", "observedAt", "sourceUrl", "contextHash", "citedRouteKeys"],
            sourceRecord.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^[Hh][Tt][Tt][Pp][Ss]?://(?![^/?#]*@)",
            sourceRecord.GetProperty("properties").GetProperty("sourceUrl").GetProperty("pattern").GetString());
        var joinQuality = reportRoot.GetProperty("properties").GetProperty("joinQuality");
        Assert.False(joinQuality.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["sourceRows", "matchedRows", "unmatchedRows", "ambiguousRows"],
            joinQuality.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var unmatched = reportRoot.GetProperty("properties").GetProperty("unmatchedCitedUrls").GetProperty("items");
        Assert.False(unmatched.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["url", "errorCode"],
            unmatched.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^(?![Hh][Tt][Tt][Pp][Ss]?://[^/?#]*@)\\S+$",
            unmatched.GetProperty("properties").GetProperty("url").GetProperty("pattern").GetString());
        var ambiguous = reportRoot.GetProperty("properties").GetProperty("ambiguousCitedUrls").GetProperty("items");
        Assert.False(ambiguous.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["url", "candidateRouteKeys"],
            ambiguous.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(2, ambiguous.GetProperty("properties").GetProperty("candidateRouteKeys").GetProperty("minItems").GetInt32());
    }

    [Fact]
    public void ActiveGuide_DocumentsQuestionCoverageContract()
    {
        var guide = ReadText("guide", "user", "22-seo-question-insights.md");
        var index = ReadText("guide", "user", "README.md");
        var cli = ReadText("guide", "user", "12-cli-reference.md");

        Assert.Contains("[22 SEO Question Insights](22-seo-question-insights.md)", index, StringComparison.Ordinal);

        Assert.Contains("seo-question-target-map.v1", guide, StringComparison.Ordinal);
        Assert.Contains("search-question-observation.v1", guide, StringComparison.Ordinal);
        Assert.Contains("seo-question-insights-report.v1", guide, StringComparison.Ordinal);
        Assert.Contains("question:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("topic:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("route:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("google-search-console", guide, StringComparison.Ordinal);
        Assert.Contains("never receives raw", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("low-volume", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("human review", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not prove demand", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not prove causation", guide, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("bukit seo question-insights", cli, StringComparison.Ordinal);
        Assert.Contains("--targets", cli, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGuide_DocumentsGenerativeCitationContract()
    {
        var guide = ReadText("guide", "user", "23-generative-citation-insights.md");
        var index = ReadText("guide", "user", "README.md");
        var cli = ReadText("guide", "user", "12-cli-reference.md");

        Assert.Contains("[23 Generative Citation Insights](23-generative-citation-insights.md)", index, StringComparison.Ordinal);

        Assert.Contains("generative-answer-observation.v1", guide, StringComparison.Ordinal);
        Assert.Contains("generative-citation-report.v1", guide, StringComparison.Ordinal);
        Assert.Contains("question:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("answer:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("route:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("promptSetVersion", guide, StringComparison.Ordinal);
        Assert.Contains("browser-export", guide, StringComparison.Ordinal);
        Assert.Contains("never receives raw", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("human review", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fixed question set", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("versioned prompt set", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repeated runs", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("observed changes do not prove causation", guide, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("bukit seo generative-insights", cli, StringComparison.Ordinal);
        Assert.Contains("seo generative-insights", index, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGuide_DocumentsExternalAuthorityContract()
    {
        var guide = ReadText("guide", "user", "24-external-authority-insights.md");
        var index = ReadText("guide", "user", "README.md");
        var cli = ReadText("guide", "user", "12-cli-reference.md");

        Assert.Contains("[24 External Authority Insights](24-external-authority-insights.md)", index, StringComparison.Ordinal);

        Assert.Contains("external-authority-observation.v1", guide, StringComparison.Ordinal);
        Assert.Contains("external-authority-report.v1", guide, StringComparison.Ordinal);
        Assert.Contains("question:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("context:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("route:sha256:", guide, StringComparison.Ordinal);
        Assert.Contains("official`, `regulator", guide, StringComparison.Ordinal);
        Assert.Contains("deleted", guide, StringComparison.Ordinal);
        Assert.Contains("unavailable", guide, StringComparison.Ordinal);
        Assert.Contains("does not score authority", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Automated posting, commenting, voting, messaging, and", guide, StringComparison.Ordinal);
        Assert.Contains("1. approved API use case and credentials boundary;", guide, StringComparison.Ordinal);
        Assert.Contains("2. measured incremental value over GSC/GA4/generative observations;", guide, StringComparison.Ordinal);
        Assert.Contains("3. fixed subreddit/query scope, rate and retention policy;", guide, StringComparison.Ordinal);
        Assert.Contains("4. deletion/unavailable synchronization;", guide, StringComparison.Ordinal);
        Assert.Contains("5. read-only commands only;", guide, StringComparison.Ordinal);
        Assert.Contains("6. output validates against external-authority-observation.v1.", guide, StringComparison.Ordinal);
        Assert.Contains("Core never receives raw", guide, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("bukit seo authority-insights", cli, StringComparison.Ordinal);
        Assert.Contains("seo authority-insights", index, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtectedReferenceTrees_AreNotActiveInputs()
    {
        var protectedNames = new[]
        {
            ".github/workflows-0.1",
            "workflows-0.1",
            "scripts-0.1",
            "scripts-0.2",
            "guide-0.1",
            "guide-0.2"
        };
        var activeRoots = new[] { ".github/workflows", "scripts", "src", "guide" };
        var violations = new List<string>();

        foreach (var relativeRoot in activeRoots)
        {
            var root = Path.Combine(RepoRoot, relativeRoot);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                        Path.GetFileName(path) is not "active-workflow-boundary.sh" and not "active-workflow-boundary-self-test.sh"))
            {
                var relativePath = Path.GetRelativePath(RepoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
                var lineNumber = 0;
                foreach (var line in File.ReadLines(path))
                {
                    lineNumber++;
                    if (protectedNames.Any(name => line.Contains(name, StringComparison.Ordinal)) &&
                        !IsAllowedProtectedReference(relativePath, line))
                    {
                        violations.Add($"{relativePath}:{lineNumber}");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    private static bool IsAllowedProtectedReference(string relativePath, string line)
        => relativePath switch
        {
            "guide/README.md" =>
                line.Contains("`guide-0.2` snapshot informed its information architecture", StringComparison.Ordinal) ||
                line == "If present, `guide-0.1`, `guide-0.2`, `scripts-0.1`, and `scripts-0.2` are",
            "guide/dev/agent-task-workflow.md" =>
                line == "- Do not create, synchronize, or modify `guide-0.1/`, `guide-0.2/`," ||
                line == "  `scripts-0.1/`, or `scripts-0.2/` by default; their absence is valid. Touch",
            _ => false
        };

    [Fact]
    public void IndexNow_IsInSolutionsAndInternalInstallManifest()
    {
        var pluginProjects = ReadSolutionProjectPaths("bukit-plugins.slnx");
        Assert.Contains("src/Bukit-Plugins/Bukit.IndexNow/Bukit.IndexNow.csproj", pluginProjects);
        Assert.Contains("src/Bukit-Plugins/Bukit.Plugin.IndexNow/Bukit.Plugin.IndexNow.csproj", pluginProjects);

        var testProjects = ReadSolutionProjectPaths("bukit-test.slnx");
        Assert.Contains("tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj", testProjects);

        using var install = ReadJson("docs", "internal", "seo-geo-wp1-osx-arm64.install.json");
        Assert.Equal(
            "d186278302caa3b757d1a233468c4fe7e89766b2",
            install.RootElement.GetProperty("sourceCommit").GetString());
        var core = install.RootElement.GetProperty("core");
        Assert.Equal("2.0.0", core.GetProperty("version").GetString());
        Assert.Equal("osx-arm64", core.GetProperty("rid").GetString());
        Assert.Matches("^[0-9a-f]{64}$", core.GetProperty("archiveSha256").GetString());
        Assert.False(string.IsNullOrWhiteSpace(core.GetProperty("installTarget").GetString()));

        var plugin = install.RootElement.GetProperty("plugin");
        Assert.Equal("indexnow", plugin.GetProperty("id").GetString());
        Assert.Equal("0.1.0", plugin.GetProperty("version").GetString());
        Assert.Equal("bukit-plugin-v1", plugin.GetProperty("protocol").GetString());
        Assert.Equal("osx-arm64", plugin.GetProperty("rid").GetString());
        Assert.Matches("^[0-9a-f]{64}$", plugin.GetProperty("entrySha256").GetString());
        Assert.Matches("^[0-9a-f]{64}$", plugin.GetProperty("packageSha256").GetString());
        Assert.False(string.IsNullOrWhiteSpace(plugin.GetProperty("installTarget").GetString()));
        Assert.Equal(
            "stage/plugins/indexnow/bin/osx-arm64/bukit-plugin-indexnow",
            plugin.GetProperty("stagedEntry").GetString());
        Assert.Equal(
            "plugins/indexnow/bin/osx-arm64/bukit-plugin-indexnow",
            plugin.GetProperty("packageEntry").GetString());
        Assert.NotEqual(plugin.GetProperty("stagedEntry").GetString(), plugin.GetProperty("packageEntry").GetString());
        Assert.False(plugin.TryGetProperty("entry", out _));

        var manifest = ReadText(
            "src", "Bukit-Plugins", "Bukit.Plugin.IndexNow", "examples", "minimal", "plugins", "indexnow", "plugin.yaml");
        Assert.Contains($"sha256: {plugin.GetProperty("entrySha256").GetString()}", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("0000000000000000000000000000000000000000000000000000000000000000", manifest, StringComparison.Ordinal);

        var serialized = install.RootElement.GetRawText();
        Assert.DoesNotContain("INDEXNOW_KEY", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"state\"", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"report\"", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument ReadJson(params string[] segments)
        => JsonDocument.Parse(ReadText(segments));

    private static void AssertStringSchemaAccepts(JsonElement schema, string valid, string invalid)
    {
        Assert.True(StringSchemaAccepts(schema, valid));
        Assert.False(StringSchemaAccepts(schema, invalid));
    }

    private static bool StringSchemaAccepts(JsonElement schema, string value)
    {
        if (schema.TryGetProperty("minLength", out var minLength) && value.Length < minLength.GetInt32())
        {
            return false;
        }

        if (schema.TryGetProperty("maxLength", out var maxLength) && value.Length > maxLength.GetInt32())
        {
            return false;
        }

        return !schema.TryGetProperty("pattern", out var pattern) ||
               Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant);
    }

    private static bool DnsHostSchemaAccepts(JsonElement schema, string value)
        => schema.GetProperty("oneOf").EnumerateArray().Count(branch => StringSchemaAccepts(branch, value)) == 1;

    private static string MaximumDnsHost()
        => $"{new string('a', 63)}.{new string('b', 63)}.{new string('c', 63)}.{new string('d', 61)}";

    private static void AssertIntegerSchemaAccepts(JsonElement schema, string valid, string invalid)
    {
        Assert.True(IntegerSchemaAccepts(schema, valid));
        Assert.False(IntegerSchemaAccepts(schema, invalid));
    }

    private static bool IntegerSchemaAccepts(JsonElement schema, string value)
    {
        var number = BigInteger.Parse(value, CultureInfo.InvariantCulture);
        if (schema.TryGetProperty("minimum", out var minimum) &&
            number < BigInteger.Parse(minimum.GetRawText(), CultureInfo.InvariantCulture))
        {
            return false;
        }

        return !schema.TryGetProperty("maximum", out var maximum) ||
               number <= BigInteger.Parse(maximum.GetRawText(), CultureInfo.InvariantCulture);
    }

    private static void AssertNumberSchemaAccepts(JsonElement schema, string valid, string invalid)
    {
        Assert.True(NumberSchemaAccepts(schema, valid));
        Assert.False(NumberSchemaAccepts(schema, invalid));
    }

    private static bool NumberSchemaAccepts(JsonElement schema, string value)
    {
        var number = ParsePositiveNumber(value);

        if (schema.TryGetProperty("minimum", out var minimum) &&
            Compare(number, ParsePositiveNumber(minimum.GetRawText())) < 0)
        {
            return false;
        }

        return !schema.TryGetProperty("maximum", out var maximum) ||
               Compare(number, ParsePositiveNumber(maximum.GetRawText())) <= 0;
    }

    private static PositiveNumber ParsePositiveNumber(string value)
    {
        var exponentIndex = value.IndexOf('e');
        if (exponentIndex < 0)
        {
            exponentIndex = value.IndexOf('E');
        }

        var mantissa = exponentIndex < 0 ? value : value[..exponentIndex];
        var exponent = exponentIndex < 0
            ? 0
            : int.Parse(value[(exponentIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var decimalIndex = mantissa.IndexOf('.');
        if (decimalIndex >= 0)
        {
            exponent -= mantissa.Length - decimalIndex - 1;
            mantissa = mantissa.Remove(decimalIndex, 1);
        }

        mantissa = mantissa.TrimStart('0');
        if (mantissa.Length == 0)
        {
            return new PositiveNumber(BigInteger.Zero, 0);
        }

        while (mantissa.EndsWith('0'))
        {
            mantissa = mantissa[..^1];
            exponent++;
        }

        return new PositiveNumber(BigInteger.Parse(mantissa, CultureInfo.InvariantCulture), exponent);
    }

    private static int Compare(PositiveNumber left, PositiveNumber right)
    {
        var commonExponent = Math.Min(left.Exponent, right.Exponent);
        var scaledLeft = left.Significand * BigInteger.Pow(10, left.Exponent - commonExponent);
        var scaledRight = right.Significand * BigInteger.Pow(10, right.Exponent - commonExponent);
        return scaledLeft.CompareTo(scaledRight);
    }

    private readonly record struct PositiveNumber(BigInteger Significand, int Exponent);

    private static string ReadText(params string[] segments)
        => File.ReadAllText(Path.Combine([RepoRoot, .. segments]));

    private static string[] ReadSolutionProjectPaths(string relativePath)
        => XDocument.Load(Path.Combine(RepoRoot, relativePath))
            .Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
