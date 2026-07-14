using Scriban;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ScribanSymbolAnalyzerTests
{
    [Fact]
    public void Analyze_AssignmentAndCapture_RecordsDeclarationsNotWriteReferences()
    {
        var template = Template.Parse("""
            {{ heading = page.title }}
            {{ capture summary }}{{ page.summary }}{{ end }}
            {{ heading }} {{ summary }}
            """);

        var analysis = ScribanSymbolAnalyzer.Analyze(template);

        Assert.Contains("heading", analysis.Declarations);
        Assert.Contains("summary", analysis.Declarations);
        Assert.Contains(analysis.References, x => x.Path == "page.title");
        Assert.Contains(analysis.References, x => x.Path == "page.summary");
        Assert.Contains(analysis.References, x => x.Path == "heading" && x.Kind == ScribanSymbolReferenceKind.Local);
        Assert.Contains(analysis.References, x => x.Path == "summary" && x.Kind == ScribanSymbolReferenceKind.Local);
    }

    [Fact]
    public void Analyze_PageCollectionLoop_InfersArbitraryVariableAsPageItem()
    {
        var template = Template.Parse("{{ for card in pages }}{{ card.title }}{{ end }}");

        var analysis = ScribanSymbolAnalyzer.Analyze(template);

        Assert.Contains("card", analysis.Declarations);
        Assert.Contains(analysis.References, x =>
            x.Path == "card.title" && x.Kind == ScribanSymbolReferenceKind.PageItem);
    }

    [Fact]
    public void Analyze_ForLoop_DeclaresLoopRuntimeObjectInsideBody()
    {
        var template = Template.Parse("{{ for card in pages }}{{ for.index }} {{ for.rindex }}{{ end }}");

        var analysis = ScribanSymbolAnalyzer.Analyze(template);

        Assert.Contains(analysis.References, x =>
            x.Path == "for.index" && x.Kind == ScribanSymbolReferenceKind.Local);
        Assert.Contains(analysis.References, x =>
            x.Path == "for.rindex" && x.Kind == ScribanSymbolReferenceKind.Local);
    }

    [Fact]
    public void Analyze_ThisMember_RecordsCurrentContextReference()
    {
        var template = Template.Parse("{{ this.page.title }}");

        var analysis = ScribanSymbolAnalyzer.Analyze(template);

        Assert.Contains(analysis.References, x =>
            x.Path == "this.page.title" && x.Kind == ScribanSymbolReferenceKind.CurrentContext);
    }
}
