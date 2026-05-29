using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class VersionCommandTests
{
    [Fact]
    public async Task RunAsync_ReturnsZero()
    {
        var exitCode = await Bukit.Cli.Commands.VersionCommand.RunAsync(CliTestHelper.CreateCommand("version", Array.Empty<string>()));
        Assert.Equal(0, exitCode);
    }
}
