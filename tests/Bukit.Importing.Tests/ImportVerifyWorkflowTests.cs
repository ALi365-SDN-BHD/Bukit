using Xunit;

namespace Bukit.Importing.Tests;

[Collection("ImportingConsole")]
public sealed class ImportVerifyWorkflowTests : IDisposable
{
    private readonly string _rootDir;

    public ImportVerifyWorkflowTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-importing-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task VerifyAsync_UsesSitePathFallbackAndMapsMissingConfigToExitCodeOne()
    {
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(
                new ImportResult
                {
                    ThemePath = Path.Combine(_rootDir, "themes", "demo")
                },
                _rootDir,
                "demo"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(Path.Combine(_rootDir, "sites", "demo", "site.yaml"), result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_ConfigValidationFailureReturnsExitCodeOne()
    {
        var siteDir = Path.Combine(_rootDir, "custom-site");
        Directory.CreateDirectory(siteDir);
        File.WriteAllText(Path.Combine(siteDir, "site.yaml"), """
site:
  name: broken
content:
  sources:
    - type: markdown
      markdown:
        dir: content
""");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(
                new ImportResult
                {
                    ThemePath = Path.Combine(_rootDir, "themes", "demo"),
                    SitePath = siteDir
                },
                _rootDir,
                "demo"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("title is required.", result.StdErr, StringComparison.Ordinal);
    }
}
