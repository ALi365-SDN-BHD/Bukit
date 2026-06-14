using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class CommandArgumentExceptionTests
{
    [Fact]
    public void Constructor_MessageOnly_PreservesMessage()
    {
        var ex = new CommandArgumentException("missing output path");

        Assert.Equal("missing output path", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesInnerException()
    {
        var inner = new InvalidOperationException("bad state");
        var ex = new CommandArgumentException("invalid argument", inner);

        Assert.Equal("invalid argument", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
