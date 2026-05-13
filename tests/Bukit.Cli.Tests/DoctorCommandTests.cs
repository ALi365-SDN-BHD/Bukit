using System.Text;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorCommandTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;

    public DoctorCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-doctor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        Directory.CreateDirectory(Path.Combine(_rootDir, "content"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "layouts", "pages"));

        File.WriteAllText(Path.Combine(_rootDir, "layouts", "layouts", "base.html"), "{{ content }}");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "page.html"), "{{ page.title }}");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "post.html"), "{{ page.title }}");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "index.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "list.html"), "{{ for p in pages }}{{ p.title }}{{ end }}");

        _configPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         collections:
                                           post:
                                             permalink: /blog/{slug}/
                                             template: pages/post.html
                                             listRoute: /blog/
                                           page:
                                             permalink: /pages/{slug}/
                                             template: pages/page.html
                                             listRoute: /pages/
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenTemplateCapabilitiesYamlIsInvalid()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "bukit.templates.yaml"), "templates: [");

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await DoctorCommand.RunAsync(new ArgReader(new[] { "--config", _configPath }));

            Assert.Equal(1, exitCode);
            Assert.Contains("bukit.templates.yaml", writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenTemplateCapabilitiesReferencesMissingTemplate()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "bukit.templates.yaml"), """
                                                                                       templates:
                                                                                         pages/missing.html:
                                                                                           capabilities:
                                                                                             needs_page_content: true
                                                                                       """);

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await DoctorCommand.RunAsync(new ArgReader(new[] { "--config", _configPath }));

            Assert.Equal(1, exitCode);
            Assert.Contains("pages/missing.html", writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_Warns_WhenListTemplatesRelyOnHeuristicFallback()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "index.html"), "{{ partial_path = \"partials/card.html\" }}{{ for p in pages }}{{ include partial_path }}{{ end }}");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "list.html"), "{{ partial_path = \"partials/card.html\" }}{{ for p in pages }}{{ include partial_path }}{{ end }}");

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await DoctorCommand.RunAsync(new ArgReader(new[] { "--config", _configPath }));

            Assert.Equal(0, exitCode);
            Assert.Contains("heuristic fallback", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_DoesNotWarn_WhenIncludeTreeDoesNotUseContent()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "layouts", "partials"));
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "partials", "card.html"), "{{ p.title }}");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "index.html"), "{{ for p in pages }}{{ include \"partials/card.html\" }}{{ end }}");
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "pages", "list.html"), "{{ for p in pages }}{{ include \"partials/card.html\" }}{{ end }}");

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await DoctorCommand.RunAsync(new ArgReader(new[] { "--config", _configPath }));

            Assert.Equal(0, exitCode);
            Assert.DoesNotContain("heuristic fallback", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenCollectionsNotConfigured()
    {
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       """);

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await DoctorCommand.RunAsync(new ArgReader(new[] { "--config", _configPath }));

            Assert.Equal(1, exitCode);
            var output = writer.ToString();
            Assert.Contains("Migration required", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("collection", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("兼容层", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task InitGeneratedMarkdownSite_ContainsCollections_AndPassesDoctor()
    {
        var initRootDir = Path.Combine(Path.GetTempPath(), "bukit-init-doctor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(initRootDir);

        try
        {
            var siteDir = Path.Combine(initRootDir, "site");
            var initExitCode = await InitCommand.RunAsync(new ArgReader(new[] { "init", siteDir }));
            Assert.Equal(0, initExitCode);

            var generatedConfigPath = Path.Combine(siteDir, "site.yaml");
            var yaml = await File.ReadAllTextAsync(generatedConfigPath);
            Assert.Contains("collections:", yaml, StringComparison.Ordinal);
            Assert.Contains("theme:", yaml, StringComparison.Ordinal);
            Assert.Contains("brand: My Site", yaml, StringComparison.Ordinal);

            var themeRoot = Path.Combine(siteDir, "themes", "starter");
            Assert.True(File.Exists(Path.Combine(themeRoot, "assets", "style.css")));
            Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "bukit.templates.yaml")));
            Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "list-card.html")));
            Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "pagination-nav.html")));
            Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "search.html")));
            Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "taxonomy-index.html")));
            Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "pages", "taxonomy-term.html")));

            var footer = await File.ReadAllTextAsync(Path.Combine(themeRoot, "layouts", "partials", "footer.html"));
            Assert.Contains("Powered by", footer, StringComparison.Ordinal);
            Assert.Contains("github.com/ALi365-SDN-BHD/Bukit", footer, StringComparison.Ordinal);

            using var writer = new StringWriter(new StringBuilder());
            var originalOut = Console.Out;
            Console.SetOut(writer);
            try
            {
                var doctorExitCode = await DoctorCommand.RunAsync(new ArgReader(new[] { "--config", generatedConfigPath }));

                Assert.Equal(0, doctorExitCode);
                Assert.Contains("Doctor passed", writer.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        finally
        {
            if (Directory.Exists(initRootDir))
            {
                Directory.Delete(initRootDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InitGeneratedNotionSite_ContainsCollections_AndPassesDoctor()
    {
        var initRootDir = Path.Combine(Path.GetTempPath(), "bukit-init-notion-doctor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(initRootDir);

        try
        {
            var siteDir = Path.Combine(initRootDir, "site");
            var initExitCode = await InitCommand.RunAsync(new ArgReader(new[] { "init", siteDir, "--provider", "notion" }));
            Assert.Equal(0, initExitCode);

            var generatedConfigPath = Path.Combine(siteDir, "site.yaml");
            var yaml = await File.ReadAllTextAsync(generatedConfigPath);
            Assert.Contains("collections:", yaml, StringComparison.Ordinal);
            Assert.Contains("post:", yaml, StringComparison.Ordinal);
            Assert.Contains("page:", yaml, StringComparison.Ordinal);
            Assert.Contains("/blog/{slug}/", yaml, StringComparison.Ordinal);
            Assert.Contains("/pages/{slug}/", yaml, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(initRootDir))
            {
                Directory.Delete(initRootDir, recursive: true);
            }
        }
    }
}
