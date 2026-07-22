using System.Text.Json;
using Bukit.Notion.Rendering.BlockRenderers;
using Xunit;

namespace Bukit.Notion.Tests;

/// <summary>
/// Regression tests for P1-2: BlockRenderers must HTML-encode the Notion block color value
/// when emitting it into the <c>class="notion-{color}"</c> attribute, so that a malicious
/// color string cannot break out of the class attribute and inject markup.
/// </summary>
public sealed class BlockRendererColorEncodingTests
{
    private const string MaliciousColor = "blue\"><img onerror=x>";
    private const string EncodedColor = "blue&quot;&gt;&lt;img onerror=x&gt;";

    private static string BuildBlockJson(string typeKey, string innerJson)
    {
        return $$"""
        {
          "id": "blk-color-injection",
          "has_children": false,
          "{{typeKey}}": {{innerJson}}
        }
        """;
    }

    private static void AssertNoAttributeBreakout(string? html)
    {
        Assert.NotNull(html);
        // The raw injected payload must not appear verbatim.
        Assert.DoesNotContain(MaliciousColor, html);
        Assert.DoesNotContain("<img onerror=x>", html);
        // The encoded form must be present inside the class attribute.
        Assert.Contains(EncodedColor, html);
    }

    [Fact]
    public async Task CalloutBlockRenderer_EncodesMaliciousColor()
    {
        var json = BuildBlockJson("callout", """
        {
          "rich_text": [ { "plain_text": "hi" } ],
          "color": "blue\"><img onerror=x>"
        }
        """);
        using var doc = JsonDocument.Parse(json);

        var html = await new CalloutBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        AssertNoAttributeBreakout(html);
    }

    [Fact]
    public async Task ToDoBlockRenderer_EncodesMaliciousColor()
    {
        var json = BuildBlockJson("to_do", """
        {
          "rich_text": [ { "plain_text": "hi" } ],
          "checked": false,
          "color": "blue\"><img onerror=x>"
        }
        """);
        using var doc = JsonDocument.Parse(json);

        var html = await new ToDoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        AssertNoAttributeBreakout(html);
    }

    [Fact]
    public async Task ToggleBlockRenderer_EncodesMaliciousColor()
    {
        var json = BuildBlockJson("toggle", """
        {
          "rich_text": [ { "plain_text": "hi" } ],
          "color": "blue\"><img onerror=x>"
        }
        """);
        using var doc = JsonDocument.Parse(json);

        var html = await new ToggleBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        AssertNoAttributeBreakout(html);
    }

    [Fact]
    public async Task BookmarkBlockRenderer_EncodesMaliciousColor()
    {
        var json = BuildBlockJson("bookmark", """
        {
          "url": "https://example.com",
          "caption": [],
          "color": "blue\"><img onerror=x>"
        }
        """);
        using var doc = JsonDocument.Parse(json);

        var html = await new BookmarkBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        AssertNoAttributeBreakout(html);
    }

    [Fact]
    public async Task EquationBlockRenderer_EncodesMaliciousColor()
    {
        var json = BuildBlockJson("equation", """
        {
          "expression": "x^2",
          "color": "blue\"><img onerror=x>"
        }
        """);
        using var doc = JsonDocument.Parse(json);

        var html = await new EquationBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        AssertNoAttributeBreakout(html);
    }
}
