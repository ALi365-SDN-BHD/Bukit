using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class DiagnosticExceptionFormatterTests
{
    [Fact]
    public void Format_ExceptionWithoutCode_ReturnsMessageOnly()
    {
        var ex = new ConfigException("site.name is required.");
        var result = DiagnosticExceptionFormatter.Format(ex);
        Assert.Equal("site.name is required.", result);
    }

    [Fact]
    public void Format_ExceptionWithCode_ReturnsFormattedMessage()
    {
        var ex = new ConfigException("site.name is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        var result = DiagnosticExceptionFormatter.Format(ex);
        Assert.Equal("[BKT-0001] site.name is required.", result);
    }

    [Fact]
    public void Format_ContentException_ReturnsFormattedMessage()
    {
        var ex = new ContentException("content load failed.", DiagnosticCode.ContentLoadFailed);
        var result = DiagnosticExceptionFormatter.Format(ex);
        Assert.Equal("[BKT-0501] content load failed.", result);
    }

    [Fact]
    public void Format_RenderException_ReturnsFormattedMessage()
    {
        var ex = new RenderException("template parse error.", DiagnosticCode.RenderTemplateParseError);
        var result = DiagnosticExceptionFormatter.Format(ex);
        Assert.Equal("[BKT-0302] template parse error.", result);
    }
}
