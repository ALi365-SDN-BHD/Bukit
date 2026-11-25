using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildResultFactoryTests
{
    [Fact]
    public void PortableOutputPath_IsRelativeToTheRoot_WithForwardSlashes()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-portable-root");

        Assert.Equal("dist", BuildResultFactory.PortableOutputPath(root, Path.Combine(root, "dist")));
        Assert.Equal("out/site", BuildResultFactory.PortableOutputPath(root, Path.Combine("out", "site")));
        Assert.Equal("../elsewhere", BuildResultFactory.PortableOutputPath(root, Path.Combine(Path.GetTempPath(), "elsewhere")));
    }

    [Fact]
    public void ReportedOutputPath_KeepsARelativeConfiguredOutput_AndRelativizesAbsoluteOrReportedOnes()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-portable-root");

        Assert.Equal("dist", BuildResultFactory.ReportedOutputPath("dist", root, null));
        Assert.Equal("out/site", BuildResultFactory.ReportedOutputPath("out/site", root, null));
        Assert.Equal("public", BuildResultFactory.ReportedOutputPath(Path.Combine(root, "public"), root, null));
        Assert.Equal("dist", BuildResultFactory.ReportedOutputPath(Path.Combine(Path.GetTempPath(), "staging"), root, Path.Combine(root, "dist")));
    }
}
