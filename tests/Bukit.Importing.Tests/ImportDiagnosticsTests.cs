using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportDiagnosticsTests
{
    [Fact]
    public void ImportException_MessageOnly_PreservesKindAndMessage()
    {
        var ex = new ImportException(ImportErrorKind.UserInput, "bad input");

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Equal("bad input", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void ImportException_WithInnerException_PreservesInnerException()
    {
        var inner = new InvalidOperationException("broken");
        var ex = new ImportException(ImportErrorKind.Internal, "failed", inner);

        Assert.Equal(ImportErrorKind.Internal, ex.Kind);
        Assert.Equal("failed", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
