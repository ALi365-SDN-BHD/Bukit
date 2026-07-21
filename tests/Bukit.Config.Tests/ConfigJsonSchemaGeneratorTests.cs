using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigJsonSchemaGeneratorTests
{
    private static readonly IReadOnlySet<string> AllowedOpenMapObjectSchemaPaths = new HashSet<string>(StringComparer.Ordinal)
    {
        "$/properties/site/properties/permalinks",
        "$/properties/site/properties/collections",
        "$/properties/site/properties/plugins",
        "$/properties/site/properties/plugins/additionalProperties/oneOf[1]/properties/options",
        "$/properties/theme/properties/params",
        "$/properties/theme/properties/shortcodes",
        "$/properties/theme/properties/components",
        "$/properties/theme/properties/components/additionalProperties/properties/props",
        "$/properties/site/properties/menus",
        "$/properties/taxonomy/properties/pinFieldBySource",
        "$/properties/taxonomy/properties/pinOrderFieldBySource",
        "$/properties/content/properties/modelSchema/properties/fieldScopes"
    };

    [Fact]
    public void Generate_ReturnsObjectSchemaForAppConfigWithRequiredRoots()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.Contains("site", root.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("content", root.GetProperty("required").EnumerateArray().Select(x => x.GetString()));

        var properties = root.GetProperty("properties");
        Assert.True(properties.TryGetProperty("site", out var site));
        Assert.True(site.GetProperty("properties").TryGetProperty("title", out var title));
        Assert.Equal("string", title.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("build", out var build));
        Assert.Equal("boolean", build.GetProperty("properties").GetProperty("clean").GetProperty("type").GetString());
        var cspReportContract = root.GetProperty("allOf")[0];
        Assert.True(
            cspReportContract
                .GetProperty("then")
                .GetProperty("properties")
                .GetProperty("build")
                .GetProperty("properties")
                .GetProperty("report")
                .GetProperty("properties")
                .GetProperty("enabled")
                .GetProperty("const")
                .GetBoolean());
    }

    [Theory]
    [InlineData("{\"site\":{},\"build\":{\"report\":{\"enabled\":false}}}", true)]
    [InlineData("{\"site\":{\"analytics\":{\"csp\":{\"mode\":\"requirements-report\"}}}}", true)]
    [InlineData("{\"site\":{\"analytics\":{\"csp\":{\"mode\":\"requirements-report\"}}},\"build\":{\"report\":{\"enabled\":false}}}", false)]
    [InlineData("{\"site\":{\"analytics\":{\"csp\":{\"mode\":\"requirements-report\"}}},\"build\":{\"report\":{\"enabled\":true}}}", true)]
    public void Generate_CspReportConditionalMatchesRuntimeDefaultSemantics(
        string instanceJson,
        bool expected)
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        using var instance = JsonDocument.Parse(instanceJson);
        var conditional = schema.RootElement.GetProperty("allOf")[0];

        var conditionMatches = MatchesRequiredProperties(instance.RootElement, conditional.GetProperty("if"));
        var actual = !conditionMatches ||
                     MatchesRequiredProperties(instance.RootElement, conditional.GetProperty("then"));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Generate_StableConfigContractFields_MatchLoaderAndStrictValidatorNames()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.GetProperty("properties");

        var geo = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("seo")
            .GetProperty("properties")
            .GetProperty("geo")
            .GetProperty("properties");
        Assert.True(geo.TryGetProperty("enabled", out _));
        Assert.True(geo.TryGetProperty("llmsTxtMaxArticles", out _));
        Assert.True(geo.TryGetProperty("aiBotMode", out _));
        Assert.True(geo.TryGetProperty("aiBotAllowList", out _));
        Assert.True(geo.TryGetProperty("aiBotBlockList", out _));
        Assert.True(geo.TryGetProperty("llmsTxtOptionalLinks", out _));
        Assert.False(geo.TryGetProperty("faqSchema", out _));

        var seo = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("seo")
            .GetProperty("properties");
        Assert.Equal("string", seo.GetProperty("homeTitleTemplate").GetProperty("type").GetString());
        Assert.Equal("string", seo.GetProperty("pageTitleTemplate").GetProperty("type").GetString());
        Assert.Equal("string", seo.GetProperty("titleSeparator").GetProperty("type").GetString());

        var search = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("search")
            .GetProperty("properties");
        Assert.Equal("string", search.GetProperty("route").GetProperty("type").GetString());

        var analyticsSchema = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("analytics");
        var analytics = analyticsSchema.GetProperty("properties");
        Assert.Equal("boolean", analytics.GetProperty("enabled").GetProperty("type").GetString());
        Assert.Equal("boolean", analytics.GetProperty("productionOnly").GetProperty("type").GetString());
        var consent = analytics.GetProperty("consent");
        Assert.Equal(new[] { "google" }, consent.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        var googleConsent = consent.GetProperty("properties").GetProperty("google");
        Assert.Equal(
            new[] { "mode", "defaults" },
            googleConsent.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            new[] { "advanced" },
            googleConsent.GetProperty("properties").GetProperty("mode").GetProperty("enum")
                .EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, googleConsent.GetProperty("properties").GetProperty("waitForUpdateMs").GetProperty("minimum").GetInt32());
        Assert.Equal(5000, googleConsent.GetProperty("properties").GetProperty("waitForUpdateMs").GetProperty("maximum").GetInt32());
        var consentDefaults = googleConsent.GetProperty("properties").GetProperty("defaults");
        Assert.Equal(
            new[] { "adStorage", "analyticsStorage", "adUserData", "adPersonalization" },
            consentDefaults.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        foreach (var consentState in consentDefaults.GetProperty("properties").EnumerateObject())
        {
            Assert.Equal(
                new[] { "granted", "denied" },
                consentState.Value.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        }

        Assert.Equal(
            new[] { "requirements-report" },
            analytics.GetProperty("csp").GetProperty("properties").GetProperty("mode").GetProperty("enum")
                .EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(2, analyticsSchema.GetProperty("allOf").GetArrayLength());
        Assert.False(analytics.TryGetProperty("googleAnalyticsId", out _));
        Assert.False(analytics.TryGetProperty("disableInPreview", out _));
        var providerVariants = analytics.GetProperty("providers").GetProperty("items").GetProperty("oneOf");
        Assert.Equal(4, providerVariants.GetArrayLength());
        Assert.Equal(
            new[] { "google-analytics", "google-tag-manager", "plausible", "umami" },
            providerVariants.EnumerateArray()
                .Select(item => item.GetProperty("properties").GetProperty("type").GetProperty("const").GetString()));
        Assert.Equal(
            new[] { "type", "measurementId" },
            providerVariants[0].GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            new[] { "type", "containerId" },
            providerVariants[1].GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            new[] { "type", "domain", "snippetMode", "scriptUrl" },
            providerVariants[2].GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            new[] { "type", "websiteId", "scriptUrl" },
            providerVariants[3].GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        var plausibleProperties = providerVariants[2].GetProperty("properties");
        Assert.Equal("idn-hostname", plausibleProperties.GetProperty("domain").GetProperty("format").GetString());
        Assert.Equal(
            new[] { "site-specific", "legacy" },
            plausibleProperties.GetProperty("snippetMode").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        AssertScriptUrlSchemaPattern(
            plausibleProperties.GetProperty("scriptUrl").GetProperty("pattern").GetString());
        var plausibleModeUrlRules = providerVariants[2].GetProperty("allOf");
        Assert.Equal(2, plausibleModeUrlRules.GetArrayLength());
        Assert.Equal(
            "site-specific",
            plausibleModeUrlRules[0]
                .GetProperty("if")
                .GetProperty("properties")
                .GetProperty("snippetMode")
                .GetProperty("const")
                .GetString());
        var cloudSiteSpecificPattern = plausibleModeUrlRules[0]
            .GetProperty("then")
            .GetProperty("properties")
            .GetProperty("scriptUrl")
            .GetProperty("pattern")
            .GetString();
        Assert.Matches(cloudSiteSpecificPattern!, "https://plausible.io/js/pa-EXAMPLE_1.js");
        Assert.DoesNotMatch(cloudSiteSpecificPattern!, "https://plausible.io/js/script.js");
        Assert.Equal(
            "legacy",
            plausibleModeUrlRules[1]
                .GetProperty("if")
                .GetProperty("properties")
                .GetProperty("snippetMode")
                .GetProperty("const")
                .GetString());
        Assert.Equal(
            cloudSiteSpecificPattern,
            plausibleModeUrlRules[1]
                .GetProperty("then")
                .GetProperty("properties")
                .GetProperty("scriptUrl")
                .GetProperty("not")
                .GetProperty("pattern")
                .GetString());
        AssertScriptUrlSchemaPattern(
            providerVariants[3].GetProperty("properties").GetProperty("scriptUrl").GetProperty("pattern").GetString());

        var collectionPagination = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("collections")
            .GetProperty("additionalProperties")
            .GetProperty("properties")
            .GetProperty("pagination")
            .GetProperty("properties");
        Assert.True(collectionPagination.TryGetProperty("enabled", out _));
        Assert.True(collectionPagination.TryGetProperty("pageSize", out _));
        Assert.True(collectionPagination.TryGetProperty("urlPattern", out _));
        Assert.True(collectionPagination.TryGetProperty("firstPageUsesListRoute", out _));
        Assert.Equal(1, collectionPagination.GetProperty("pageSize").GetProperty("minimum").GetInt32());
        Assert.Equal("boolean", collectionPagination.GetProperty("firstPageUsesListRoute").GetProperty("type").GetString());

        var filteredListItem = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("collections")
            .GetProperty("additionalProperties")
            .GetProperty("properties")
            .GetProperty("filteredLists")
            .GetProperty("items");
        var filteredList = filteredListItem.GetProperty("properties");
        Assert.True(filteredList.TryGetProperty("field", out _));
        Assert.True(filteredList.TryGetProperty("operator", out _));
        Assert.True(filteredList.TryGetProperty("value", out _));
        Assert.True(filteredList.TryGetProperty("values", out _));
        Assert.True(filteredList.TryGetProperty("listRoute", out _));
        Assert.True(filteredList.TryGetProperty("listTemplate", out _));
        Assert.True(filteredList.TryGetProperty("pageSize", out _));
        Assert.True(filteredList.TryGetProperty("urlPattern", out _));
        Assert.True(filteredList.TryGetProperty("emptyBehavior", out _));
        Assert.Equal(new[] { "field", "listRoute" }, filteredListItem.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "equals", "contains", "in" }, filteredList.GetProperty("operator").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "render", "skip" }, filteredList.GetProperty("emptyBehavior").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(1, filteredList.GetProperty("pageSize").GetProperty("minimum").GetInt32());

        var scss = properties
            .GetProperty("theme")
            .GetProperty("properties")
            .GetProperty("scss")
            .GetProperty("properties");
        Assert.True(scss.TryGetProperty("outputDir", out _));
        Assert.False(scss.TryGetProperty("outDir", out _));
        Assert.False(scss.TryGetProperty("includePaths", out _));

        var propertyMap = properties
            .GetProperty("content")
            .GetProperty("properties")
            .GetProperty("sources")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("notion")
            .GetProperty("properties")
            .GetProperty("propertyMap")
            .GetProperty("properties");
        Assert.True(propertyMap.TryGetProperty("Title", out _));
        Assert.True(propertyMap.TryGetProperty("PublishAt", out _));
        Assert.True(propertyMap.TryGetProperty("SeoDescription", out _));
        Assert.True(propertyMap.TryGetProperty("Canonical", out _));
        Assert.True(propertyMap.TryGetProperty("OriginalUrl", out _));
        Assert.True(propertyMap.TryGetProperty("References", out _));
        Assert.True(propertyMap.TryGetProperty("EntitiesJson", out _));
        Assert.True(propertyMap.TryGetProperty("Cover", out _));
        Assert.True(propertyMap.TryGetProperty("CoverAlt", out _));
        Assert.True(propertyMap.TryGetProperty("CoverCaption", out _));
        Assert.False(propertyMap.TryGetProperty("lang", out _));

        var taxonomy = properties
            .GetProperty("taxonomy")
            .GetProperty("properties");
        Assert.True(taxonomy.TryGetProperty("outputMode", out _));
        Assert.True(taxonomy.TryGetProperty("itemFields", out _));
        Assert.True(taxonomy.TryGetProperty("pageSize", out _));
        Assert.True(taxonomy.TryGetProperty("indexEnabled", out _));
        Assert.True(taxonomy.TryGetProperty("pinField", out _));
        Assert.True(taxonomy.TryGetProperty("pinOrderField", out _));
        Assert.True(taxonomy.TryGetProperty("pinFieldBySource", out _));
        Assert.True(taxonomy.TryGetProperty("pinOrderFieldBySource", out _));

        var taxonomyKind = taxonomy
            .GetProperty("kinds")
            .GetProperty("items")
            .GetProperty("properties");
        Assert.True(taxonomyKind.TryGetProperty("routePrefix", out _));
    }

    [Fact]
    public void Generate_AllObjectSchemasDeclareAdditionalProperties()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);

        AssertObjectSchemasDeclareAdditionalProperties(doc.RootElement, "$");
    }

    [Fact]
    public void Generate_ExplicitlyAllowsDynamicMapLocations()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.GetProperty("properties");

        var site = properties.GetProperty("site").GetProperty("properties");
        var permalinks = site.GetProperty("permalinks").GetProperty("additionalProperties");
        Assert.Equal("string", permalinks.GetProperty("type").GetString());

        var plugins = site.GetProperty("plugins").GetProperty("additionalProperties");
        var pluginForms = plugins.GetProperty("oneOf");
        Assert.Equal("boolean", pluginForms[0].GetProperty("type").GetString());
        var pluginMapping = pluginForms[1];
        Assert.Equal("object", pluginMapping.GetProperty("type").GetString());
        Assert.False(pluginMapping.GetProperty("additionalProperties").GetBoolean());
        var pluginProperties = pluginMapping.GetProperty("properties");
        Assert.Equal("boolean", pluginProperties.GetProperty("enabled").GetProperty("type").GetString());
        var options = pluginProperties.GetProperty("options");
        Assert.Equal("object", options.GetProperty("type").GetString());
        Assert.True(options.GetProperty("additionalProperties").GetBoolean());

        var theme = properties.GetProperty("theme").GetProperty("properties");
        var themeParams = theme.GetProperty("params").GetProperty("additionalProperties");
        Assert.Equal(JsonValueKind.True, themeParams.ValueKind);

        var shortcodes = theme.GetProperty("shortcodes").GetProperty("additionalProperties");
        Assert.Equal("string", shortcodes.GetProperty("type").GetString());

        var componentProps = theme.GetProperty("components").GetProperty("additionalProperties")
            .GetProperty("properties").GetProperty("props")
            .GetProperty("additionalProperties");
        Assert.Equal("string", componentProps.GetProperty("type").GetString());

        var menus = site.GetProperty("menus").GetProperty("additionalProperties");
        Assert.Equal("array", menus.GetProperty("type").GetString());
        Assert.Equal("object", menus.GetProperty("items").GetProperty("type").GetString());

        var taxonomy = properties.GetProperty("taxonomy").GetProperty("properties");
        Assert.Equal("string", taxonomy.GetProperty("pinFieldBySource").GetProperty("additionalProperties").GetProperty("type").GetString());
        Assert.Equal("string", taxonomy.GetProperty("pinOrderFieldBySource").GetProperty("additionalProperties").GetProperty("type").GetString());

        var fieldScopes = properties
            .GetProperty("content").GetProperty("properties")
            .GetProperty("modelSchema").GetProperty("properties")
            .GetProperty("fieldScopes");
        var fieldScopesAddl = fieldScopes.GetProperty("additionalProperties");
        Assert.Equal("array", fieldScopesAddl.GetProperty("type").GetString());
        Assert.Equal("object", fieldScopesAddl.GetProperty("items").GetProperty("type").GetString());
    }

    private static bool MatchesRequiredProperties(JsonElement instance, JsonElement schema)
    {
        if (schema.TryGetProperty("const", out var constant) &&
            instance.GetRawText() != constant.GetRawText())
        {
            return false;
        }

        if (schema.TryGetProperty("required", out var required))
        {
            if (instance.ValueKind != JsonValueKind.Object ||
                required.EnumerateArray().Any(name =>
                    !instance.TryGetProperty(name.GetString()!, out _)))
            {
                return false;
            }
        }

        if (instance.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (instance.TryGetProperty(property.Name, out var value) &&
                    !MatchesRequiredProperties(value, property.Value))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void AssertObjectSchemasDeclareAdditionalProperties(JsonElement node, string path)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.GetString() == "object")
            {
                Assert.True(node.TryGetProperty("additionalProperties", out var additionalProperties), $"Object schema at {path} must declare additionalProperties");

                if (!AllowedOpenMapObjectSchemaPaths.Contains(path) &&
                    additionalProperties.ValueKind != JsonValueKind.False)
                {
                    Assert.Fail($"Object schema at {path} must be additionalProperties=false");
                }
            }

            foreach (var property in node.EnumerateObject())
            {
                AssertObjectSchemasDeclareAdditionalProperties(property.Value, $"{path}/{property.Name}");
            }

            return;
        }

        if (node.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in node.EnumerateArray())
            {
                AssertObjectSchemasDeclareAdditionalProperties(item, $"{path}[{index}]");
                index++;
            }
        }
    }

    private static void AssertScriptUrlSchemaPattern(string? pattern)
    {
        Assert.False(string.IsNullOrWhiteSpace(pattern));
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Matches(regex, "https://analytics.example.com/script.js");
        Assert.Matches(regex, "https://analytics.example.com:443/js/script.js?v=1");
        Assert.DoesNotMatch(regex, "http://analytics.example.com/script.js");
        Assert.DoesNotMatch(regex, "https://user:pass@analytics.example.com/script.js");
        Assert.DoesNotMatch(regex, "https://analytics.example.com:8443/script.js");
        Assert.DoesNotMatch(regex, "https://analytics.example.com/script.js#fragment");
        Assert.DoesNotMatch(regex, "https://analytics.example.com/script.css");
    }
}
