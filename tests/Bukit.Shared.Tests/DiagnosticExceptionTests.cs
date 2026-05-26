using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class DiagnosticExceptionTests
{
    [Fact]
    public void ConfigException_HasDiagnosticCode()
    {
        var ex = new ConfigException("site.name is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        Assert.NotNull(ex.Code);
        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code.Value);
    }

    [Fact]
    public void ConfigException_MessagePreserved()
    {
        var ex = new ConfigException("site.name is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        Assert.Equal("site.name is required.", ex.Message);
    }

    [Fact]
    public void ConfigException_FormattedMessage_IncludesCode()
    {
        var ex = new ConfigException("site.name is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        var formatted = DiagnosticExceptionFormatter.Format(ex);
        Assert.Contains("BKT-", formatted);
        Assert.Contains("site.name is required.", formatted);
    }

    [Fact]
    public void RenderException_HasDiagnosticCode()
    {
        var ex = new RenderException("Template not found: page.html", DiagnosticCode.RenderTemplateNotFound);
        Assert.NotNull(ex.Code);
        Assert.Equal(DiagnosticCode.RenderTemplateNotFound, ex.Code.Value);
    }

    [Fact]
    public void ConfigException_BackwardCompatible_WithoutCode()
    {
        var ex = new ConfigException("site.name is required.");
        Assert.Null(ex.Code);
    }

    [Fact]
    public void ConfigException_WithInnerException_PreservesBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ConfigException("outer", inner, DiagnosticCode.ConfigInvalidValue);
        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("outer", ex.Message);
    }

    [Fact]
    public void DiagnosticCode_DescribesCategories()
    {
        var description = DiagnosticCodeFormatter.Describe(DiagnosticCode.ConfigRequiredFieldMissing);
        Assert.Contains("Config", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("required", description, StringComparison.OrdinalIgnoreCase);
    }
}
