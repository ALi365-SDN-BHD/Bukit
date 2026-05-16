using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ArgReaderExtendedTests
{
    [Fact]
    public void Constructor_NullArgs_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ArgReader(null!));
    }

    [Fact]
    public void Constructor_EmptyArgs_CommandIsNull()
    {
        var reader = new ArgReader(Array.Empty<string>());

        Assert.Null(reader.Command);
        Assert.Empty(reader.RemainingArgs);
    }

    [Fact]
    public void HasFlag_DuplicateFlag_ReturnsTrue()
    {
        var reader = new ArgReader(new[] { "--verbose", "--verbose" });

        Assert.True(reader.HasFlag("--verbose"));
    }

    [Fact]
    public void GetOption_EqualsSyntax_ReturnsValue()
    {
        var reader = new ArgReader(new[] { "--key=value" });

        Assert.Equal("value", reader.GetOption("--key"));
    }

    [Fact]
    public void GetOption_NonExistent_ReturnsNull()
    {
        var reader = new ArgReader(new[] { "--name", "test" });

        Assert.Null(reader.GetOption("--nonexistent"));
    }

    [Fact]
    public void GetArg_NegativeIndex_ReturnsNull()
    {
        var reader = new ArgReader(new[] { "command", "arg1" });

        Assert.Null(reader.GetArg(-1));
    }

    [Fact]
    public void GetArg_OutOfRange_ReturnsNull()
    {
        var reader = new ArgReader(new[] { "command" });

        Assert.Null(reader.GetArg(5));
    }

    [Fact]
    public void GetOption_PartialMatch_DoesNotMatchPrefix()
    {
        var reader = new ArgReader(new[] { "--foo=bar" });

        Assert.Null(reader.GetOption("--fo"));
    }
}
