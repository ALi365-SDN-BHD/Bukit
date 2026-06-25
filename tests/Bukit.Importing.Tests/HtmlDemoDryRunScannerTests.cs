using Bukit.Importing.HtmlDemo;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class HtmlDemoDryRunScannerTests : IDisposable
{
    private readonly string _projectRoot;

    public HtmlDemoDryRunScannerTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-html-demo-dry-run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_projectRoot, recursive: true);
    }

    [Fact]
    public void Scan_WhenDirectoryMissing_ReturnsFailureDiagnostic()
    {
        HtmlDemoDryRunScanResult result = HtmlDemoDryRunScanner.Scan(new HtmlDemoDryRunOptions(
            ProjectRoot: _projectRoot,
            DemoDirectory: Path.Combine(_projectRoot, "missing")));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.htmlDemoDirNotFound");
        Assert.Empty(result.Pages);
    }

    [Fact]
    public void Scan_WhenDirectoryEmpty_ReturnsNoHtmlFailure()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;

        HtmlDemoDryRunScanResult result = HtmlDemoDryRunScanner.Scan(new HtmlDemoDryRunOptions(
            ProjectRoot: _projectRoot,
            DemoDirectory: demoDir));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.htmlDemoNoHtmlFiles");
    }

    [Fact]
    public void Scan_SingleIndex_ReturnsPageAssetsAndLinks()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        Directory.CreateDirectory(Path.Combine(demoDir, "assets"));
        File.WriteAllText(Path.Combine(demoDir, "assets", "site.css"), "body{}");
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html>
          <head>
            <title>Home</title>
            <link href="assets/site.css" rel="stylesheet">
          </head>
          <body>
            <a href="about.html">About</a>
            <main><h1>Home</h1></main>
          </body>
        </html>
        """);

        HtmlDemoDryRunScanResult result = HtmlDemoDryRunScanner.Scan(new HtmlDemoDryRunOptions(
            ProjectRoot: _projectRoot,
            DemoDirectory: demoDir));

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        HtmlDemoPageCandidate page = Assert.Single(result.Pages);
        Assert.Equal("demo/index.html", page.Source);
        Assert.Equal("", page.Slug);
        Assert.Equal("Home", page.Title);
        Assert.Contains(result.Assets, asset => asset.Path == "demo/assets/site.css" && asset.Exists);
        Assert.Contains(result.Links, link => link.Source == "demo/index.html" && link.Target == "about.html");
    }

    [Fact]
    public void Scan_MultiplePagesWithoutIndex_ReturnsWarning()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "about.html"), "<html><head><title>About</title></head><body>About</body></html>");
        File.WriteAllText(Path.Combine(demoDir, "contact.html"), "<html><head><title>Contact</title></head><body>Contact</body></html>");

        HtmlDemoDryRunScanResult result = HtmlDemoDryRunScanner.Scan(new HtmlDemoDryRunOptions(
            ProjectRoot: _projectRoot,
            DemoDirectory: demoDir));

        Assert.True(result.Success);
        Assert.Equal(2, result.Pages.Count);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "import.htmlDemoMissingIndex" && diagnostic.Severity == "warning");
    }

    [Fact]
    public void Scan_BrokenAssetReference_ReturnsWarningDiagnostic()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
        <html><head><title>Home</title></head><body><img src="assets/missing.png"></body></html>
        """);

        HtmlDemoDryRunScanResult result = HtmlDemoDryRunScanner.Scan(new HtmlDemoDryRunOptions(
            ProjectRoot: _projectRoot,
            DemoDirectory: demoDir));

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "import.htmlDemoAssetMissing"
            && diagnostic.Severity == "warning"
            && diagnostic.Path == "demo/assets/missing.png");
    }

    [Fact]
    public void Scan_WithRouteMap_AppliesMappedSlugAndPageType()
    {
        string demoDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "demo")).FullName;
        File.WriteAllText(Path.Combine(demoDir, "legacy.html"), """
        <html><head><title>Legacy</title></head><body><main>Legacy</main></body></html>
        """);
        string routeMapPath = Path.Combine(_projectRoot, "routes.yaml");
        File.WriteAllText(routeMapPath, """
        pages:
          - source: legacy.html
            route: /mapped-route/
            type: CompanyList
            template: mapped-companies
        """);

        HtmlDemoDryRunScanResult result = HtmlDemoDryRunScanner.Scan(new HtmlDemoDryRunOptions(
            ProjectRoot: _projectRoot,
            DemoDirectory: demoDir,
            RouteMapPath: routeMapPath));

        HtmlDemoPageCandidate page = Assert.Single(result.Pages);
        Assert.Equal("mapped-route", page.Slug);
        Assert.Equal("CompanyList", page.Type);
    }
}
