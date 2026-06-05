using System.Text;
using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
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
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
                                                                           name: test
                                                                           templates:
                                                                             home:
                                                                               template: pages/index.html
                                                                               required: true
                                                                             page:
                                                                               template: pages/page.html
                                                                               accepts:
                                                                                 type: page
                                                                             post:
                                                                               template: pages/post.html
                                                                               accepts:
                                                                                 type: post
                                                                             list:
                                                                               template: pages/list.html
                                                                               accepts:
                                                                                 kind: list
                                                                           """);

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
    public async Task RunAsync_UsesPagesIndexHtml_WhenHomeTemplateIsNotDeclared()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), "name: test\n");
        WriteConfigWithExplicitCollectionTemplates();

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Doctor passed", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenDefaultHomePagesIndexHtmlIsMissing()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), "name: test\n");
        WriteConfigWithExplicitCollectionTemplates();
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "index.html"), "legacy root index");
        File.Delete(Path.Combine(_rootDir, "layouts", "pages", "index.html"));

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Missing templates", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pages/index.html", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenHomeRequiredFalse()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
                                                                           name: test
                                                                           templates:
                                                                             home:
                                                                               template: pages/index.html
                                                                               required: false
                                                                           """);
        WriteConfigWithExplicitCollectionTemplates();

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Theme template config error", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("home.required", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenPluginTemplateRequirementHasNoThemeMatch()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
                                                                           name: test
                                                                           templates:
                                                                             home:
                                                                               template: pages/index.html
                                                                               required: true
                                                                             post:
                                                                               template: pages/post.html
                                                                               accepts:
                                                                                 type: post
                                                                           """);
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         collections:
                                           post:
                                             permalink: /blog/{slug}/
                                             template: pages/post.html
                                             listRoute: /blog/
                                             listTemplate: pages/list.html
                                             pagination:
                                               enabled: true
                                               pageSize: 2
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);
        for (var i = 1; i <= 3; i++)
        {
            File.WriteAllText(Path.Combine(_rootDir, "content", $"post-{i}.md"), $"""
                ---
                type: post
                collection: post
                title: Post {i}
                slug: post-{i}
                ---
                # Post {i}
                """);
        }

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Plugin template requirement error", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pagination", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ChecksExternalPluginTemplateRequirementsFromConfig()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
                                                                           name: test
                                                                           templates:
                                                                             home:
                                                                               template: pages/index.html
                                                                               required: true
                                                                             widget:
                                                                               template: pages/missing-widget.html
                                                                               accepts:
                                                                                 kind: widget
                                                                           """);
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         externalPluginPolicy: allow
                                         externalPlugins:
                                           sample:
                                             runtime: process
                                             entry: plugins/sample
                                             hooks:
                                               - after-build
                                             templateRequirements:
                                               - widget
                                         collections:
                                           post:
                                             permalink: /blog/{slug}/
                                             template: pages/post.html
                                             listRoute: /blog/
                                             listTemplate: pages/list.html
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Missing used templates", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pages/missing-widget.html", output, StringComparison.OrdinalIgnoreCase);
    }

    private void WriteConfigWithExplicitCollectionTemplates()
    {
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         collections:
                                           post:
                                             permalink: /blog/{slug}/
                                             template: pages/post.html
                                             listRoute: /blog/
                                             listTemplate: pages/list.html
                                           page:
                                             permalink: /pages/{slug}/
                                             template: pages/page.html
                                             listRoute: /pages/
                                             listTemplate: pages/list.html
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);
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
            var exitCode = await DoctorCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = _configPath
                },
                Array.Empty<string>()));

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
            var exitCode = await DoctorCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = _configPath
                },
                Array.Empty<string>()));

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
            var exitCode = await DoctorCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = _configPath
                },
                Array.Empty<string>()));

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
            var exitCode = await DoctorCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = _configPath
                },
                Array.Empty<string>()));

            Assert.Equal(0, exitCode);
            Assert.DoesNotContain("heuristic fallback", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_Passes_WhenCollectionsNotConfiguredAndNoContentUsesOnlyHome()
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

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Doctor passed", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migration required", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Passes_WhenCollectionsNotConfiguredButContentHasFullRouteTemplate()
    {
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);
        File.WriteAllText(Path.Combine(_rootDir, "content", "about.md"), """
            ---
            title: About
            slug: about
            route:
              url: /about/
              outputPath: about/index.html
              template: pages/page.html
            ---
            # About
            """);

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Doctor passed", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migration required", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenRoutesConflict()
    {
        File.WriteAllText(Path.Combine(_rootDir, "content", "one.md"), """
            ---
            type: post
            collection: post
            title: One
            slug: same
            ---
            # One
            """);
        File.WriteAllText(Path.Combine(_rootDir, "content", "two.md"), """
            ---
            type: post
            collection: post
            title: Two
            slug: same
            ---
            # Two
            """);

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await DoctorCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = _configPath
                },
                Array.Empty<string>()));

            Assert.Equal(1, exitCode);
            var output = writer.ToString();
            Assert.Contains("Route inventory error", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Route conflict on url", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/blog/same", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_Passes_WhenUnusedTemplateKeyIsNotDeclared()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
                                                                           name: test
                                                                           templates:
                                                                             home:
                                                                               template: pages/index.html
                                                                               required: true
                                                                             page:
                                                                               template: pages/page.html
                                                                               accepts:
                                                                                 type: page
                                                                             list:
                                                                               template: pages/list.html
                                                                               accepts:
                                                                                 kind: list
                                                                           """);
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         collections:
                                           page:
                                             permalink: /pages/{slug}/
                                             listRoute: /pages/
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);
        File.WriteAllText(Path.Combine(_rootDir, "content", "about.md"), """
            ---
            type: page
            collection: page
            title: About
            slug: about
            ---
            # About
            """);

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Doctor passed", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No theme template matches", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenContentHasNoMatchingThemeTemplate()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
                                                                           name: test
                                                                           templates:
                                                                             home:
                                                                               template: pages/index.html
                                                                               required: true
                                                                             page:
                                                                               template: pages/page.html
                                                                               accepts:
                                                                                 type: page
                                                                             list:
                                                                               template: pages/list.html
                                                                               accepts:
                                                                                 kind: list
                                                                           """);
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         collections:
                                           post:
                                             permalink: /blog/{slug}/
                                             listRoute: /blog/
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);
        File.WriteAllText(Path.Combine(_rootDir, "content", "one.md"), """
            ---
            type: post
            collection: post
            title: One
            slug: one
            ---
            # One
            """);

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("No theme template matches content item", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Passes_WhenOptionalUnreferencedTemplateIsMissing()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
                                                                           name: test
                                                                           templates:
                                                                             home:
                                                                               template: pages/index.html
                                                                               required: true
                                                                             page:
                                                                               template: pages/page.html
                                                                               accepts:
                                                                                 type: page
                                                                             article:
                                                                               template: pages/missing-article.html
                                                                               accepts:
                                                                                 type: article
                                                                             list:
                                                                               template: pages/list.html
                                                                               accepts:
                                                                                 kind: list
                                                                           """);
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         collections:
                                           page:
                                             permalink: /pages/{slug}/
                                             listRoute: /pages/
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);
        File.WriteAllText(Path.Combine(_rootDir, "content", "about.md"), """
            ---
            type: page
            collection: page
            title: About
            slug: about
            ---
            # About
            """);

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("Doctor passed", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missing-article.html", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsError_WhenMatchedOptionalTemplateFileIsMissing()
    {
        File.WriteAllText(Path.Combine(_rootDir, "layouts", "theme.yaml"), """
                                                                           name: test
                                                                           templates:
                                                                             home:
                                                                               template: pages/index.html
                                                                               required: true
                                                                             post:
                                                                               template: pages/missing-post.html
                                                                               accepts:
                                                                                 type: post
                                                                             list:
                                                                               template: pages/list.html
                                                                               accepts:
                                                                                 kind: list
                                                                           """);
        File.WriteAllText(_configPath, """
                                       site:
                                         name: test
                                         title: Test
                                         collections:
                                           post:
                                             permalink: /blog/{slug}/
                                             listRoute: /blog/
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       build:
                                         listPageContentMode: auto
                                       """);
        File.WriteAllText(Path.Combine(_rootDir, "content", "one.md"), """
            ---
            type: post
            collection: post
            title: One
            slug: one
            ---
            # One
            """);

        var (exitCode, output) = await RunDoctorAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("Missing used templates", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pages/missing-post.html", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitGeneratedMarkdownSite_ContainsCollections_AndPassesDoctor()
    {
        var initRootDir = Path.Combine(Path.GetTempPath(), "bukit-init-doctor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(initRootDir);

        try
        {
            var siteDir = Path.Combine(initRootDir, "site");
            var initExitCode = await InitCommand.RunAsync(CliTestHelper.CreateCommand("init", new[] { "init", siteDir }));
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
                var doctorExitCode = await DoctorCommand.RunAsync(new CliBoundCommand(
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["--config"] = generatedConfigPath
                    },
                    Array.Empty<string>()));

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

    private async Task<(int ExitCode, string Output)> RunDoctorAsync()
    {
        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var exitCode = await DoctorCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = _configPath
                },
                Array.Empty<string>()));

            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
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
            var initExitCode = await InitCommand.RunAsync(CliTestHelper.CreateCommand("init", new[] { "init", siteDir, "--provider", "notion" }));
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
