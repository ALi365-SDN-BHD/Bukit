using Bukit.Engine.Plugins.Protocol;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ProcessArgumentsBuilderTests
{
    [Fact]
    public void Build_NullOptions_ReturnsNull()
    {
        var result = ProcessArgumentsBuilder.Build(null);

        Assert.Null(result);
    }

    [Fact]
    public void Build_EmptyDict_ReturnsNull()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Null(result);
    }

    [Fact]
    public void Build_NoProcessArgsKey_ReturnsNull()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["other"] = "value"
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Null(result);
    }

    [Fact]
    public void Build_SimpleNamedValue_GeneratesFlagAndValue()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["output"] = "out.txt"
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("--output out.txt", result);
    }

    [Fact]
    public void Build_ValueWithSpaces_IsQuoted()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["message"] = "hello world"
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("--message \"hello world\"", result);
    }

    [Fact]
    public void Build_ValueWithQuotes_IsEscaped()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = "my \"cool\" title"
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("--title \"my \\\"cool\\\" title\"", result);
    }

    [Fact]
    public void Build_BooleanTrue_GeneratesFlagOnly()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["verbose"] = true
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("--verbose", result);
    }

    [Fact]
    public void Build_BooleanFalse_IsOmitted()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["verbose"] = false
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Null(result);
    }

    [Fact]
    public void Build_PositionalArgs_AppearBeforeNamedArgs()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["positionals"] = new List<object> { "input.txt", "output.txt" },
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["verbose"] = true
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("input.txt output.txt --verbose", result);
    }

    [Fact]
    public void Build_MixedNamedAndPositional_OrderedCorrectly()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["positionals"] = new List<object> { "source.txt" },
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mode"] = "full",
                    ["compress"] = true
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("source.txt --compress --mode full", result);
    }

    [Fact]
    public void Build_NullNamedValue_IsSkipped()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = default(object)!,
                    ["verbose"] = true
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("--verbose", result);
    }

    [Fact]
    public void Build_PositionalWithSpace_IsQuoted()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["positionals"] = new List<object> { "my file.txt" }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("\"my file.txt\"", result);
    }

    [Fact]
    public void Build_EmptyPositionalsAndEmptyNamed_ReturnsNull()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["positionals"] = new List<object>(),
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Null(result);
    }

    [Fact]
    public void Build_MultipleNamedArgs_OrderedAlphabetically()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["zeta"] = "z",
                    ["alpha"] = "a",
                    ["gamma"] = "g"
                }
            }
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Equal("--alpha a --gamma g --zeta z", result);
    }

    [Fact]
    public void Build_ProcessArgsIsNull_ReturnsNull()
    {
        var options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["processArgs"] = default(object)!
        };

        var result = ProcessArgumentsBuilder.Build(options);

        Assert.Null(result);
    }
}
