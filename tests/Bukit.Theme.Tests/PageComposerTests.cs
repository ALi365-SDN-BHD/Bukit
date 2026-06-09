using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class PageComposerTests
{
    [Fact]
    public void ParseSections_EmptyString_ReturnsEmptyList()
    {
        var result = PageComposer.ParseSections("");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseSections_ValidJson_ParsesCorrectly()
    {
        var json = """
            [
              { "type": "hero", "props": { "title": "Hello" } }
            ]
            """;

        var result = PageComposer.ParseSections(json);
        Assert.Single(result);
        Assert.Equal("hero", result[0].Type);
        Assert.NotNull(result[0].Props);
        Assert.Contains("title", result[0].Props!.Keys);
    }

    [Fact]
    public void ParseSections_Null_ReturnsEmptyList()
    {
        var result = PageComposer.ParseSections(null);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseSections_InvalidJson_ReturnsEmptyList()
    {
        var result = PageComposer.ParseSections("not json");
        Assert.Empty(result);
    }

    [Fact]
    public void Compose_MergesThemeDefaults()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new() { Type = "hero", Props = new Dictionary<string, object?> { ["title"] = "Custom" } }
        };

        var themeSections = new Dictionary<string, ThemeSectionDefinition>
        {
            ["hero"] = new() { Description = "Theme default" }
        };

        var result = PageComposer.Compose(pageSections, themeSections);
        Assert.Single(result);
        Assert.Equal("hero", result[0].Type);
        Assert.Equal("Custom", result[0].Props!["title"]);
    }

    [Fact]
    public void Compose_SectionNotFound_ReturnsAsIs()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new() { Type = "unknown", Props = new Dictionary<string, object?> { ["x"] = "1" } }
        };

        var result = PageComposer.Compose(pageSections, new Dictionary<string, ThemeSectionDefinition>());
        Assert.Single(result);
        Assert.Equal("unknown", result[0].Type);
    }

    [Fact]
    public void Compose_MergesFilterAndSort()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new()
            {
                Type = "posts",
                Filter = new Dictionary<string, object?> { ["category"] = "news" },
                Sort = "publishAt asc"
            }
        };
        var themeSections = new Dictionary<string, ThemeSectionDefinition>
        {
            ["posts"] = new()
            {
                Data = new ThemeDataBindingDefinition { Source = "posts", Limit = 5, Filters = new Dictionary<string, object?> { ["featured"] = true } }
            }
        };
        var result = PageComposer.Compose(pageSections, themeSections);
        Assert.Single(result);
        Assert.Equal("posts", result[0].Type);
        Assert.Equal(5, result[0].Limit);
    }

    [Fact]
    public void ParseSections_SingleObject_WrapsInList()
    {
        var json = """{ "type": "hero", "props": { "title": "Solo" } }""";
        var result = PageComposer.ParseSections(json);
        Assert.Single(result);
        Assert.Equal("hero", result[0].Type);
    }

    [Fact]
    public void Compose_MultipleSections_ReturnsAll()
    {
        var pageSections = new List<PageSectionDefinition>
        {
            new() { Type = "hero" }, new() { Type = "cta" }, new() { Type = "footer" }
        };
        var result = PageComposer.Compose(pageSections, new Dictionary<string, ThemeSectionDefinition>());
        Assert.Equal(3, result.Count);
    }
}

