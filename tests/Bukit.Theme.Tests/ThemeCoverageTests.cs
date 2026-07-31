using System.Text.Json;
using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class ThemeCoverageTests
{
    // ── ThemeTokensProcessor.WriteToFile ──────────────────────────────

    [Fact]
    public void WriteToFile_CreatesDirectoryAndFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        try
        {
            var tokens = new ThemeTokens
            {
                Colors = new Dictionary<string, string> { ["bg"] = "#fff" },
                Font = new Dictionary<string, string> { ["body"] = "sans-serif" },
                Radius = new Dictionary<string, string> { ["sm"] = "4px" },
                Spacing = new Dictionary<string, string> { ["md"] = "16px" },
                Layout = new Dictionary<string, string> { ["max-width"] = "1200px" }
            };
            var outputPath = Path.Combine(dir, "sub", "tokens.css");
            ThemeTokensProcessor.WriteToFile(tokens, outputPath);

            Assert.True(File.Exists(outputPath));
            var css = File.ReadAllText(outputPath);
            Assert.Contains("--color-bg: #fff;", css);
            Assert.Contains("--font-body: sans-serif;", css);
            Assert.Contains("--radius-sm: 4px;", css);
            Assert.Contains("--spacing-md: 16px;", css);
            Assert.Contains("--layout-max-width: 1200px;", css);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteToFile_ExistingDirectory_DoesNotThrow()
    {
        var dir = Path.GetTempPath();
        var outputPath = Path.Combine(dir, $"bukit-test-{Guid.NewGuid():N}.css");
        try
        {
            ThemeTokensProcessor.WriteToFile(new ThemeTokens(), outputPath);
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    // ── ThemePageTemplateDefinition / ThemePageTemplateAccept ─────────

    [Fact]
    public void ThemePageTemplateAccept_Defaults_AreNull()
    {
        var accept = new ThemePageTemplateAccept();
        Assert.Null(accept.Type);
        Assert.Null(accept.Collection);
    }

    [Fact]
    public void ThemePageTemplateAccept_SetProperties()
    {
        var accept = new ThemePageTemplateAccept
        {
            Type = "post",
            Collection = "blog"
        };
        Assert.Equal("post", accept.Type);
        Assert.Equal("blog", accept.Collection);
    }

    [Fact]
    public void ThemePageTemplateDefinition_DefaultValues()
    {
        var def = new ThemePageTemplateDefinition();
        Assert.Equal("", def.Template);
        Assert.Null(def.Label);
        Assert.Null(def.Accepts);
        Assert.Null(def.RequiredFields);
    }

    [Fact]
    public void ThemePageTemplateDefinition_FullRoundTrip()
    {
        var def = new ThemePageTemplateDefinition
        {
            Template = "page.html",
            Label = "Full Page",
            Accepts = new ThemePageTemplateAccept { Type = "page" },
            RequiredFields = ["title", "body"]
        };
        Assert.Equal("page.html", def.Template);
        Assert.Equal("Full Page", def.Label);
        Assert.Equal("page", def.Accepts.Type);
        Assert.Equal(2, def.RequiredFields!.Count);
    }

    // ── ThemeVariantDefinition ────────────────────────────────────────

    [Fact]
    public void ThemeVariantDefinition_DefaultValues()
    {
        var v = new ThemeVariantDefinition();
        Assert.Equal("", v.Template);
        Assert.Null(v.Label);
        Assert.Null(v.Description);
    }

    [Fact]
    public void ThemeVariantDefinition_AllProperties()
    {
        var v = new ThemeVariantDefinition
        {
            Template = "compact.html",
            Label = "Compact",
            Description = "Minimal layout"
        };
        Assert.Equal("compact.html", v.Template);
        Assert.Equal("Compact", v.Label);
        Assert.Equal("Minimal layout", v.Description);
    }

    // ── SectionSchema ─────────────────────────────────────────────────

    [Fact]
    public void SectionSchema_Load_NonExistentFile_ReturnsNull()
    {
        Assert.Null(SectionSchema.Load("/nonexistent/path/schema.json"));
    }

    [Fact]
    public void SectionSchema_Load_ValidJson_ReturnsSchema()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"Name":"hero","Label":"Hero Section","Description":"Top banner","Props":{"title":{"Type":"string","Required":true,"MaxLength":100}}}""");
            var schema = SectionSchema.Load(path);
            Assert.NotNull(schema);
            Assert.Equal("hero", schema!.Name);
            Assert.Equal("Hero Section", schema.Label);
            Assert.Equal("Top banner", schema.Description);
            Assert.NotNull(schema.Props);
            Assert.True(schema.Props!.ContainsKey("title"));
            Assert.True(schema.Props["title"].Required);
            Assert.Equal(100, schema.Props["title"].MaxLength);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SectionSchema_Load_InvalidJson_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not valid json {{{");
            Assert.Null(SectionSchema.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SchemaPropDefinition_DefaultValues()
    {
        var prop = new SchemaPropDefinition();
        Assert.Equal("string", prop.Type);
        Assert.False(prop.Required);
        Assert.Null(prop.MaxLength);
    }

    [Fact]
    public void SectionSchema_DefaultValues()
    {
        var schema = new SectionSchema();
        Assert.Equal("", schema.Name);
        Assert.Null(schema.Label);
        Assert.Null(schema.Description);
        Assert.Null(schema.Props);
    }

    // ── PageComposer extended coverage ────────────────────────────────

    [Fact]
    public void ParseSections_WithVariant_SetsVariant()
    {
        var json = """[{"type":"hero","variant":"compact","props":{"title":"Hi"}}]""";
        var result = PageComposer.ParseSections(json);
        Assert.Single(result);
        Assert.Equal("compact", result[0].Variant);
    }

    [Fact]
    public void ParseSections_WithSource_SetsSource()
    {
        var json = """[{"type":"posts","source":"blog"}]""";
        var result = PageComposer.ParseSections(json);
        Assert.Equal("blog", result[0].Source);
    }

    [Fact]
    public void ParseSections_WithFilterAndLimitAndSort()
    {
        var json = """[{"type":"posts","filter":{"category":"news"},"limit":10,"sort":"date desc"}]""";
        var result = PageComposer.ParseSections(json);
        Assert.Equal(10, result[0].Limit);
        Assert.Equal("date desc", result[0].Sort);
        Assert.NotNull(result[0].Filter);
        Assert.Equal("news", result[0].Filter!["category"]);
    }

    [Fact]
    public void ParseSections_MissingType_ReturnsEmpty()
    {
        var json = """[{"props":{"title":"no type"}}]""";
        var result = PageComposer.ParseSections(json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseSections_NonObjectArray_SkipsInvalid()
    {
        var json = """["string", 42, null, {"type":"hero"}]""";
        var result = PageComposer.ParseSections(json);
        Assert.Single(result);
        Assert.Equal("hero", result[0].Type);
    }

    [Fact]
    public void ParseSections_NonArrayNonObject_ReturnsEmpty()
    {
        var json = """42""";
        var result = PageComposer.ParseSections(json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseSections_TypeIsNotString_ReturnsEmpty()
    {
        var json = """[{"type":123}]""";
        var result = PageComposer.ParseSections(json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseSections_PropsWithAllValueKinds()
    {
        var json = """[{"type":"t","props":{"s":"str","n":42,"f":3.14,"b":true,"f2":false,"nil":null,"arr":[1,2]}}]""";
        var result = PageComposer.ParseSections(json);
        Assert.Single(result);
        var props = result[0].Props!;
        Assert.Equal("str", props["s"]);
        Assert.Equal(42L, props["n"]);
        Assert.Equal(3.14, props["f"]);
        Assert.Equal(true, props["b"]);
        Assert.Equal(false, props["f2"]);
        Assert.Null(props["nil"]);
        Assert.IsType<string>(props["arr"]); // raw text for arrays
    }

    [Fact]
    public void ParseSections_SingleObject_NotObject_ReturnsEmpty()
    {
        var json = """42""";
        var result = PageComposer.ParseSections(json);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseSections_SingleObject_MissingType_ReturnsEmpty()
    {
        var json = """{"props":{"x":1}}""";
        var result = PageComposer.ParseSections(json);
        Assert.Empty(result);
    }

    [Fact]
    public void Compose_ThemeSectionNoData_PreservesPageSectionSourceAndSort()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new() { Type = "posts", Source = "blog", Sort = "date desc", Props = new Dictionary<string, object?> { ["title"] = "Hi" } }
        };
        var themeSections = new Dictionary<string, ThemeSectionDefinition>
        {
            ["posts"] = new() { Description = "theme" }
        };
        var result = PageComposer.Compose(pageSections, themeSections);
        Assert.Equal("blog", result[0].Source);
        Assert.Equal("date desc", result[0].Sort);
    }

    [Fact]
    public void Compose_ThemeSectionWithData_OverridesPageSectionDefaults()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new() { Type = "posts" }
        };
        var themeSections = new Dictionary<string, ThemeSectionDefinition>
        {
            ["posts"] = new() { Data = new ThemeDataBindingDefinition { Source = "articles", Limit = 20, Sort = "title asc" } }
        };
        var result = PageComposer.Compose(pageSections, themeSections);
        Assert.Equal("articles", result[0].Source);
        Assert.Equal(20, result[0].Limit);
        Assert.Equal("title asc", result[0].Sort);
    }

    [Fact]
    public void Compose_PageSectionFilterOverridesThemeDataFilters()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new()
            {
                Type = "posts",
                Filter = new Dictionary<string, object?> { ["tag"] = "tech" },
                Limit = 5,
                Sort = "date desc"
            }
        };
        var themeSections = new Dictionary<string, ThemeSectionDefinition>
        {
            ["posts"] = new()
            {
                Data = new ThemeDataBindingDefinition
                {
                    Source = "articles",
                    Filters = new Dictionary<string, object?> { ["featured"] = true }
                }
            }
        };
        var result = PageComposer.Compose(pageSections, themeSections);
        Assert.Equal("articles", result[0].Source);
        Assert.Equal(5, result[0].Limit);
        Assert.Equal("date desc", result[0].Sort);
        Assert.Equal("tech", result[0].Filter!["tag"]);
        Assert.False(result[0].Filter!.ContainsKey("featured"));
    }

    [Fact]
    public void Compose_NoThemeData_NoPageFilter_ReturnsThemeData()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new() { Type = "posts" }
        };
        var themeSections = new Dictionary<string, ThemeSectionDefinition>
        {
            ["posts"] = new()
            {
                Data = new ThemeDataBindingDefinition
                {
                    Source = "articles",
                    Filters = new Dictionary<string, object?> { ["active"] = true }
                }
            }
        };
        var result = PageComposer.Compose(pageSections, themeSections);
        Assert.Equal("articles", result[0].Source);
        Assert.Equal(true, result[0].Filter!["active"]);
    }

    [Fact]
    public void Compose_PageSectionWithNoProps_EmptyMergeReturnsNull()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new() { Type = "hero" }
        };
        var themeSections = new Dictionary<string, ThemeSectionDefinition>
        {
            ["hero"] = new() { Description = "theme" }
        };
        var result = PageComposer.Compose(pageSections, themeSections);
        Assert.Null(result[0].Props);
    }

    // ── ThemeYaml extended ────────────────────────────────────────────

    // ── ThemeDataBindingDefinition ─────────────────────────────────

    [Fact]
    public void ThemeDataBindingDefinition_DefaultValues()
    {
        var d = new ThemeDataBindingDefinition();
        Assert.Null(d.Source);
        Assert.Null(d.Mode);
        Assert.Null(d.Limit);
        Assert.Null(d.Sort);
        Assert.Null(d.Filters);
    }

    [Fact]
    public void ThemeDataBindingDefinition_AllProperties()
    {
        var d = new ThemeDataBindingDefinition
        {
            Source = "posts",
            Mode = "list",
            Limit = 10,
            Sort = "date desc",
            Filters = new Dictionary<string, object?> { ["tag"] = "tech" }
        };
        Assert.Equal("posts", d.Source);
        Assert.Equal("list", d.Mode);
        Assert.Equal(10, d.Limit);
        Assert.Equal("date desc", d.Sort);
        Assert.Equal("tech", d.Filters["tag"]);
    }
}
