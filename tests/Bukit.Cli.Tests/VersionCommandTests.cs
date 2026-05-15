using Bukit.Cli;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class VersionCommandTests
{
    [Fact]
    public async Task RunAsync_ReturnsZero()
    {
        var reader = new ArgReader(Array.Empty<string>());
        var exitCode = await Bukit.Cli.Commands.VersionCommand.RunAsync(reader);
        Assert.Equal(0, exitCode);
    }
}
