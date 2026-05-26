using Scriban;
using Scriban.Syntax;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ScribanVariableCollectorTests
{
    [Fact]
    public void Collect_SimplePageVariable_ExtractsPageTitle()
    {
        var template = Template.Parse("{{ page.title }}");
        var vars = ScribanVariableCollector.Collect(template);

        Assert.Contains("page.title", vars);
    }

    [Fact]
    public void Collect_SiteAndPageVars_ExtractsAllPaths()
    {
        var template = Template.Parse("{{ site.name }} - {{ page.title }} - {{ site.url }}");
        var vars = ScribanVariableCollector.Collect(template);

        Assert.Contains("site.name", vars);
        Assert.Contains("page.title", vars);
        Assert.Contains("site.url", vars);
    }

    [Fact]
    public void Collect_NestedMemberAccess_ExtractsFullPath()
    {
        var template = Template.Parse("{{ page.fields.author }} {{ site.analytics.google_analytics_id }}");
        var vars = ScribanVariableCollector.Collect(template);

        Assert.Contains("page.fields.author", vars);
        Assert.Contains("site.analytics.google_analytics_id", vars);
    }

    [Fact]
    public void Collect_WithinForLoop_ExtractsListVariables()
    {
        var template = Template.Parse("{{ for p in pages }} {{ p.title }} {{ p.url }} {{ end }}");
        var vars = ScribanVariableCollector.Collect(template);

        Assert.Contains("p.title", vars);
        Assert.Contains("p.url", vars);
    }

    [Fact]
    public void Collect_WithinIfBlock_ExtractsConditionVariables()
    {
        var template = Template.Parse("{{ if page.seo }} {{ page.seo.title }} {{ end }}");
        var vars = ScribanVariableCollector.Collect(template);

        Assert.Contains("page.seo", vars);
        Assert.Contains("page.seo.title", vars);
    }

    [Fact]
    public void Collect_IgnoresLiteralsAndFunctions()
    {
        var template = Template.Parse("{{ 'hello' }} {{ now | date.to_string '%Y' }}");
        var vars = ScribanVariableCollector.Collect(template);

        Assert.DoesNotContain("hello", vars);
        Assert.DoesNotContain("'hello'", vars);
    }

    [Fact]
    public void Collect_DeepMemberChain_ExtractsFullPath()
    {
        var template = Template.Parse("{{ page.seo.og.image }} {{ page.seo.twitter.card }}");
        var vars = ScribanVariableCollector.Collect(template);

        Assert.Contains("page.seo.og.image", vars);
        Assert.Contains("page.seo.twitter.card", vars);
    }

    [Fact]
    public void Collect_SectionWithProps_ExtractsSectionProps()
    {
        var template = Template.Parse("{{ section.props.heading }} {{ section.props.items }}");
        var vars = ScribanVariableCollector.Collect(template);

        Assert.Contains("section.props.heading", vars);
        Assert.Contains("section.props.items", vars);
    }
}
