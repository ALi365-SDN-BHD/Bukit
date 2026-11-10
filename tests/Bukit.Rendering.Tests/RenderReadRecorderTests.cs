using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering.Scriban;
using Xunit;

namespace Bukit.Rendering.Tests;

// add-per-page-render-dependencies: which shared collections a render reads.
public sealed class RenderReadRecorderTests : IDisposable
{
    private readonly string _layoutsDir;

    public RenderReadRecorderTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-render-reads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose() => TestCleanup.DeleteDirectory(_layoutsDir, recursive: true);

    [Theory]
    [InlineData("{{ page.title }} {{ site.title }} {{ site.params.x }}", "")]
    [InlineData("{{ site.data.menu.items }}", "site.data.menu")]
    [InlineData("{{ site.data.Menu }}", "site.data.menu")]
    [InlineData("{{ site.data[\"pages_by_id\"][page.id].title }}", "site.data.pages_by_id")]
    [InlineData("{{ site.modules.team }} {{ site.data_index.tags }}", "site.data_index.tags,site.modules.team")]
    [InlineData("{{ site.related_pages }} {{ site.data_files }}", "site.data_files,site.related_pages")]
    [InlineData("{{ for p in pages }}{{ p.title }}{{ end }}", "pages")]
    [InlineData("{{ site.data }}", "site.data.*")]
    [InlineData("{{ site.data[page.key] }}", "site.data.*")]
    [InlineData("{{ for k in site.modules }}{{ k }}{{ end }}", "site.modules.*")]
    [InlineData("{{ site.data.menu }} {{ site.data | object.keys }}", "site.data.*")]
    [InlineData("{{ x = site.data.menu; x.items }}", "site.data.menu")]
    [InlineData("{{ func f(a); ret a; end; f page.title }}", "")]
    [InlineData("{{ capture c }}hi{{ end }}{{ c }}", "")]
    [InlineData("{{ for r in site.related_pages limit: 3 offset: 1 reversed }}{{ r.title }}{{ end }}", "site.related_pages")]
    [InlineData("{{ x = { title: page.title, menu: site.data.menu } }}{{ x.title }}", "site.data.menu")]
    [InlineData("{{ for p in pages limit: page.fields.n.value }}{{ p.title }}{{ end }}", "pages")]
    [InlineData("{{ page.title | object.default \"x\" }}", "")]
    public void Analyze_MapsReferencesToCollectionKeys(string template, string expected)
    {
        Assert.Equal(expected, Reads(template));
    }

    [Theory]
    [InlineData("{{ site }}")]
    [InlineData("{{ include \"x\" site }}")]
    [InlineData("{{ this.site }}")]
    [InlineData("{{ unknown_global }}")]
    [InlineData("{{ with site.data }}{{ menu }}{{ end }}")]
    [InlineData("{{ if }}")]
    [InlineData("{{ object.eval \"site.data.menu\" }}")]
    [InlineData("{{ \"{{ site.data.menu }}\" | object.eval_template }}")]
    [InlineData("{{ object[page.fn] }}")]
    [InlineData("{{ o = object; o.eval \"site.data.m\" }}")]
    [InlineData("{{ for p in pages limit: unknown_global }}{{ end }}")]
    public void Analyze_UnresolvableUseReadsEverything(string template)
    {
        Assert.Equal(RenderReadRecorder.All, Reads(template));
    }

    [Fact]
    public void Recorder_WithoutTrackedRenderer_ReadsEverything()
    {
        using var scope = RenderReadRecorder.Begin();
        scope.Recorder.AddTemplate(ScribanTemplateReadAnalyzer.Analyze("{{ page.title }}"));
        Assert.Equal([RenderReadRecorder.All], scope.Recorder.Complete());
    }

    [Fact]
    public void Recorder_DeclaredNameInAnotherTemplate_IsNotUnknown()
    {
        using var scope = RenderReadRecorder.Begin();
        scope.Recorder.MarkTracked();
        scope.Recorder.AddTemplate(ScribanTemplateReadAnalyzer.Analyze("{{ menu = site.data.menu }}"));
        scope.Recorder.AddTemplate(ScribanTemplateReadAnalyzer.Analyze("{{ menu.items }}"));
        Assert.Equal(["site.data.menu"], scope.Recorder.Complete());
    }

    [Fact]
    public void Recorder_ScopeWildcardAbsorbsItsKeys()
    {
        using var scope = RenderReadRecorder.Begin();
        scope.Recorder.MarkTracked();
        scope.Recorder.RecordKey("site.data.menu");
        scope.Recorder.RecordKey("site.data.*");
        scope.Recorder.RecordKey("site.modules.team");
        Assert.Equal(["site.data.*", "site.modules.team"], scope.Recorder.Complete());
    }

    [Fact]
    public void RenderPage_RecordsTemplateLayoutAndIncludes()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "base.html"), "<html>{{ include \"nav.html\" }}{{ content }}</html>");
        File.WriteAllText(Path.Combine(_layoutsDir, "nav.html"), "{{ for m in site.data.menu }}{{ m }}{{ end }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"), "{{ layout \"base.html\" }}\n{{ site.modules.team }}{{ page.title }}");
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        using var scope = RenderReadRecorder.Begin();
        renderer.RenderPage("page.html", PageModel());

        Assert.Equal(["site.data.menu", "site.modules.team"], scope.Recorder.Complete());
    }

    [Fact]
    public void RenderPage_DynamicIncludeRecordsTheFileUsed()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "a.html"), "{{ site.data.a }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "b.html"), "{{ site.data.b }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"), "{{ include page.fields.part.value }}");
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        using var scope = RenderReadRecorder.Begin();
        renderer.RenderPage("page.html", PageModel(("part", "b.html")));

        Assert.Equal(["site.data.b"], scope.Recorder.Complete());
    }

    [Fact]
    public void RenderPage_PagesIsTheAllPagesList_RenderListPagesAreItems()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "t.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        using (var scope = RenderReadRecorder.Begin())
        {
            renderer.RenderPage("t.html", PageModel());
            Assert.Equal([RenderReadRecorder.Pages], scope.Recorder.Complete());
        }

        using (var scope = RenderReadRecorder.Begin())
        {
            renderer.RenderList("t.html", new ListPageModel { Site = Site(), Pages = [] });
            Assert.Empty(scope.Recorder.Complete());
        }
    }

    [Fact]
    public void Render_WithoutRecorder_StillWorks()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "t.html"), "{{ site.title }}");
        Assert.Equal("Test", new ScribanTemplateRenderer(_layoutsDir).RenderPage("t.html", PageModel()));
        Assert.Null(RenderReadRecorder.Current);
    }

    private static string Reads(string template)
    {
        var reads = ScribanTemplateReadAnalyzer.Analyze(template);
        using var scope = RenderReadRecorder.Begin();
        scope.Recorder.MarkTracked();
        scope.Recorder.AddTemplate(reads);
        return string.Join(",", scope.Recorder.Complete());
    }

    private static SiteModel Site() => new()
    {
        Name = "test",
        Title = "Test",
        Url = "https://example.com",
        BaseUrl = "/",
        Language = "en"
    };

    private static PageModel PageModel(params (string Key, string Value)[] fields) => new()
    {
        Site = Site(),
        Page = new PageInfo
        {
            Title = "Hello",
            Content = string.Empty,
            Url = "/hello/",
            Fields = fields.ToDictionary(
                field => field.Key,
                field => new ContentField("text", field.Value),
                StringComparer.OrdinalIgnoreCase)
        }
    };
}
