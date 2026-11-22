using Xunit;
using Bukit.Config;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering.Scriban;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Rendering.Tests;

public sealed class TemplateLoopLimitTests : IDisposable
{
    // 40 outer x 30 inner = 1200 iterations in one outermost loop, above Scriban's default of 1000.
    private const string NestedLoops = "{{ n = 0 }}{{ for i in 1..40 }}{{ for j in 1..30 }}{{ n = n + 1 }}{{ end }}{{ end }}{{ n }}";

    private readonly string _themeDir;
    private readonly string _layoutsDir;

    public TemplateLoopLimitTests()
    {
        _themeDir = Path.Combine(Path.GetTempPath(), "bukit-loop-limit-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(_themeDir, "layouts");
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose() => TestCleanup.DeleteDirectory(_themeDir, recursive: true);

    [Fact]
    public void RenderPage_NestedLoopsOverDefaultLimit_FailsWithConfigHint()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"), NestedLoops);
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        var ex = Assert.Throws<RenderException>(() => renderer.RenderPage("page.html", CreateModel()));

        Assert.Equal(DiagnosticCode.RenderFailed, ex.Code);
        Assert.Contains("iteration limit", ex.Message, StringComparison.Ordinal);
        Assert.Contains("build.templateLoopLimit", ex.Message, StringComparison.Ordinal);
        Assert.Contains("(current: 1000)", ex.Message, StringComparison.Ordinal);
        Assert.Contains("array.filter", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderPage_RaisedLimit_RendersNestedLoops()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"), NestedLoops);
        var renderer = new ScribanTemplateRenderer(_layoutsDir) { TemplateLoopLimit = 2000 };

        Assert.Equal("1200", renderer.RenderPage("page.html", CreateModel()));
    }

    [Fact]
    public void RenderPage_LoweredLimit_ReportsCurrentLimit()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"), "{{ for i in 1..20 }}{{ i }}{{ end }}");
        var renderer = new ScribanTemplateRenderer(_layoutsDir) { TemplateLoopLimit = 10 };

        var ex = Assert.Throws<RenderException>(() => renderer.RenderPage("page.html", CreateModel()));

        Assert.Contains("(current: 10)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderPage_ArrayFilterInsideLoop_DoesNotCountTowardLimit()
    {
        // The documented alternative: array.filter with an anonymous function does not add loop iterations.
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"),
            "{{ items = 1..30 }}{{ n = 0 }}{{ for i in 1..40 }}{{ n = n + (items | array.filter @(do; ret $0 > 0; end) | array.size) }}{{ end }}{{ n }}");
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        Assert.Equal("1200", renderer.RenderPage("page.html", CreateModel()));
    }

    [Fact]
    public void ThemeComponent_UsesConfiguredLimit()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "grid.html"), NestedLoops);
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"), "{{ comp.render 'grid' {} }}");
        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Components = new() { ["grid"] = new() { Template = "grid.html" } }
        };
        var registry = new ThemeComponentRegistry(_themeDir, manifest, null);

        var limited = new ScribanTemplateRenderer(_layoutsDir, null, null, null, null, registry, null, null, "off");
        var diagnostic = limited.RenderPage("page.html", CreateModel());
        Assert.Contains("build.templateLoopLimit", diagnostic, StringComparison.Ordinal);

        var raised = new ScribanTemplateRenderer(_layoutsDir, null, null, null, null, registry, null, null, "off") { TemplateLoopLimit = 2000 };
        Assert.Equal("1200", raised.RenderPage("page.html", CreateModel()));
    }

    [Fact]
    public void ConfigComponent_InheritsPageLimit()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "grid.html"), NestedLoops);
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"), "{{ comp.render 'grid' '' '' '' }}");
        var components = new Dictionary<string, ComponentDefinition> { ["grid"] = new() { Template = "grid.html" } };

        var raised = new ScribanTemplateRenderer(_layoutsDir, null, null, components, null) { TemplateLoopLimit = 2000 };

        Assert.Equal("1200", raised.RenderPage("page.html", CreateModel()));
    }

    [Fact]
    public void Describe_OtherErrors_KeepsMessage()
    {
        var ex = new InvalidOperationException("boom");

        Assert.Equal("boom", TemplateLoopLimitHint.Describe(ex, 1000));
        Assert.False(TemplateLoopLimitHint.IsLoopLimitError(ex));
    }

    private static PageModel CreateModel() => new()
    {
        Site = new SiteModel { Name = "t", Title = "T", Url = "https://example.com", BaseUrl = "/", Language = "en" },
        Page = new PageInfo { Title = "T", Content = string.Empty, Url = "/" }
    };
}
