using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TemplateStaticAnalysisTests : IDisposable
{
    private readonly string _layoutsDir;
    private readonly string _baseDir;

    public TemplateStaticAnalysisTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "bukit-template-analysis-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(_baseDir, "layouts");
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDir))
        {
            Directory.Delete(_baseDir, recursive: true);
        }
    }

    private void WriteTemplate(string relativePath, string content)
    {
        var fullPath = Path.Combine(_layoutsDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_DirectContentUsage_ReturnsTrue()
    {
        WriteTemplate("pages/post.html", "{{ .content }}");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/post.html");

        Assert.True(result.NeedsPageContent);
        Assert.Equal("analysis", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_NoContentUsage_ReturnsFalse()
    {
        WriteTemplate("pages/list.html", "{{ for p in pages }}{{ p.title }}{{ end }}");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/list.html");

        Assert.False(result.NeedsPageContent);
        Assert.Equal("analysis", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_LayoutUsesContent_ReturnsTrue()
    {
        WriteTemplate("layouts/base.html", "{{ .content }}");
        WriteTemplate("pages/post.html", "{% layout \"layouts/base.html\" %}\n<h1>{{ page.title }}</h1>");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/post.html");

        Assert.True(result.NeedsPageContent);
        Assert.Equal("analysis", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_LayoutDoesNotUseContent_ReturnsFalse()
    {
        WriteTemplate("layouts/base.html", "<h1>{{ page.title }}</h1>");
        WriteTemplate("pages/post.html", "{% layout \"layouts/base.html\" %}\n<p>{{ page.summary }}</p>");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/post.html");

        Assert.False(result.NeedsPageContent);
        Assert.Equal("analysis", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_StaticIncludeUsesContent_ReturnsTrue()
    {
        WriteTemplate("partials/card.html", "{{ .content }}");
        WriteTemplate("pages/list.html", "{{ for p in pages }}{{ include \"partials/card.html\" }}{{ end }}");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/list.html");

        Assert.True(result.NeedsPageContent);
        Assert.Equal("analysis", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_DynamicInclude_ReturnsNull()
    {
        WriteTemplate("pages/list.html", "{{ include dynamicPartial }}");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/list.html");

        Assert.Null(result.NeedsPageContent);
        Assert.Equal("dynamic_include", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_TemplateNotFound_ReturnsNullWithMissingTemplateSource()
    {
        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/does_not_exist.html");

        Assert.Null(result.NeedsPageContent);
        Assert.Equal("missing_template", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_LayoutCycle_ReturnsNullWithCycleSource()
    {
        WriteTemplate("layouts/a.html", "{% layout \"layouts/b.html\" %}");
        WriteTemplate("layouts/b.html", "{% layout \"layouts/a.html\" %}");
        WriteTemplate("pages/post.html", "{% layout \"layouts/a.html\" %}\n<h1>{{ page.title }}</h1>");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/post.html");

        Assert.Null(result.NeedsPageContent);
        Assert.Equal("cycle", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_OnlyPageTitleNoContentRef_ReturnsFalse()
    {
        WriteTemplate("pages/post.html", "{{ page.title }}");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/post.html");

        Assert.False(result.NeedsPageContent);
        Assert.Equal("analysis", result.Source);
    }

    [Fact]
    public void AnalyzeNeedsPageContent_ScribanCommentContainingContent_ReturnsFalse()
    {
        WriteTemplate("pages/post.html", "{{* .content *}}\n<h1>Hello</h1>");

        var result = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(_layoutsDir, "pages/post.html");

        Assert.False(result.NeedsPageContent);
        Assert.Equal("analysis", result.Source);
    }
}
