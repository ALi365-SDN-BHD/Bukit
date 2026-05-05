using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ThemeCommandTests
{
    [Fact]
    public async Task RunAsync_ReturnsTwo_WhenThemeNameMissing()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[] { "theme", "use" }));
        Assert.Equal(2, exitCode);
    }
}
