using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class CoverageGateTests
{
    [Fact]
    public void CoreCoveragePartition_ExcludesExperimentalAssemblies()
    {
        var script = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "checks", "coverage.sh"));

        Assert.Contains("-bukit-labs", script, StringComparison.Ordinal);
        Assert.Contains("-Bukit.Importing", script, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "bukit.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir) ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
