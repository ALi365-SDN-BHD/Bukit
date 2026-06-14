using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DoctorPathHelpersTests
{
    [Fact]
    public void ToRelativeTemplatePath_WithWhitespace_ReturnsOriginalValue()
    {
        Assert.Equal("   ", DoctorPathHelpers.ToRelativeTemplatePath("/tmp/layouts", "   "));
    }

    [Fact]
    public void ToRelativeTemplatePath_WithRelativePath_NormalizesSlashes()
    {
        var value = DoctorPathHelpers.ToRelativeTemplatePath("/tmp/layouts", @"pages\home.html");
        Assert.Equal("pages/home.html", value);
    }

    [Fact]
    public void ToRelativeTemplatePath_WithAbsolutePath_RelativizesAgainstLayoutsDir()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-doctor-paths");
        var filePath = Path.Combine(layoutsDir, "pages", "home.html");

        var value = DoctorPathHelpers.ToRelativeTemplatePath(layoutsDir, filePath);

        Assert.Equal("pages/home.html", value);
    }
}
