using Bukit.Cli.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliBoundCommandExtendedTests
{
    [Fact]
    public void GetArgument_Zero_ReturnsFirstArg()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new[] { "first", "second" });

        Assert.Equal("first", command.GetArgument(0));
    }

    [Fact]
    public void GetArgument_One_ReturnsSecondArg()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new[] { "first", "second" });

        Assert.Equal("second", command.GetArgument(1));
    }

    [Fact]
    public void GetArgument_NegativeIndex_ReturnsNull()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new[] { "first" });

        Assert.Null(command.GetArgument(-1));
    }

    [Fact]
    public void GetArgument_OutOfRange_ReturnsNull()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            new[] { "first" });

        Assert.Null(command.GetArgument(999));
    }

    [Fact]
    public void GetString_NonExistentKey_ReturnsNull()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.Null(command.GetString("--nonexistent"));
    }

    [Fact]
    public void GetInt_NonNumericValue_ReturnsNull()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--count"] = "not-a-number",
            },
            Array.Empty<string>());

        Assert.Null(command.GetInt("--count"));
    }

    [Fact]
    public void Constructor_EmptyDictionary_DoesNotThrow()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.NotNull(command);
        Assert.Null(command.GetString("any"));
    }

    [Fact]
    public void Constructor_EmptyArguments_DoesNotThrow()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.NotNull(command);
        Assert.Null(command.GetArgument(0));
    }
}
