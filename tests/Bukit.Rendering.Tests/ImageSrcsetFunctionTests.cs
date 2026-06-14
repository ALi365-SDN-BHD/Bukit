using Bukit.Rendering.Scriban;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class ImageSrcsetFunctionTests
{
    [Fact]
    public void Invoke_UsesDefaultSizesWhenNotProvided()
    {
        var function = new ImageSrcsetFunction();
        var arguments = new ScriptArray
        {
            "/img/photo.jpg"
        };

        var result = function.Invoke(new TemplateContext(), callerContext: null, arguments, blockStatement: null);

        var text = Assert.IsType<string>(result);
        Assert.Contains("/img/photo.jpg?w=480 480w", text);
        Assert.Contains("/img/photo.jpg?w=768 768w", text);
        Assert.Contains("/img/photo.jpg?w=1200 1200w", text);
    }

    [Fact]
    public async Task InvokeAsync_WithUnsafeSource_ReturnsEmpty()
    {
        var function = new ImageSrcsetFunction();
        var arguments = new ScriptArray
        {
            "javascript:alert(1)",
            "100,200"
        };

        var result = await function.InvokeAsync(new TemplateContext(), callerContext: null, arguments, blockStatement: null);

        Assert.Equal(string.Empty, Assert.IsType<string>(result));
    }

    [Fact]
    public void Metadata_ReportsExpectedSignature()
    {
        var function = new ImageSrcsetFunction();

        Assert.Equal(1, function.RequiredParameterCount);
        Assert.Equal(2, function.ParameterCount);
        Assert.Equal(ScriptVarParamKind.None, function.VarParamKind);
        Assert.Equal(typeof(string), function.ReturnType);
        Assert.Equal("src", function.GetParameterInfo(0).Name);
        Assert.Equal("sizes", function.GetParameterInfo(1).Name);
    }
}
