using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering.Scriban;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace Bukit.Rendering.Tests;

/// <summary>
/// Structured field values as the CloudBase source builds them (sorted ordinal maps, lists, and
/// <see cref="long"/>/<see cref="decimal"/>/<see cref="bool"/>/null leaves) rendered through the page fields model.
/// </summary>
public sealed class StructuredFieldTemplateTests
{
    private static readonly IReadOnlyDictionary<string, ContentField> Fields = new Dictionary<string, ContentField>
    {
        ["days"] = new("list", new List<object?>
        {
            new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["attractions"] = new List<object?> { "a1", "a2" },
                ["attractions_links"] = new List<object?>
                {
                    new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["key"] = "a1", ["title"] = "Batu Caves" }
                },
                ["free"] = false,
                ["note"] = null,
                ["price"] = 12.50m,
                ["title"] = "Day 1"
            }
        }),
        ["months"] = new("list", new List<object?> { 3L, 4L, 5L }),
        // Markdown front matter gives list items as text.
        ["md_months"] = new("list", new List<string> { "3", "4", "5" }),
        ["contact"] = new("list", new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["phone"] = "+60" }),
        // content.links output: a list of sorted link maps.
        ["cities_links"] = new("list", new List<object?>
        {
            new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["key"] = "kl", ["title"] = "Kuala Lumpur", ["url"] = "/cities/kl/" },
            new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["key"] = "ipoh", ["title"] = "Ipoh", ["url"] = null }
        })
    };

    [Theory]
    [InlineData("{{ for day in page.fields.days.value }}{{ day.title }}:{{ for a in day.attractions }}{{ a }};{{ end }}{{ end }}", "Day 1:a1;a2;")]
    [InlineData("{{ page.fields.days.value[0].price }}|{{ page.fields.days.value[0].free }}|{{ page.fields.days.value[0].note }}", "12.50|false|")]
    [InlineData("{{ page.fields.contact.value.phone }}", "+60")]
    [InlineData("{{ for m in page.fields.months.value }}{{ if m == 4 }}April{{ end }}{{ end }}", "April")]
    [InlineData("{{ page.fields.months.value | array.join ',' }}", "3,4,5")]
    [InlineData("{{ for m in page.fields.md_months.value }}{{ if m == \"4\" }}April{{ end }}{{ end }}|{{ page.fields.md_months.value | array.contains \"4\" }}", "April|true")]
    [InlineData("{{ for city in page.fields.cities_links.value }}<a href=\"{{ city.url }}\">{{ city.title }}</a>{{ end }}", "<a href=\"/cities/kl/\">Kuala Lumpur</a><a href=\"\">Ipoh</a>")]
    [InlineData("{{ for day in page.fields.days.value }}{{ for a in day.attractions_links }}{{ a.title }}{{ end }}{{ end }}", "Batu Caves")]
    public void Render_StructuredFields(string template, string expected)
        => Assert.Equal(expected, Render(template));

    /// <summary>
    /// Scriban parses the literal 4 as int and <c>array.contains</c> compares with Equals, so a long
    /// element never matches it; the guide tells authors to compare with <c>==</c> instead.
    /// </summary>
    [Fact]
    public void Render_ArrayContainsIntLiteral_DoesNotMatchLongElements()
        => Assert.Equal("false", Render("{{ page.fields.months.value | array.contains 4 }}"));

    private static string Render(string template)
    {
        var page = new ScriptObject();
        page.SetValue("fields", ScribanDynamicValueMapper.ToFieldsScriptObject(Fields), readOnly: true);
        var globals = new ScriptObject();
        globals.SetValue("page", page, readOnly: true);
        var context = new TemplateContext();
        context.PushGlobal(globals);
        return Template.Parse(template).Render(context);
    }
}
