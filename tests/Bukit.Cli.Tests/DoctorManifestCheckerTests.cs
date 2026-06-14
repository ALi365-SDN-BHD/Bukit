using Bukit.Cli.Commands;
using Bukit.Config;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorManifestCheckerTests : IDisposable
{
    private readonly string _layoutsDir;

    public DoctorManifestCheckerTests()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-manifest-checker-tests-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(rootDir, "layouts");
        Directory.CreateDirectory(_layoutsDir);
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "layouts"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "partials"));
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(Path.GetDirectoryName(_layoutsDir)!, recursive: true);
    }

    [Fact]
    public void CheckManifestCompleteness_WithoutManifest_Warns()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "home.html"), "<html></html>");
        var allHtmlFiles = Directory.GetFiles(_layoutsDir, "*.html", SearchOption.AllDirectories);

        var output = CaptureStdOut(() => DoctorManifestChecker.CheckManifestCompleteness(_layoutsDir, allHtmlFiles));

        Assert.Contains("No bukit.templates.yaml found", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckManifestCompleteness_WithMissingAndStaleEntries_ReportsBoth()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "home.html"), "<html></html>");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "post.html"), "<html></html>");
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), """
            templates:
              pages/home.html: {}
              pages/stale.html: {}
            """);

        var allHtmlFiles = Directory.GetFiles(_layoutsDir, "*.html", SearchOption.AllDirectories);

        var output = CaptureStdOut(() => DoctorManifestChecker.CheckManifestCompleteness(_layoutsDir, allHtmlFiles));

        Assert.Contains("template(s) not in bukit.templates.yaml", output, StringComparison.Ordinal);
        Assert.Contains("pages/post.html", output, StringComparison.Ordinal);
        Assert.Contains("stale declaration(s)", output, StringComparison.Ordinal);
        Assert.Contains("pages/stale.html", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckUnreferencedTemplates_ReportsUnusedTemplate()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "layouts", "base.html"), "{{ content }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "home.html"), "{% include \"partials/used.html\" %}");
        File.WriteAllText(Path.Combine(_layoutsDir, "partials", "used.html"), "<span>used</span>");
        File.WriteAllText(Path.Combine(_layoutsDir, "partials", "unused.html"), "<span>unused</span>");

        var allHtmlFiles = Directory.GetFiles(_layoutsDir, "*.html", SearchOption.AllDirectories);
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["page"] = new() { Permalink = "/pages/{slug}/", Template = "pages/home.html" }
                }
            },
            Content = new ContentConfig()
        };

        var output = CaptureStdOut(() => DoctorManifestChecker.CheckUnreferencedTemplates(
            _layoutsDir,
            allHtmlFiles,
            config,
            Array.Empty<RouteInfo>()));

        Assert.Contains("appear unreferenced by any route", output, StringComparison.Ordinal);
        Assert.Contains("partials/unused.html", output, StringComparison.Ordinal);
        Assert.DoesNotContain("partials/used.html", output, StringComparison.Ordinal);
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
