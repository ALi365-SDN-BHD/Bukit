using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering.Scriban;
using Scriban.Syntax;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class DerivedFieldTemplateTests
{
    [Fact]
    public void Render_ReadsTopLevelFieldsAndLinks()
    {
        var template = Parse("{{ name }} ({{ name_en }}) · {{ cities_links[0].title }}");
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = new("text", "吉隆坡"),
            ["name_en"] = new("text", "Kuala Lumpur"),
            ["cities_links"] = new("list", new List<object?> { new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["title"] = "Penang" } })
        };

        Assert.Equal("吉隆坡 (Kuala Lumpur) · Penang", template.Render(fields));
    }

    [Fact]
    public void Render_UsesTheInvariantCultureAndTrims()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var template = Parse("  {{ price * 1.5 }}  ");

            Assert.Equal("3.75", template.Render(new Dictionary<string, ContentField> { ["price"] = new("number", 2.5d) }));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("{{ date.now }}", "date.now")]
    [InlineData("{{ date.now | date.to_string '%Y' }}", "date.now")]
    [InlineData("{{ math.uuid }}", "math.uuid")]
    [InlineData("{{ math.random 1 10 }}", "math.random")]
    public void Render_ClockAndRandomFunctionsFailWithTheirName(string template, string name)
    {
        var exception = Assert.ThrowsAny<ScriptRuntimeException>(() => Parse(template).Render(null));

        Assert.Contains($"{name} is not available in derived fields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_LoopLimit_Throws()
        => Assert.ThrowsAny<ScriptRuntimeException>(() => Parse("{{ for i in 1..5000 }}x{{ end }}").Render(null));

    [Fact]
    public void Render_OutputLimit_FailsWhileRendering()
    {
        // 1000 x 1000 x 60000 characters would be 60 GB; the limit stops it at 64 KiB.
        var template = Parse("{{ for i in 1..1000 }}{{ for j in 1..1000 }}{{ \"\" | string.pad_left 60000 }}{{ end }}{{ end }}");

        var exception = Assert.ThrowsAny<ScriptRuntimeException>(() => template.Render(null));

        Assert.Contains("longer than 65536 characters", exception.Message, StringComparison.Ordinal);
        Assert.Equal(new string('x', 1000), Parse("{{ for i in 1..1000 }}x{{ end }}").Render(null));
    }

    [Fact]
    public void Parse_InvalidTemplate_ReportsTheError()
    {
        Assert.Null(DerivedFieldTemplate.Parse("{{ if }}", out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("Short text.", 20, "Short text.")]
    [InlineData("<p>Two   lines</p>\n<p>here</p>", 40, "Two lines here")]
    [InlineData("First sentence. Second sentence goes on and on.", 30, "First sentence.")]
    [InlineData("abcdefghijklmnopqrstuvwxyz", 10, "abcdefghi…")]
    [InlineData("吉隆坡是马来西亚的首都和最大城市", 10, "吉隆坡是…")]
    [InlineData("", 10, "")]
    public void Describe_CutsByDisplayWidth(string text, int width, string expected)
        => Assert.Equal(expected, DerivedFieldTemplate.Describe(text, width));

    [Fact]
    public void Render_SeoDescribeIsAvailableAsAFilter()
        => Assert.Equal(
            "abcdefghi…",
            Parse("{{ summary | seo.describe 10 }}").Render(new Dictionary<string, ContentField> { ["summary"] = new("text", "abcdefghijklmnopqrstuvwxyz") }));

    private static DerivedFieldTemplate Parse(string text)
        => DerivedFieldTemplate.Parse(text, out var error) ?? throw new InvalidOperationException(error);
}
