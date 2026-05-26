using Scriban;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ScribanTemplateLinterTests : IDisposable
{
    private readonly string _tempDir;

    public ScribanTemplateLinterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-linter-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Lint_Directory_KnownVariables_ReturnsNoWarnings()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"), "{{ page.title }} {{ page.content }} {{ site.name }}");

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "page.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Directory_UnknownVariable_ReturnsWarning()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"), "{{ page.mispelled_field }}");

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "page.html");

        Assert.Single(warnings);
        var w = warnings[0];
        Assert.Contains("page.mispelled_field", w.Variable);
        Assert.Contains("page.html", w.Template);
    }

    [Fact]
    public void Lint_Directory_MultipleUnknown_ReturnsAllWarnings()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"), "{{ page.wrng_titl }} {{ site.invalid_param }}");

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "page.html");

        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void Lint_Directory_Mixed_KnownAndUnknown()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"), "{{ page.title }} {{ page.wrong_field }} {{ site.name }}");

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "page.html");

        Assert.Single(warnings);
        Assert.Contains("page.wrong_field", warnings[0].Variable);
    }

    [Fact]
    public void Lint_Directory_LoopVarAccess_DoesNotWarnForPageFields()
    {
        File.WriteAllText(Path.Combine(_tempDir, "list.html"), """
            {{ for p in pages }}
            {{ p.title }} {{ p.url }} {{ p.summary }}
            {{ end }}
            """);

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "list.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Directory_LoopVarAccess_WarnsForUnknownFields()
    {
        File.WriteAllText(Path.Combine(_tempDir, "list.html"), """
            {{ for p in pages }}
            {{ p.wrongo_field }}
            {{ end }}
            """);

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "list.html");

        Assert.Single(warnings);
        Assert.Contains("p.wrongo_field", warnings[0].Variable);
    }

    [Fact]
    public void Lint_Directory_SectionAccess_IsAllowed()
    {
        var templateDir = Path.Combine(_tempDir, "pages");
        Directory.CreateDirectory(templateDir);
        File.WriteAllText(Path.Combine(templateDir, "page.html"), "{{ section.type }} {{ section.props.heading }}");

        var warnings = ScribanTemplateLinter.LintDirectory(templateDir, "page.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_DirectCall_KnownOnly()
    {
        var template = Template.Parse("{{ page.title }} {{ site.name }}");

        var warnings = ScribanTemplateLinter.LintTemplate(template, "page.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_DirectCall_UnknownFound()
    {
        var template = Template.Parse("{{ page.bad_field }}");

        var warnings = ScribanTemplateLinter.LintTemplate(template, "page.html");

        Assert.Single(warnings);
        Assert.Contains("page.bad_field", warnings[0].Variable);
    }
}
