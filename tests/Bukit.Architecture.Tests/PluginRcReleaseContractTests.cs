using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class PluginRcReleaseContractTests
{
    [Fact]
    public void ReleaseGate_BuildsAndSmokesImportAndNotionPackages()
    {
        string releaseGate = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "gates", "release.sh"));

        Assert.Contains("scripts/build/import-plugin-package.sh", releaseGate, StringComparison.Ordinal);
        Assert.Contains("scripts/smoke/import-plugin-package.sh", releaseGate, StringComparison.Ordinal);
        Assert.Contains("scripts/build/notion-plugin-package.sh", releaseGate, StringComparison.Ordinal);
        Assert.Contains("scripts/smoke/notion-plugin-package.sh", releaseGate, StringComparison.Ordinal);
        Assert.Contains("${artifact_dir}/plugin-packages/import", releaseGate, StringComparison.Ordinal);
        Assert.Contains("${artifact_dir}/plugin-packages/notion", releaseGate, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualAcceptanceScript_CoversRcHandoffAndConfirmedLivePush()
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "smoke", "import-notion-rc-manual.sh");

        Assert.True(File.Exists(scriptPath), $"Missing RC acceptance script: {scriptPath}");
        string script = File.ReadAllText(scriptPath);
        Assert.Contains("import html-demo", script, StringComparison.Ordinal);
        Assert.Contains("notion validate-seed", script, StringComparison.Ordinal);
        Assert.Contains("notion validate-database-map", script, StringComparison.Ordinal);
        Assert.Contains("--dry-run", script, StringComparison.Ordinal);
        Assert.Contains("--mode create", script, StringComparison.Ordinal);
        Assert.Contains("NOTION_TOKEN", script, StringComparison.Ordinal);
        Assert.Contains("NOTION_DATA_SOURCE_ID", script, StringComparison.Ordinal);
        Assert.Contains("BUKIT_NOTION_RC_CONFIRM", script, StringComparison.Ordinal);
        Assert.Contains(".bukit/tmp/notion/rc-manual-database-map.yaml", script, StringComparison.Ordinal);
        Assert.Contains("Only the generated pages collection is accepted", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RcReleaseNotesAndHandoffGuide_AreCommitted()
    {
        string root = FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(root, "docs", "release", "import-notion-plugins-1.0.0-rc.1-release-notes.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "plugins", "import-notion-handoff-usage.md")));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
