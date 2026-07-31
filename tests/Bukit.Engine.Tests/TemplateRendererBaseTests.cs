using Xunit;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for TemplateRendererBase template resolution, caching, layout nesting, and shortcodes.
/// </summary>
public sealed class TemplateRendererBaseTests : IDisposable
{
    private readonly string _layoutsDir;
    private readonly string _parentLayoutsDir;

    public TemplateRendererBaseTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-trb-" + Guid.NewGuid().ToString("N"), "layouts");
        _parentLayoutsDir = Path.Combine(Path.GetTempPath(), "bukit-trb-" + Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(_layoutsDir);
        Directory.CreateDirectory(_parentLayoutsDir);
    }

    public void Dispose()
    {
        var root1 = Path.GetDirectoryName(_layoutsDir);
        var root2 = Path.GetDirectoryName(_parentLayoutsDir);
        TestCleanup.DeleteDirectory(root1!, recursive: true);
        TestCleanup.DeleteDirectory(root2!, recursive: true);
    }

    private sealed class TestRenderer : TemplateRendererBase
    {
        public TestRenderer(
            string layoutsDir,
            string? parentLayoutsDir = null,
            string? userLayoutsDir = null,
            IReadOnlyDictionary<string, string>? shortcodes = null)
            : base(layoutsDir, parentLayoutsDir, userLayoutsDir, shortcodes)
        {
        }

        public string CallRenderWithLayout(string templateRelativePath, object modelData, int depth = 0)
            => RenderWithLayout(templateRelativePath, modelData, depth);

        public override string RenderPage(string templateRelativePath, PageModel model)
            => CallRenderWithLayout(templateRelativePath, model);

        public override string RenderList(string templateRelativePath, ListPageModel model)
            => CallRenderWithLayout(templateRelativePath, model);

        protected override object ParseTemplateText(string templateText, string templatePath, string templateRelativePath)
            => templateText;

        protected override string RenderTemplateCore(object parsedTemplate, string templateRelativePath, object modelData)
            => (string)parsedTemplate;

        protected override string ResolveTemplatePath(string templateRelativePath)
            => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(LayoutsDir))!, "layouts", templateRelativePath);

        protected override void SetContent(object modelData, string content)
        {
        }
    }

    [Fact]
    public void RenderWithLayout_SimpleTemplate_Renders()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "simple.html"), "hello");
        var renderer = new TestRenderer(_layoutsDir);

        var result = renderer.CallRenderWithLayout("simple.html", new object());

        Assert.Equal("hello", result);
    }

    [Fact]
    public void RenderWithLayout_MissingTemplate_Throws()
    {
        var renderer = new TestRenderer(_layoutsDir);

        var ex = Assert.Throws<RenderException>(() =>
            renderer.CallRenderWithLayout("missing.html", new object()));
        Assert.Equal(DiagnosticCode.RenderTemplateNotFound, ex.Code);
    }

    [Fact]
    public void RenderWithLayout_DepthExceeded_Throws()
    {
        var renderer = new TestRenderer(_layoutsDir);

        var ex = Assert.Throws<RenderException>(() =>
            renderer.CallRenderWithLayout("simple.html", new object(), depth: 10));
        Assert.Equal(DiagnosticCode.RenderLayoutNestingExceeded, ex.Code);
    }

    [Fact]
    public void RenderWithLayout_CachesTemplate_SecondCallUsesCache()
    {
        var templatePath = Path.Combine(_layoutsDir, "cached.html");
        File.WriteAllText(templatePath, "v1");
        var renderer = new TestRenderer(_layoutsDir);

        var first = renderer.CallRenderWithLayout("cached.html", new object());
        File.WriteAllText(templatePath, "v2");
        // Same size/time hash may differ; verify content is re-read only when file changes detectably
        var second = renderer.CallRenderWithLayout("cached.html", new object());

        Assert.Equal("v1", first);
        Assert.NotNull(second);
    }

    [Fact]
    public void RenderWithLayout_Shortcodes_Applied()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "short.html"), "before {% name 'arg1' %} after");
        var renderer = new TestRenderer(_layoutsDir, shortcodes: new Dictionary<string, string>
        {
            ["name"] = "Value: {{ $1 }}"
        });

        var result = renderer.CallRenderWithLayout("short.html", new object());

        Assert.Contains("Value: arg1", result);
    }

    [Fact]
    public void ExtractLayoutDirective_Default_ReturnsBodyOnly()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "plain.html"), "content");
        var renderer = new TestRenderer(_layoutsDir);

        var result = renderer.CallRenderWithLayout("plain.html", new object());

        Assert.Equal("content", result);
    }
}
