using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportModelsTests
{
    [Fact]
    public void HtmlDemoImportOptions_Defaults()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = "/demo",
            ThemeName = "test",
            RootDir = "/root"
        };

        Assert.Equal("/demo", options.InputPath);
        Assert.Equal("test", options.ThemeName);
        Assert.Equal("/root", options.RootDir);
        Assert.False(options.Force);
        Assert.False(options.Use);
        Assert.False(options.Verify);
        Assert.Equal("zh", options.Language);
        Assert.True(options.PreserveHtml);
        Assert.True(options.GenerateReport);
    }

    [Fact]
    public void HtmlDemoImportOptions_AllFlags()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = "/demo",
            ThemeName = "test",
            RootDir = "/root",
            Force = true,
            Use = true,
            Verify = true,
            Language = "zh"
        };

        Assert.True(options.Force);
        Assert.True(options.Use);
        Assert.True(options.Verify);
        Assert.Equal("zh", options.Language);
    }

    [Fact]
    public void DiscoveredPage_PropertiesSet()
    {
        var page = new DiscoveredPage
        {
            FilePath = "/demo/index.html",
            RelativePath = "index.html",
            Slug = "",
            Type = PageType.Home,
            Title = "Home Page",
            FullHtml = "<html></html>",
            HeadContent = "<title>Home</title>",
            BodyContent = "<h1>Home</h1>",
            BodyOpening = "<header>Nav</header>",
            UniqueBody = "<main>Content</main>",
            BodyClosing = "<footer>End</footer>",
            AssetPaths = ["img/logo.png"]
        };

        Assert.Equal("/demo/index.html", page.FilePath);
        Assert.Equal("index.html", page.RelativePath);
        Assert.Equal("", page.Slug);
        Assert.Equal(PageType.Home, page.Type);
        Assert.Equal("Home Page", page.Title);
        Assert.Single(page.AssetPaths);
    }

    [Fact]
    public void PageType_AllValuesExist()
    {
        var values = Enum.GetValues<PageType>();
        Assert.Contains(PageType.Home, values);
        Assert.Contains(PageType.Page, values);
        Assert.Contains(PageType.PostList, values);
        Assert.Contains(PageType.PostDetail, values);
        Assert.Contains(PageType.CompanyList, values);
        Assert.Contains(PageType.CompanyDetail, values);
        Assert.Contains(PageType.ServiceList, values);
        Assert.Contains(PageType.ServiceDetail, values);
        Assert.Contains(PageType.Unknown, values);
    }

    [Fact]
    public void ImportResult_Defaults()
    {
        var result = new ImportResult
        {
            ThemePath = "/themes/test"
        };

        Assert.Equal("/themes/test", result.ThemePath);
        Assert.Equal(0, result.PagesFound);
        Assert.Equal(0, result.TemplatesGenerated);
        Assert.Equal(0, result.PartialsGenerated);
        Assert.Equal(0, result.AssetsCopied);
        Assert.False(result.SiteYamlCreated);
        Assert.False(result.TemplatesSynced);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ImportResult_WithWarnings()
    {
        var result = new ImportResult
        {
            ThemePath = "/themes/test",
            PagesFound = 5,
            TemplatesGenerated = 7,
            PartialsGenerated = 3,
            AssetsCopied = 10,
            SiteYamlCreated = true,
            TemplatesSynced = true,
            Warnings = ["warn1", "warn2"]
        };

        Assert.Equal(5, result.PagesFound);
        Assert.Equal(7, result.TemplatesGenerated);
        Assert.Equal(3, result.PartialsGenerated);
        Assert.Equal(10, result.AssetsCopied);
        Assert.True(result.SiteYamlCreated);
        Assert.True(result.TemplatesSynced);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void ImportDiagnostic_AllFields()
    {
        var diag = new ImportDiagnostic(
            ImportDiagnosticSeverity.Error,
            "TEST_CODE",
            "test message",
            "/test/file.html",
            42);

        Assert.Equal(ImportDiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("TEST_CODE", diag.Code);
        Assert.Equal("test message", diag.Message);
        Assert.Equal("/test/file.html", diag.FilePath);
        Assert.Equal(42, diag.LineNumber);
    }

    [Fact]
    public void ImportDiagnostic_OptionalFields()
    {
        var diag = new ImportDiagnostic(
            ImportDiagnosticSeverity.Warning,
            "CODE",
            "msg");

        Assert.Equal(ImportDiagnosticSeverity.Warning, diag.Severity);
        Assert.Null(diag.FilePath);
        Assert.Null(diag.LineNumber);
    }
}
