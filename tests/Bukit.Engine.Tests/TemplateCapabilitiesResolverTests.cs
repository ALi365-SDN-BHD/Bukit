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
    public void GetCapabilities_ReloadsManifestWhenSameLengthContentChangesWithoutTimestampChange()
    {
        var manifestPath = Path.Combine(_layoutsDir, "bukit.templates.yaml");
        var fixedTimestamp = DateTime.UtcNow.AddMinutes(-5);
        WriteNeedsPageContentManifest(false);
        File.SetLastWriteTimeUtc(manifestPath, fixedTimestamp);
        var originalLength = new FileInfo(manifestPath).Length;

        var initial = TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir);

        WriteNeedsPageContentManifest(true);
        File.SetLastWriteTimeUtc(manifestPath, fixedTimestamp);
        Assert.Equal(originalLength, new FileInfo(manifestPath).Length);
        var updated = TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir);

        Assert.False(initial!.NeedsPageContent);
        Assert.True(updated!.NeedsPageContent);
    }

    [Fact]
    public void ValidateManifest_InvalidFileCanRecoverAfterCorrection()
    {
        var manifestPath = Path.Combine(_layoutsDir, "bukit.templates.yaml");
        File.WriteAllText(manifestPath, "templates: [");
        Assert.Throws<ConfigException>(() => TemplateCapabilitiesResolver.ValidateManifest(_layoutsDir));

        WriteNeedsPageContentManifest(true);

        TemplateCapabilitiesResolver.ValidateManifest(_layoutsDir);
        Assert.True(TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir)!.NeedsPageContent);
    }

    [Fact]
    public void GetCapabilities_ObservesManifestAppearanceAndDeletion()
    {
        Assert.Null(TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir));

        WriteNeedsPageContentManifest(true);
        Assert.True(TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir)!.NeedsPageContent);

        File.Delete(Path.Combine(_layoutsDir, "bukit.templates.yaml"));
        Assert.Null(TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir));
    }

    [Fact]
    public async Task GetCapabilities_ConcurrentReadersObserveCompleteUpdatedManifest()
    {
        WriteNeedsPageContentManifest(false);
        Assert.False(TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir)!.NeedsPageContent);
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), """
                                                                        templates:
                                                                          pages/list.html:
                                                                            capabilities:
                                                                              needs_page_content: true
                                                                              supports_pagination: true
                                                                              supports_taxonomy: true
                                                                              supports_search_snippets: true
                                                                        """);

        var readers = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => TemplateCapabilitiesResolver.GetCapabilities("pages/list.html", _layoutsDir)))
            .ToArray();
        var results = await Task.WhenAll(readers);

        Assert.All(results, capabilities =>
        {
            Assert.NotNull(capabilities);
            Assert.True(capabilities!.NeedsPageContent);
            Assert.True(capabilities.SupportsPagination);
            Assert.True(capabilities.SupportsTaxonomy);
            Assert.True(capabilities.SupportsSearchSnippets);
        });

        Assert.True(TemplateCapabilitiesResolver.SupportsPagination("pages/list.html", _layoutsDir));
        Assert.True(TemplateCapabilitiesResolver.SupportsTaxonomy("pages/list.html", _layoutsDir));
        Assert.True(TemplateCapabilitiesResolver.SupportsSearchSnippets("pages/list.html", _layoutsDir));
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

    [Fact]
    public void ResolveListPageContent_ReanalyzesTemplateCreatedAfterMissingFallback()
    {
        var initial = TemplateCapabilitiesResolver.ResolveListPageContent("pages/later.html", _layoutsDir, "auto");

        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "later.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        var updated = TemplateCapabilitiesResolver.ResolveListPageContent("pages/later.html", _layoutsDir, "auto");

        Assert.True(initial.IncludeContent);
        Assert.True(initial.UsedHeuristic);
        Assert.False(updated.IncludeContent);
        Assert.False(updated.UsedHeuristic);
        Assert.Equal("analysis", updated.Source);
    }

    private void WriteNeedsPageContentManifest(bool needsPageContent)
    {
        var value = needsPageContent ? "true " : "false";
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), $"""
                                                                        templates:
                                                                          pages/list.html:
                                                                            capabilities:
                                                                              needs_page_content: {value}
                                                                        """);
    }
}
