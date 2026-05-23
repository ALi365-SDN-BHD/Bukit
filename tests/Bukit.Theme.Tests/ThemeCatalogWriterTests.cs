using System.Text.Json;
using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class ThemeCatalogWriterTests : IDisposable
{
    private readonly string _testDir;

    public ThemeCatalogWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-theme-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void GenerateJson_ProducesValidJsonWithSectionsAndComponents()
    {
        var manifest = new ThemeManifestV2
        {
            Name = "test-theme",
            Version = "2.0.0",
            Description = "A test theme",
            Extends = "parent-theme",
            Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["hero"] = new()
                {
                    Template = "sections/hero.html",
                    Description = "Full-width hero",
                    Variants = new Dictionary<string, ThemeVariantDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["centered"] = new() { Template = "sections/hero--centered.html", Label = "Centered" },
                        ["split"] = new() { Template = "sections/hero--split.html", Label = "Split" }
                    },
                    Data = new ThemeDataBindingDefinition
                    {
                        Source = "posts",
                        Limit = 3,
                        Sort = "publishAt desc"
                    }
                },
                ["cta"] = new()
                {
                    Template = "sections/cta.html",
                    Description = "Call to action"
                }
            },
            Components = new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["PostCard"] = new()
                {
                    Template = "components/card.html",
                    Props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["title"] = "string",
                        ["url"] = "string"
                    }
                }
            }
        };

        var registry = new ThemeComponentRegistry(_testDir, manifest);

        var json = ThemeCatalogWriter.GenerateJson(manifest, registry);
        Assert.NotNull(json);
        Assert.NotEmpty(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("test-theme", root.GetProperty("theme").GetString());
        Assert.Equal("2.0.0", root.GetProperty("version").GetString());
        Assert.Equal("A test theme", root.GetProperty("description").GetString());
        Assert.Equal("parent-theme", root.GetProperty("extends").GetString());

        var sections = root.GetProperty("sections");
        Assert.Equal(2, sections.GetArrayLength());

        var heroSection = sections[0];
        Assert.Equal("hero", heroSection.GetProperty("name").GetString());
        Assert.Equal("Full-width hero", heroSection.GetProperty("description").GetString());
        Assert.Equal(2, heroSection.GetProperty("variants").GetArrayLength());
        Assert.Equal(1, heroSection.GetProperty("dataSources").GetArrayLength());
        Assert.Equal("posts", heroSection.GetProperty("dataSources")[0].GetString());

        var components = root.GetProperty("components");
        Assert.Equal(1, components.GetArrayLength());
        Assert.Equal("PostCard", components[0].GetProperty("name").GetString());
    }

    [Fact]
    public void GenerateJson_SectionsContainRequiredPropsFromSchema()
    {
        var schemasDir = Path.Combine(_testDir, "schemas");
        Directory.CreateDirectory(schemasDir);
        var schemaPath = Path.Combine(schemasDir, "hero.schema.json");
        File.WriteAllText(schemaPath, """
            {
              "Name": "hero",
              "Props": {
                "title": { "Type": "string", "Required": true },
                "eyebrow": { "Type": "string", "Required": false },
                "ctaText": { "Type": "string", "Required": true }
              }
            }
            """);

        var manifest = new ThemeManifestV2
        {
            Name = "test-theme",
            Version = "2.0.0",
            Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["hero"] = new()
                {
                    Template = "sections/hero.html",
                    Description = "Hero section",
                    Schema = schemaPath
                }
            }
        };

        var registry = new ThemeComponentRegistry(_testDir, manifest);

        var json = ThemeCatalogWriter.GenerateJson(manifest, registry);

        using var doc = JsonDocument.Parse(json);
        var sections = doc.RootElement.GetProperty("sections");
        var heroSection = sections[0];

        var requiredProps = heroSection.GetProperty("requiredProps");
        Assert.Equal(2, requiredProps.GetArrayLength());
        var reqList = requiredProps.EnumerateArray().Select(e => e.GetString()).OrderBy(s => s).ToList();
        Assert.Contains("title", reqList);
        Assert.Contains("ctaText", reqList);

        var optionalProps = heroSection.GetProperty("optionalProps");
        Assert.Equal(1, optionalProps.GetArrayLength());
        Assert.Equal("eyebrow", optionalProps[0].GetString());
    }

    [Fact]
    public void WriteToFile_CreatesFile()
    {
        var manifest = new ThemeManifestV2
        {
            Name = "test-theme",
            Version = "2.0.0",
            Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["hero"] = new() { Template = "sections/hero.html", Description = "Hero" }
            }
        };

        var registry = new ThemeComponentRegistry(_testDir, manifest);
        var outputPath = Path.Combine(_testDir, "theme-catalog.json");

        ThemeCatalogWriter.WriteToFile(manifest, registry, outputPath);

        Assert.True(File.Exists(outputPath));
        var content = File.ReadAllText(outputPath);
        Assert.NotEmpty(content);
        Assert.Contains("test-theme", content);
    }

    [Fact]
    public void GenerateJson_EmptyManifestReturnsValidJsonWithEmptyArrays()
    {
        var manifest = new ThemeManifestV2
        {
            Name = "minimal-theme",
            Version = "1.0.0"
        };

        var registry = new ThemeComponentRegistry(_testDir, manifest);

        var json = ThemeCatalogWriter.GenerateJson(manifest, registry);
        Assert.NotNull(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("minimal-theme", root.GetProperty("theme").GetString());
        Assert.Equal("1.0.0", root.GetProperty("version").GetString());

        Assert.Equal(JsonValueKind.Null, root.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("extends").ValueKind);

        var sections = root.GetProperty("sections");
        Assert.Equal(0, sections.GetArrayLength());

        var components = root.GetProperty("components");
        Assert.Equal(0, components.GetArrayLength());
    }
}
