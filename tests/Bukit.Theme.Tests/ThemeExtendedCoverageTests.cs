using Xunit;
using Bukit.Theme;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Theme.Tests;

public sealed class ThemeExtendedCoverageTests
{
    private static ContentDocument MakeDoc(string id, string title, string type,
        DateTimeOffset? publishAt = null,
        List<string>? collections = null,
        IReadOnlyDictionary<string, ContentField>? fields = null)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = type
        };
        if (collections is not null) meta["collections"] = collections;
        return ContentDocument.Create(id, title, id,
            publishAt ?? new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            null, ContentFieldReader.WithValues(fields, meta));
    }

    private static RouteInfo MakeRoute(string url)
        => new(url, url.TrimStart('/') + "index.html", "pages/page.html");

    private static IReadOnlyList<(ContentDocument, RouteInfo?)> MakePages(params (ContentDocument, string)[] items)
        => items.Select(i => ((ContentDocument, RouteInfo?))(i.Item1, MakeRoute(i.Item2))).ToList();

    // ── SectionDataResolver: collection: prefix ──────────────────────

    [Fact]
    public void Resolve_CollectionPrefix_MatchesCollection()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post", collections: ["blog", "featured"]), "/a/"),
            (MakeDoc("b", "B", "post"), "/b/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "collection:blog" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Single(result);
        Assert.Equal("A", result[0].Document.Title);
    }

    [Fact]
    public void Resolve_CollectionPrefix_CaseInsensitive()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post", collections: ["Blog"]), "/a/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "collection:blog" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Single(result);
    }

    [Fact]
    public void Resolve_AllKeyword_MatchesEverything()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post"), "/a/"),
            (MakeDoc("b", "B", "page"), "/b/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "all" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Resolve_MultipleSources_SplitByComma()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post"), "/a/"),
            (MakeDoc("b", "B", "page"), "/b/"),
            (MakeDoc("c", "C", "doc"), "/c/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post, page" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Resolve_SortByDateDescending()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/a/"),
            (MakeDoc("b", "B", "post", new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)), "/b/"),
            (MakeDoc("c", "C", "post", new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero)), "/c/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Sort = "date desc" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Equal("B", result[0].Document.Title);
        Assert.Equal("C", result[1].Document.Title);
        Assert.Equal("A", result[2].Document.Title);
    }

    [Fact]
    public void Resolve_SortByPublishAtAscending()
    {
        var items = MakePages(
            (MakeDoc("b", "B", "post", new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)), "/b/"),
            (MakeDoc("a", "A", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/a/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Sort = "publishAt" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Equal("A", result[0].Document.Title);
        Assert.Equal("B", result[1].Document.Title);
    }

    [Fact]
    public void Resolve_SortByTitleDescending()
    {
        var items = MakePages(
            (MakeDoc("a", "Alpha", "post"), "/a/"),
            (MakeDoc("b", "Bravo", "post"), "/b/"),
            (MakeDoc("c", "Charlie", "post"), "/c/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Sort = "title desc" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Equal("Charlie", result[0].Document.Title);
        Assert.Equal("Bravo", result[1].Document.Title);
        Assert.Equal("Alpha", result[2].Document.Title);
    }

    [Fact]
    public void Resolve_SortByUnknownField_FallsBackToDate()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post", new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)), "/a/"),
            (MakeDoc("b", "B", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/b/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Sort = "unknown desc" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Equal("A", result[0].Document.Title);
    }

    [Fact]
    public void Resolve_FilterByStringValue()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post", fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase) { ["category"] = new("text", "tech") }), "/a/"),
            (MakeDoc("b", "B", "post", fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase) { ["category"] = new("text", "food") }), "/b/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Filter = new Dictionary<string, object?> { ["category"] = "tech" } };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Single(result);
        Assert.Equal("A", result[0].Document.Title);
    }

    [Fact]
    public void Resolve_FilterByBoolString()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post", fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase) { ["draft"] = new("text", "True") }), "/a/"),
            (MakeDoc("b", "B", "post", fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase) { ["draft"] = new("text", "False") }), "/b/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Filter = new Dictionary<string, object?> { ["draft"] = true } };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Single(result);
        Assert.Equal("A", result[0].Document.Title);
    }

    [Fact]
    public void Resolve_FilterNullValue_Skipped()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post"), "/a/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Filter = new Dictionary<string, object?> { ["x"] = null } };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Single(result);
    }

    [Fact]
    public void Resolve_FilterMissingField_ReturnsFalse()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post"), "/a/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Filter = new Dictionary<string, object?> { ["missing"] = "value" } };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Empty(result);
    }

    [Fact]
    public void Resolve_CollectionMatchByPlainSource()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post", collections: ["blog"]), "/a/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "blog" };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Single(result);
    }

    [Fact]
    public void Resolve_LimitZeroOrNegative_NoLimitApplied()
    {
        var items = MakePages(
            (MakeDoc("a", "A", "post"), "/a/"),
            (MakeDoc("b", "B", "post"), "/b/")
        );
        var section = new PageSectionDefinition { Type = "list", Source = "post", Limit = 0 };
        var result = SectionDataResolver.Resolve(section, items);
        Assert.Equal(2, result.Count);
    }

    // ── ThemeComponentRegistry ────────────────────────────────────────

    [Fact]
    public void ThemeComponentRegistry_ResolvesSectionTemplate()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        var layoutsDir = Path.Combine(dir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        File.WriteAllText(Path.Combine(layoutsDir, "hero.html"), "<div>hero</div>");

        try
        {
            var manifest = new ThemeManifestV2
            {
                Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hero"] = new() { Template = "hero.html" }
                }
            };
            var registry = new ThemeComponentRegistry(dir, manifest);
            var path = registry.ResolveSectionTemplate("hero");
            Assert.NotNull(path);
            Assert.EndsWith("hero.html", path!);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_ResolvesSectionVariantTemplate()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        var layoutsDir = Path.Combine(dir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        File.WriteAllText(Path.Combine(layoutsDir, "hero-compact.html"), "<div>compact</div>");

        try
        {
            var manifest = new ThemeManifestV2
            {
                Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hero"] = new()
                    {
                        Template = "hero.html",
                        Variants = new Dictionary<string, ThemeVariantDefinition>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["compact"] = new() { Template = "hero-compact.html" }
                        }
                    }
                }
            };
            var registry = new ThemeComponentRegistry(dir, manifest);
            var path = registry.ResolveSectionTemplate("hero", "compact");
            Assert.NotNull(path);
            Assert.EndsWith("hero-compact.html", path!);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_UnknownSection_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = new ThemeManifestV2();
            var registry = new ThemeComponentRegistry(dir, manifest);
            Assert.Null(registry.ResolveSectionTemplate("nonexistent"));
            Assert.Null(registry.ResolveSection("nonexistent"));
            Assert.Null(registry.ResolveComponentTemplate("nonexistent"));
            Assert.Null(registry.ResolveComponent("nonexistent"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_ParentFallback()
    {
        var parentDir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}-parent");
        var childDir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}-child");
        var parentLayouts = Path.Combine(parentDir, "layouts");
        Directory.CreateDirectory(parentLayouts);
        File.WriteAllText(Path.Combine(parentLayouts, "hero.html"), "<div>parent</div>");
        Directory.CreateDirectory(childDir);

        try
        {
            var parentManifest = new ThemeManifestV2
            {
                Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hero"] = new() { Template = "hero.html" }
                },
                Components = new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["nav"] = new() { Template = "nav.html" }
                }
            };
            var parent = new ThemeComponentRegistry(parentDir, parentManifest);
            var child = new ThemeComponentRegistry(childDir, new ThemeManifestV2(), parent);

            Assert.NotNull(child.ResolveSectionTemplate("hero"));
            Assert.NotNull(child.ResolveSection("hero"));
            Assert.NotNull(child.ResolveComponentTemplate("nav"));
            Assert.NotNull(child.ResolveComponent("nav"));
        }
        finally
        {
            Directory.Delete(parentDir, recursive: true);
            Directory.Delete(childDir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_LayoutAndPageTemplates()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        var layoutsDir = Path.Combine(dir, "layouts");
        var pagesDir = Path.Combine(dir, "layouts", "pages");
        Directory.CreateDirectory(layoutsDir);
        Directory.CreateDirectory(pagesDir);
        File.WriteAllText(Path.Combine(layoutsDir, "base.html"), "<html>");
        File.WriteAllText(Path.Combine(pagesDir, "default.html"), "<body>");

        try
        {
            var registry = new ThemeComponentRegistry(dir, new ThemeManifestV2());
            Assert.NotNull(registry.ResolveLayoutTemplate("base"));
            Assert.NotNull(registry.ResolvePageTemplate("default"));
            Assert.Null(registry.ResolveLayoutTemplate("nonexistent"));
            Assert.Null(registry.ResolvePageTemplate("nonexistent"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_GetAllNames()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = new ThemeManifestV2
            {
                Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["hero"] = new(), ["cta"] = new()
                },
                Components = new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["nav"] = new()
                }
            };
            var registry = new ThemeComponentRegistry(dir, manifest);
            var sectionNames = registry.GetAllSectionNames().OrderBy(n => n).ToArray();
            var componentNames = registry.GetAllComponentNames().ToArray();
            Assert.Equal(["cta", "hero"], sectionNames);
            Assert.Single(componentNames);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_PathTraversal_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        var layoutsDir = Path.Combine(dir, "layouts");
        Directory.CreateDirectory(layoutsDir);

        try
        {
            var manifest = new ThemeManifestV2
            {
                Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["evil"] = new() { Template = "../../etc/passwd" }
                }
            };
            var registry = new ThemeComponentRegistry(dir, manifest);
            Assert.Null(registry.ResolveSectionTemplate("evil"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_AbsolutePath_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = new ThemeManifestV2
            {
                Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["abs"] = new() { Template = "/etc/passwd" }
                }
            };
            var registry = new ThemeComponentRegistry(dir, manifest);
            Assert.Null(registry.ResolveSectionTemplate("abs"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_EmptyTemplate_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = new ThemeManifestV2
            {
                Sections = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["empty"] = new() { Template = "" }
                }
            };
            var registry = new ThemeComponentRegistry(dir, manifest);
            Assert.Null(registry.ResolveSectionTemplate("empty"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ThemeComponentRegistry_ParentLayoutFallback()
    {
        var parentDir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}-parent");
        var childDir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}-child");
        var parentLayouts = Path.Combine(parentDir, "layouts");
        Directory.CreateDirectory(parentLayouts);
        File.WriteAllText(Path.Combine(parentLayouts, "base.html"), "<html>");
        Directory.CreateDirectory(childDir);

        try
        {
            var parent = new ThemeComponentRegistry(parentDir, new ThemeManifestV2());
            var child = new ThemeComponentRegistry(childDir, new ThemeManifestV2(), parent);
            Assert.NotNull(child.ResolveLayoutTemplate("base"));
        }
        finally
        {
            Directory.Delete(parentDir, recursive: true);
            Directory.Delete(childDir, recursive: true);
        }
    }
}
