using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class ContentExceptionTests
{
    [Fact]
    public void ContentException_DefaultConstructor_SetsMessage()
    {
        var ex = new ContentException("content failed.");
        Assert.Equal("content failed.", ex.Message);
        Assert.Null(ex.Code);
    }

    [Fact]
    public void ContentException_WithCode_SetsCode()
    {
        var ex = new ContentException("content failed.", DiagnosticCode.ContentLoadFailed);
        Assert.Equal(DiagnosticCode.ContentLoadFailed, ex.Code);
        Assert.Equal("content failed.", ex.Message);
    }

    [Fact]
    public void ContentException_WithInnerException_PreservesInner()
    {
        var inner = new InvalidOperationException("inner error");
        var ex = new ContentException("content failed.", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("content failed.", ex.Message);
        Assert.Null(ex.Code);
    }

    [Fact]
    public void ContentException_WithInnerExceptionAndCode_PreservesAll()
    {
        var inner = new InvalidOperationException("inner error");
        var ex = new ContentException("content failed.", inner, DiagnosticCode.ContentProviderUnavailable);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("content failed.", ex.Message);
        Assert.Equal(DiagnosticCode.ContentProviderUnavailable, ex.Code);
    }

    [Fact]
    public void ContentException_FormattedMessage_IncludesCode()
    {
        var ex = new ContentException("content failed.", DiagnosticCode.ContentLoadFailed);
        var formatted = DiagnosticExceptionFormatter.Format(ex);
        Assert.Equal("[BKT-0501] content failed.", formatted);
    }

    [Fact]
    public void RenderException_DefaultConstructor_SetsMessage()
    {
        var ex = new RenderException("render failed.");
        Assert.Equal("render failed.", ex.Message);
        Assert.Null(ex.Code);
    }

    [Fact]
    public void RenderException_WithCode_SetsCode()
    {
        var ex = new RenderException("render failed.", DiagnosticCode.RenderFailed);
        Assert.Equal(DiagnosticCode.RenderFailed, ex.Code);
    }

    [Fact]
    public void RenderException_WithInnerException_PreservesInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new RenderException("render failed.", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Null(ex.Code);
    }

    [Fact]
    public void RenderException_WithInnerExceptionAndCode_PreservesAll()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new RenderException("render failed.", inner, DiagnosticCode.RenderFailed);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(DiagnosticCode.RenderFailed, ex.Code);
    }

    [Fact]
    public void RenderException_FormattedMessage_IncludesCode()
    {
        var ex = new RenderException("render failed.", DiagnosticCode.RenderFailed);
        var formatted = DiagnosticExceptionFormatter.Format(ex);
        Assert.Equal("[BKT-0399] render failed.", formatted);
    }
}
