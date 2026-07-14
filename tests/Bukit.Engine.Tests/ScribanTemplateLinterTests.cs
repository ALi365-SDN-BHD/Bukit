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
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void Lint_Directory_KnownVariables_ReturnsNoWarnings()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"), "{{ page.title }} {{ page.content }} {{ site.name }}");

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "page.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Directory_BaseUrlLocalVariable_ReturnsNoWarnings()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"), """
            {{ base_url = site.base_url }}
            {{ if base_url == "/" }}{{ base_url = "" }}{{ end }}
            <link rel="stylesheet" href="{{ base_url }}/assets/style.css" />
            """);

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
            {{ p.title }} {{ p.url }} {{ p.summary }} {{ p.updated_at }}
            {{ end }}
            """);

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "list.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Directory_PageUpdatedAt_IsKnown()
    {
        File.WriteAllText(Path.Combine(_tempDir, "page.html"), "{{ page.updated_at }}");

        var warnings = ScribanTemplateLinter.LintDirectory(_tempDir, "page.html");

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

    [Fact]
    public void Lint_Template_ScribanBuiltins_ReturnsNoWarnings()
    {
        var template = Template.Parse("""
            {{ date.now | date.to_string "%Y" }}
            {{ "bukit" | string.upcase }}
            {{ [1, 2] | array.size }}
            {{ 1.2 | math.round }}
            {{ "<b>" | html.escape }}
            {{ {} | object.size }}
            {{ "a" | regex.match "a" }}
            {{ timespan.from_seconds 1 }}
            {{ empty }} {{ blank }} {{ include }} {{ include_join }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "builtins.html");

        Assert.False(template.HasErrors, string.Join(Environment.NewLine, template.Messages));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_BukitRuntimeHelpers_ReturnsNoWarnings()
    {
        var template = Template.Parse("""
            {{ image.srcset page.url }}
            {{ util.slugify page.title }}
            {{ comp.render "card" }}
            {{ render_section page.fields.section.value }}
            {{ shortcode "badge" }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "helpers.html");

        Assert.False(template.HasErrors, string.Join(Environment.NewLine, template.Messages));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_AssignedLocal_ReturnsNoWarnings()
    {
        var template = Template.Parse("""
            {{ heading = page.title }}
            {{ if heading }}{{ heading }}{{ end }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "local.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_ArbitraryPageLoopVariable_ValidatesPageFields()
    {
        var template = Template.Parse("""
            {{ for card in items }}
              {{ card.title }} {{ card.url }}
            {{ end }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "list.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_ArbitraryPageLoopVariable_WarnsForUnknownPageField()
    {
        var template = Template.Parse("""
            {{ for card in pages }}
              {{ card.titel }}
            {{ end }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "list.html");

        var warning = Assert.Single(warnings);
        Assert.Equal("card.titel", warning.Variable);
    }

    [Fact]
    public void Lint_Template_NestedScopes_ResolveOuterPageItemInnerLocalAndLoopRuntime()
    {
        var template = Template.Parse("""
            {{ for card in pages }}
              {{ for badge in card.fields.badges.value }}
                {{ card.title }} {{ badge.name }} {{ for.index }} {{ for.rindex }}
              {{ end }}
            {{ end }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "nested-list.html");

        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("{{ page.mispelled_field }}", "page.mispelled_field")]
    [InlineData("{{ site.namme }}", "site.namme")]
    [InlineData("{{ for card in pages }}{{ card.titel }}{{ end }}", "card.titel")]
    public void Lint_Template_KnownContextTypos_ReturnSingleStableWarning(
        string source,
        string expectedVariable)
    {
        var template = Template.Parse(source);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "typo.html");

        var warning = Assert.Single(warnings);
        Assert.Equal(expectedVariable, warning.Variable);
    }

    [Fact]
    public void Lint_Template_FunctionParameterAndCaptureTarget_ReturnsNoWarnings()
    {
        var template = Template.Parse("""
            {{ func render_card(card) }}
              {{ card.title }}
            {{ end }}
            {{ capture heading }}{{ page.title }}{{ end }}
            {{ render_card page }} {{ heading }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "scopes.html");

        Assert.False(template.HasErrors, string.Join(Environment.NewLine, template.Messages));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_OpenDynamicFields_ReturnsNoWarnings()
    {
        var template = Template.Parse("""
            {{ page.fields.product.value.vendor.name }}
            {{ page.content_model.presentation.custom_heading }}
            {{ site.params.brand.palette.primary }}
            {{ site.modules.navigation[0].fields.link.value }}
            {{ site.data.catalog.entries[0].sku }}
            {{ site.data_index.settings.contact.email }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "dynamic.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_StableListRoots_ReturnsNoWarnings()
    {
        var template = Template.Parse("""
            {{ seo.canonical }} {{ pagination.total_pages }}
            {{ collection.key }} {{ taxonomy.kind }} {{ filter.operator }}
            """);

        var warnings = ScribanTemplateLinter.LintTemplate(template, "list.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_ThisPageTitle_ReturnsNoWarnings()
    {
        var template = Template.Parse("{{ this.page.title }}");

        var warnings = ScribanTemplateLinter.LintTemplate(template, "page.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_ThisTitle_ReturnsKnownContextWarning()
    {
        var template = Template.Parse("{{ this.title }}");

        var warnings = ScribanTemplateLinter.LintTemplate(template, "page.html");

        var warning = Assert.Single(warnings);
        Assert.Equal("this.title", warning.Variable);
        Assert.Contains("current template context", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lint_Template_UnknownExtensionRoot_IsIndeterminateAndDoesNotWarn()
    {
        var template = Template.Parse("{{ commerce.product.title }} {{ this.commerce.product.title }}");

        var warnings = ScribanTemplateLinter.LintTemplate(template, "extension.html");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Lint_Template_BinderDoesNotExposeKnownLookingFields_ReturnsWarnings()
    {
        var template = Template.Parse("{{ page.alternates }} {{ page.term }} {{ page.terms }} {{ seo.schema_type }}");

        var warnings = ScribanTemplateLinter.LintTemplate(template, "invalid-binder-fields.html");

        Assert.Collection(
            warnings.OrderBy(x => x.Variable, StringComparer.Ordinal),
            warning => Assert.Equal("page.alternates", warning.Variable),
            warning => Assert.Equal("page.term", warning.Variable),
            warning => Assert.Equal("page.terms", warning.Variable),
            warning => Assert.Equal("seo.schema_type", warning.Variable));
    }
}
