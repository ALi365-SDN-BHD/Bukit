using Bukit.Cli.Commands;
using Bukit.Config;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorTemplateCheckerTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _layoutsDir;

    public DoctorTemplateCheckerTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-template-checker-tests-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(_rootDir, "layouts");
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public void CheckIncludeExistence_ReportsMissingInclude()
    {
        WriteTemplate("pages/home.html", "{% include \"partials/missing.html\" %}");
        var context = CreateContext(CreateConfig());

        var output = CaptureStdOut(() => DoctorTemplateChecker.CheckIncludeExistence(context));

        Assert.Contains("include \"partials/missing.html\" not found", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckThemeParamsConsistency_ReportsUnusedAndUndeclaredParams()
    {
        WriteTemplate("pages/home.html", """
            <div>{{ site.theme.params.used }}</div>
            <div>{{ site.params.missing }}</div>
            """);
        var config = CreateConfig(
            theme: new ThemeConfig
            {
                Params = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["brand"] = "Bukit",
                    ["used"] = "yes"
                }
            });
        var context = CreateContext(config);

        var output = CaptureStdOut(() => DoctorTemplateChecker.CheckThemeParamsConsistency(context));

        Assert.Contains("theme param(s) declared but not used", output, StringComparison.Ordinal);
        Assert.Contains("brand", output, StringComparison.Ordinal);
        Assert.Contains("theme param(s) not declared in config", output, StringComparison.Ordinal);
        Assert.Contains("missing", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckHardcodedUrls_ReportsAbsoluteAndRootRelativeUrls()
    {
        WriteTemplate("pages/absolute.html", "<a href=\"https://example.com/path\">Example</a>");
        WriteTemplate("pages/root-relative.html", "<img src=\"/assets/app.css\" />");
        var context = CreateContext(CreateConfig());

        var output = CaptureStdOut(() => DoctorTemplateChecker.CheckHardcodedUrls(context));

        Assert.Contains("hardcoded URL(s) in templates", output, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.com/path\"", output, StringComparison.Ordinal);
        Assert.Contains("src=\"/assets/app.css\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckHardcodedText_ReportsCopyrightAndLongSnippet()
    {
        WriteTemplate("pages/copyright.html", "<footer>Copyright 2026 Bukit</footer>");
        WriteTemplate("pages/snippet.html", "<p>This is a very long literal marketing sentence for testing only.</p>");
        var context = CreateContext(CreateConfig());

        var output = CaptureStdOut(() => DoctorTemplateChecker.CheckHardcodedText(context));

        Assert.Contains("hardcoded text issue(s) in templates", output, StringComparison.Ordinal);
        Assert.Contains("Copyright 2026", output, StringComparison.Ordinal);
        Assert.Contains("hardcoded text snippet", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TextCleanupHelpers_RemoveCommentsBlocksAndTags()
    {
        var withComments = DoctorTemplateChecker.RemoveHtmlComments("before<!-- hidden -->after");
        var withoutScriban = DoctorTemplateChecker.RemoveScribanBlocks("{{ site.title }} {% include \"x\" %} plain");
        var withoutScript = DoctorTemplateChecker.RemoveTagContent("<script>alert(1)</script><p>ok</p>", "script");
        var plainText = DoctorTemplateChecker.ExtractHtmlText("<div>Hello&nbsp;<strong>world</strong></div>");

        Assert.Equal("before after", withComments);
        Assert.Equal("    plain", withoutScriban);
        Assert.Equal(" <p>ok</p>", withoutScript);
        Assert.Equal("Hello world", plainText);
    }

    private DoctorCommand.DoctorContext CreateContext(AppConfig config)
    {
        var allHtmlFiles = Directory.GetFiles(_layoutsDir, "*.html", SearchOption.AllDirectories);
        return new DoctorCommand.DoctorContext(_rootDir, config, _layoutsDir, allHtmlFiles);
    }

    private AppConfig CreateConfig(string? listTemplate = null, ThemeConfig? theme = null, TaxonomyConfig? taxonomyTemplates = null)
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = listTemplate is null
                    ? null
                    : new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["post"] = new()
                        {
                            Permalink = "/blog/{slug}/",
                            ListTemplate = listTemplate
                        }
                    }
            },
            Content = new ContentConfig(),
            Theme = theme ?? new ThemeConfig(),
            Taxonomy = taxonomyTemplates ?? new TaxonomyConfig()
        };
    }

    private void WriteTemplate(string relativePath, string content)
    {
        var fullPath = Path.Combine(_layoutsDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static string CaptureStdOut(Action action)
    {
        using var writer = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
