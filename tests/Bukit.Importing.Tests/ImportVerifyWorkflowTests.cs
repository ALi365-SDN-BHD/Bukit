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

    [Fact]
    public async Task VerifyAsync_ImportedSiteBuildsAndEachPageCarriesOnlyItsOwnAssets()
    {
        var demoDir = Path.Combine(_rootDir, "demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title><style>.home-only{color:red}</style></head>" +
            "<body><main><h1>Home</h1><p>Welcome.</p></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title><style>.about-only{color:blue}</style></head>" +
            "<body><main><h1>About</h1><p>About us.</p></main><script>window.aboutOnly=1;</script></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "pricing.html"),
            "<html><head><title>Pricing</title><style>.pricing-only{color:green}</style></head>" +
            "<body><main><h1>Pricing</h1><p>Plans.</p></main></body></html>");
        var routeMap = Path.Combine(demoDir, "routes.yaml");
        File.WriteAllText(routeMap, """
pages:
  - source: about.html
    route: /company/about/
    type: Page
    template: about-us
""");

        var import = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = "e2e",
            RootDir = _rootDir,
            Force = true,
            RouteMapPath = routeMap
        });

        var verify = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(import, _rootDir, "e2e"));
        Assert.True(verify.ExitCode == 0, verify.StdErr);

        var distDir = Path.Combine(_rootDir, "dist");
        string Output(string relative)
        {
            var path = Path.Combine(distDir, relative);
            Assert.True(File.Exists(path), $"Missing build output '{relative}'.");
            return File.ReadAllText(path);
        }

        var home = Output("index.html");
        Assert.Contains(".home-only", home, StringComparison.Ordinal);
        Assert.DoesNotContain(".about-only", home, StringComparison.Ordinal);

        var about = Output(Path.Combine("company", "about", "index.html"));
        Assert.Contains(".about-only", about, StringComparison.Ordinal);
        Assert.Contains("window.aboutOnly", about, StringComparison.Ordinal);
        Assert.DoesNotContain(".home-only", about, StringComparison.Ordinal);
        Assert.DoesNotContain(".pricing-only", about, StringComparison.Ordinal);

        var pricing = Output(Path.Combine("pricing", "index.html"));
        Assert.Contains(".pricing-only", pricing, StringComparison.Ordinal);
        Assert.DoesNotContain(".about-only", pricing, StringComparison.Ordinal);
    }
}
