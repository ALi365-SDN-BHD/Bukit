using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Bukit.Theme;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ReportSchemaContractTests
{
    [Theory]
    [InlineData("agent-manifest", "agent-manifest.v1.schema.json", "documents")]
    [InlineData("metrics", "metrics.v2.schema.json", "variants")]
    [InlineData("search-index-index", "search-index-index.v1.schema.json", "indexes")]
    public async Task RealMultilingualBuildOutputs_MatchLocalSchema(string id, string schemaFile, string arrayField)
    {
        using var site = new SiteBuilder("i18n-site");
        var config = site.Config;
        config = config with { Site = config.Site with { Search = config.Site.Search with { Mode = "index" } } };
        await new SiteEngine(new ConsoleLogger(LogLevel.Error)).BuildAsync(config, site.Root,
            new ConfigOverrides { Clean = true, Incremental = false, Jobs = 1, MetricsPath = "metrics.json" });

        var paths = id switch
        {
            "agent-manifest" => new[] { "dist/agent-manifest.json", "dist/en/agent-manifest.json", "dist/zh-CN/agent-manifest.json" },
            "metrics" => ["metrics.json"],
            _ => new[] { "dist/search.index.json" }
        };
        foreach (var path in paths)
            AssertContract(id, schemaFile, site.Sandbox.PathFor(path), arrayField);
    }

    [Fact]
    public void RealThemeCatalog_MatchesLocalSchemaForPopulatedAndEmptyThemes()
    {
        using var sandbox = new SiteSandbox();
        var manifest = new ThemeManifestV2
        {
            Name = "test-theme",
            Version = "2.0.0",
            Sections = new Dictionary<string, ThemeSectionDefinition>
            {
                ["hero"] = new() { Template = "sections/hero.html", Description = "Hero" }
            },
            Components = new Dictionary<string, ThemeComponentDefinition>
            {
                ["card"] = new() { Template = "components/card.html", Props = new Dictionary<string, string> { ["title"] = "string" } }
            }
        };
        var path = sandbox.PathFor("theme-catalog.json");
        ThemeCatalogWriter.WriteToFile(manifest, new ThemeComponentRegistry(sandbox.Root, manifest), path);
        AssertContract("theme-catalog", "theme-catalog.schema.json", path, "sections");

        var empty = new ThemeManifestV2 { Name = "empty-theme" };
        ThemeCatalogWriter.WriteToFile(empty, new ThemeComponentRegistry(sandbox.Root, empty), path);
        AssertContract("theme-catalog", "theme-catalog.schema.json", path, "sections");
    }

    private static void AssertContract(string id, string schemaFile, string outputPath, string arrayField)
    {
        Assert.True(File.Exists(outputPath), $"Missing generated output: {outputPath}");
        var schemaPath = Path.Combine(SiteSandbox.RepositoryRoot, "docs", "schemas", schemaFile);
        Assert.True(File.Exists(schemaPath), $"Missing local schema: {schemaFile}");
        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(SiteSandbox.RepositoryRoot, "docs/testing/core-report-inventory.json")));
        var record = Assert.Single(inventory.RootElement.GetProperty("records").EnumerateArray(), entry => entry.GetProperty("id").GetString() == id);
        Assert.Equal("docs/schemas/" + schemaFile, record.GetProperty("localSchema").GetString());
        var declared = record.GetProperty("declaredSchemaId");
        var expectedId = declared.ValueKind == JsonValueKind.Null ? record.GetProperty("localSchemaId").GetString() : declared.GetString();
        Assert.Equal(expectedId, schema.RootElement.GetProperty("$id").GetString());
        var generated = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        if (declared.ValueKind != JsonValueKind.Null)
            Assert.Equal(expectedId, generated["schema"]!.GetValue<string>());
        else
            Assert.False(generated.ContainsKey("schema"));
        LocalJsonSchema.AssertValid(JsonSerializer.SerializeToElement(generated), schema.RootElement);

        if (id != "theme-catalog") Assert.NotEmpty(generated[arrayField]!.AsArray());
        var invalid = generated.DeepClone().AsObject();
        invalid[arrayField] = false;
        var error = Assert.Throws<InvalidDataException>(() => LocalJsonSchema.AssertValid(JsonSerializer.SerializeToElement(invalid), schema.RootElement));
        Assert.Contains(arrayField, error.Message);

        if (generated[arrayField]!.AsArray().Count > 0)
        {
            var field = id switch { "agent-manifest" => "route", "metrics" => "routed", "search-index-index" => "language", _ => "name" };
            invalid = generated.DeepClone().AsObject();
            invalid[arrayField]![0]![field] = false;
            error = Assert.Throws<InvalidDataException>(() => LocalJsonSchema.AssertValid(JsonSerializer.SerializeToElement(invalid), schema.RootElement));
            Assert.Contains(field, error.Message);
        }

        var version = record.GetProperty("versionBinding");
        if (version.ValueKind != JsonValueKind.Null)
        {
            var property = version.GetProperty("property").GetString()!;
            invalid = generated.DeepClone().AsObject();
            invalid[property] = version.GetProperty("value").ValueKind == JsonValueKind.String
                ? JsonValue.Create("wrong-version")
                : JsonValue.Create(-1);
            error = Assert.Throws<InvalidDataException>(() => LocalJsonSchema.AssertValid(JsonSerializer.SerializeToElement(invalid), schema.RootElement));
            Assert.Contains(property, error.Message);
        }
    }
}
