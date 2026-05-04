using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TemplateCapabilitiesResolverTests : IDisposable
{
    private readonly string _layoutsDir;

    public TemplateCapabilitiesResolverTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-template-capabilities-" + Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "index.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
    }

    public void Dispose()
    {
        if (Directory.Exists(Path.GetDirectoryName(_layoutsDir)!))
        {
            Directory.Delete(Path.GetDirectoryName(_layoutsDir)!, recursive: true);
        }
    }

    [Fact]
    public void ValidateManifest_Throws_WhenTemplatePathTraversesOutsideLayouts()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), """
                                                                        templates:
                                                                          ../outside.html:
                                                                            capabilities:
                                                                              needs_page_content: true
                                                                        """);

        var ex = Assert.Throws<ConfigException>(() => TemplateCapabilitiesResolver.ValidateManifest(_layoutsDir));
        Assert.Contains("must stay within layouts", ex.Message);
    }

    [Fact]
    public void ValidateManifest_Throws_WhenTemplateFileDoesNotExist()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), """
                                                                        templates:
                                                                          pages/missing.html:
                                                                            capabilities:
                                                                              needs_page_content: true
                                                                        """);

        var ex = Assert.Throws<ConfigException>(() => TemplateCapabilitiesResolver.ValidateManifest(_layoutsDir));
        Assert.Contains("Template declared in bukit.templates.yaml not found", ex.Message);
    }

    [Fact]
    public void GetCapabilities_ReturnsDeclaredGenericCapabilities()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), """
                                                                        templates:
                                                                          pages/list.html:
                                                                            capabilities:
                                                                              needs_page_content: true
                                                                              supports_pagination: true
                                                                              supports_taxonomy: true
                                                                              supports_search_snippets: false
                                                                        """);

        var capabilities = TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir);

        Assert.NotNull(capabilities);
        Assert.True(capabilities!.NeedsPageContent);
        Assert.True(capabilities.SupportsPagination);
        Assert.True(capabilities.SupportsTaxonomy);
        Assert.False(capabilities.SupportsSearchSnippets);
    }

    [Fact]
    public void ResolveListPageContent_UsesRecursiveAnalysis_ForIncludedPartial()
    {
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "partials"));
        File.WriteAllText(Path.Combine(_layoutsDir, "partials", "card.html"), "{{ p.content }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ include \"partials/card.html\" }}{{ end }}");

        var resolution = TemplateCapabilitiesResolver.ResolveListPageContent("pages/list.html", _layoutsDir, "auto");

        Assert.True(resolution.IncludeContent);
        Assert.False(resolution.UsedHeuristic);
        Assert.Equal("analysis", resolution.Source);
    }

    [Fact]
    public void ResolveListPageContent_UsesRecursiveAnalysis_ForLayoutChain()
    {
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "partials"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "layouts"));
        File.WriteAllText(Path.Combine(_layoutsDir, "partials", "card.html"), "{{ p.content }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "layouts", "base.html"), "{{ include \"partials/card.html\" }}{{ content }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "list.html"), "{% layout \"layouts/base.html\" %}\n{{ for p in pages }}{{ p.title }}{{ end }}");

        var resolution = TemplateCapabilitiesResolver.ResolveListPageContent("pages/list.html", _layoutsDir, "auto");

        Assert.True(resolution.IncludeContent);
        Assert.False(resolution.UsedHeuristic);
        Assert.Equal("analysis", resolution.Source);
    }

    [Fact]
    public void ResolveListPageContent_DoesNotRequireContent_ForHarmlessInclude()
    {
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "partials"));
        File.WriteAllText(Path.Combine(_layoutsDir, "partials", "card.html"), "{{ p.title }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "list.html"), "{{ for p in pages }}{{ include \"partials/card.html\" }}{{ end }}");

        var resolution = TemplateCapabilitiesResolver.ResolveListPageContent("pages/list.html", _layoutsDir, "auto");

        Assert.False(resolution.IncludeContent);
        Assert.False(resolution.UsedHeuristic);
        Assert.Equal("analysis", resolution.Source);
    }
}
