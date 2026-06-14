using Bukit.Rendering.Scriban;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class ImageImgFunctionTests
{
    [Fact]
    public void Invoke_RendersImageTagFromArguments()
    {
        var function = new ImageImgFunction();
        var arguments = new ScriptArray
        {
            "/img/photo.jpg",
            "Hero",
            "640,1280",
            "banner"
        };

        var result = function.Invoke(new TemplateContext(), callerContext: null, arguments, blockStatement: null);

        var text = Assert.IsType<string>(result);
        Assert.Contains("<img src=\"/img/photo.jpg\"", text);
        Assert.Contains("alt=\"Hero\"", text);
        Assert.Contains("class=\"banner\"", text);
        Assert.Contains("/img/photo.jpg?w=640 640w", text);
    }

    [Fact]
    public async Task InvokeAsync_WithoutOptionalArguments_UsesDefaults()
    {
        var function = new ImageImgFunction();
        var arguments = new ScriptArray
        {
            "https://example.com/img.jpg"
        };

        var result = await function.InvokeAsync(new TemplateContext(), callerContext: null, arguments, blockStatement: null);

        var text = Assert.IsType<string>(result);
        Assert.Contains("https://example.com/img.jpg?w=480 480w", text);
        Assert.DoesNotContain("alt=", text);
        Assert.DoesNotContain("class=", text);
    }

    [Fact]
    public void Metadata_ReportsExpectedSignature()
    {
        var function = new ImageImgFunction();

        Assert.Equal(1, function.RequiredParameterCount);
        Assert.Equal(4, function.ParameterCount);
        Assert.Equal(ScriptVarParamKind.None, function.VarParamKind);
        Assert.Equal(typeof(string), function.ReturnType);
        Assert.Equal("src", function.GetParameterInfo(0).Name);
        Assert.Equal("alt", function.GetParameterInfo(1).Name);
        Assert.Equal("sizes", function.GetParameterInfo(2).Name);
        Assert.Equal("className", function.GetParameterInfo(3).Name);
    }
}
