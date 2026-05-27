using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class ShortcodeProcessorTests
{
    [Fact]
    public void RenderShortcodes_NullTemplates_ReturnsOriginalHtml()
    {
        var result = ShortcodeProcessor.RenderShortcodes("{% alert hello %}", null);
        Assert.Equal("{% alert hello %}", result);
    }

    [Fact]
    public void RenderShortcodes_EmptyTemplates_ReturnsOriginalHtml()
    {
        var result = ShortcodeProcessor.RenderShortcodes("{% alert hello %}", new Dictionary<string, string>());
        Assert.Equal("{% alert hello %}", result);
    }

    [Fact]
    public void RenderShortcodes_NullOrWhitespaceHtml_ReturnsOriginal()
    {
        var templates = new Dictionary<string, string> { ["alert"] = "<div>{{ $1 }}</div>" };
        Assert.Null(ShortcodeProcessor.RenderShortcodes(null!, templates));
        Assert.Equal("", ShortcodeProcessor.RenderShortcodes("", templates));
        Assert.Equal("   ", ShortcodeProcessor.RenderShortcodes("   ", templates));
    }

    [Fact]
    public void RenderShortcodes_UnknownShortcode_ReturnsOriginalTag()
    {
        var templates = new Dictionary<string, string> { ["alert"] = "<div>{{ $1 }}</div>" };
        var result = ShortcodeProcessor.RenderShortcodes("{% unknown test %}", templates);
        Assert.Equal("{% unknown test %}", result);
    }

    [Fact]
    public void RenderShortcodes_SimpleReplacement_ReturnsReplacedHtml()
    {
        var templates = new Dictionary<string, string> { ["alert"] = "<div class=\"alert\">{{ $1 }}</div>" };
        var result = ShortcodeProcessor.RenderShortcodes("{% alert \"hello\" %}", templates);
        Assert.Equal("<div class=\"alert\">hello</div>", result);
    }

    [Fact]
    public void RenderShortcodes_MultiplePositionalArgs_ReplacesCorrectly()
    {
        var templates = new Dictionary<string, string> { ["card"] = "<h3>{{ $1 }}</h3><p>{{ $2 }}</p>" };
        var result = ShortcodeProcessor.RenderShortcodes("{% card \"Title\" \"Description text\" %}", templates);
        Assert.Equal("<h3>Title</h3><p>Description text</p>", result);
    }

    [Fact]
    public void RenderShortcodes_SingleQuotedArgs_ReplacesCorrectly()
    {
        var templates = new Dictionary<string, string> { ["note"] = "> {{ $1 }}" };
        var result = ShortcodeProcessor.RenderShortcodes("{% note 'single quoted' %}", templates);
        Assert.Equal("> single quoted", result);
    }

    [Fact]
    public void RenderShortcodes_MultipleShortcodesInHtml_ReplacesAll()
    {
        var templates = new Dictionary<string, string>
        {
            ["alert"] = "<div>{{ $1 }}</div>",
            ["note"] = "<span>{{ $1 }}</span>"
        };
        var result = ShortcodeProcessor.RenderShortcodes("{% alert \"first\" %} and {% note \"second\" %}", templates);
        Assert.Equal("<div>first</div> and <span>second</span>", result);
    }

    [Fact]
    public void RenderShortcode_NullTemplates_ReturnsEmptyString()
    {
        var result = ShortcodeProcessor.RenderShortcode("alert", null, "hello");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void RenderShortcode_UnknownName_ReturnsEmptyString()
    {
        var templates = new Dictionary<string, string> { ["alert"] = "<div>{{ $1 }}</div>" };
        var result = ShortcodeProcessor.RenderShortcode("unknown", templates, "hello");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void RenderShortcode_ValidName_ReturnsReplacedTemplate()
    {
        var templates = new Dictionary<string, string> { ["alert"] = "<div>{{ $1 }}</div>" };
        var result = ShortcodeProcessor.RenderShortcode("alert", templates, "hello");
        Assert.Equal("<div>hello</div>", result);
    }

    [Fact]
    public void RenderShortcode_MultiplePositionalArgs_ReplacesAllPlaceholders()
    {
        var templates = new Dictionary<string, string> { ["card"] = "<h2>{{ $1 }}</h2><p>{{ $2 }}</p>" };
        var result = ShortcodeProcessor.RenderShortcode("card", templates, "Title", "Body text");
        Assert.Equal("<h2>Title</h2><p>Body text</p>", result);
    }

    [Fact]
    public void RenderShortcode_ExtraArgsIgnored_WhenTemplateHasFewerPlaceholders()
    {
        var templates = new Dictionary<string, string> { ["alert"] = "<div>{{ $1 }}</div>" };
        var result = ShortcodeProcessor.RenderShortcode("alert", templates, "first", "second");
        Assert.Equal("<div>first</div>", result);
    }

    [Fact]
    public void RenderShortcode_MissingPlaceholder_KeepsOriginal()
    {
        var templates = new Dictionary<string, string> { ["card"] = "<div>{{ $1 }} and {{ $2 }}</div>" };
        var result = ShortcodeProcessor.RenderShortcode("card", templates, "only one");
        Assert.Equal("<div>only one and {{ $2 }}</div>", result);
    }

    [Fact]
    public void ParseShortcodeArgs_DoubleQuotedArgs_ParsesCorrectly()
    {
        var result = ShortcodeProcessor.ParseShortcodeArgs("\"hello\" \"world\"");
        Assert.Equal(2, result.Count);
        Assert.Equal("hello", result["$1"]);
        Assert.Equal("world", result["$2"]);
    }

    [Fact]
    public void ParseShortcodeArgs_SingleQuotedArgs_ParsesCorrectly()
    {
        var result = ShortcodeProcessor.ParseShortcodeArgs("'hello' 'world'");
        Assert.Equal(2, result.Count);
        Assert.Equal("hello", result["$1"]);
        Assert.Equal("world", result["$2"]);
    }

    [Fact]
    public void ParseShortcodeArgs_MixedQuotes_ParsesCorrectly()
    {
        var result = ShortcodeProcessor.ParseShortcodeArgs("\"hello\" 'world'");
        Assert.Equal(2, result.Count);
        Assert.Equal("hello", result["$1"]);
        Assert.Equal("world", result["$2"]);
    }

    [Fact]
    public void ParseShortcodeArgs_EmptyInput_ReturnsEmpty()
    {
        var result = ShortcodeProcessor.ParseShortcodeArgs("");
        Assert.Empty(result);
    }
}
