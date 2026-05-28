using System.Text;
using Bukit.Cli.Commands;
using Bukit.Config;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorMarkdownCheckerTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _configPath;
    private readonly string _contentDir;

    public DoctorMarkdownCheckerTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-md-check-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        _contentDir = Path.Combine(_rootDir, "content");
        Directory.CreateDirectory(_contentDir);

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

    private DoctorCommand.DoctorContext CreateContext()
    {
        var config = ConfigLoader.Load(_configPath);
        return new DoctorCommand.DoctorContext(_rootDir, config, Path.Combine(_rootDir, "layouts"), Array.Empty<string>());
    }

    [Fact]
    public void CheckMarkdownFrontMatter_ValidFrontMatter_NoOutput()
    {
        File.WriteAllText(Path.Combine(_contentDir, "valid.md"), """
            ---
            title: hello
            ---
            # Content
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownFrontMatter(ctx));

        Assert.Empty(output);
    }

    [Fact]
    public void CheckMarkdownFrontMatter_UnclosedFrontMatter_Warns()
    {
        File.WriteAllText(Path.Combine(_contentDir, "unclosed.md"), """
            ---
            title: hello
            # Content
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownFrontMatter(ctx));

        Assert.Contains("unclosed front matter", output);
    }

    [Fact]
    public void CheckMarkdownFrontMatter_EmptyFrontMatter_Warns()
    {
        File.WriteAllText(Path.Combine(_contentDir, "empty-fm.md"), """
            ---
            ---
            # Content
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownFrontMatter(ctx));

        Assert.Contains("empty front matter", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckMarkdownFrontMatter_NoFrontMatterAtAll_NoOutput()
    {
        File.WriteAllText(Path.Combine(_contentDir, "no-fm.md"), "# Just content");

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownFrontMatter(ctx));

        Assert.Empty(output);
    }

    [Fact]
    public void CheckMarkdownFrontMatter_InvalidYaml_Warns()
    {
        File.WriteAllText(Path.Combine(_contentDir, "bad-yaml.md"), """
            ---
             : invalid * yaml *
            ---
            # Content
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownFrontMatter(ctx));

        Assert.Contains("failed to parse YAML", output);
    }

    [Fact]
    public void CheckMarkdownSyntax_UnclosedCodeBlock_Warns()
    {
        File.WriteAllText(Path.Combine(_contentDir, "unclosed-code.md"), """
            ---
            title: test
            ---
            # Title

            ```
            code here
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownSyntax(ctx));

        Assert.Contains("unclosed code block", output);
    }

    [Fact]
    public void CheckMarkdownSyntax_ValidCodeBlock_NoOutput()
    {
        File.WriteAllText(Path.Combine(_contentDir, "valid-code.md"), """
            ---
            title: test
            ---
            # Title

            ```
            code here
            ```

            More content.
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownSyntax(ctx));

        Assert.Empty(output);
    }

    [Fact]
    public void CheckMarkdownSyntax_EmptyLink_Warns()
    {
        File.WriteAllText(Path.Combine(_contentDir, "empty-link.md"), """
            ---
            title: test
            ---
            # Title
            [click here]()
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownSyntax(ctx));

        Assert.Contains("empty link", output);
    }

    [Fact]
    public void CheckMarkdownSyntax_EmptyImageLink_Warns()
    {
        File.WriteAllText(Path.Combine(_contentDir, "empty-img.md"), """
            ---
            title: test
            ---
            # Title
            ![alt text]()
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownSyntax(ctx));

        Assert.Contains("empty image", output);
    }

    [Fact]
    public void CheckMarkdownEmptyBody_OnlyFrontMatter_Warns()
    {
        File.WriteAllText(Path.Combine(_contentDir, "fm-only.md"), """
            ---
            title: hello
            ---
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownEmptyBody(ctx));

        Assert.Contains("empty body", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckMarkdownEmptyBody_HasContent_NoOutput()
    {
        File.WriteAllText(Path.Combine(_contentDir, "has-body.md"), """
            ---
            title: hello
            ---
            # Content goes here
            """);

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownEmptyBody(ctx));

        Assert.Empty(output);
    }

    [Fact]
    public void CheckMarkdownEmptyBody_NoFrontMatterNoBody_Warns()
    {
        File.WriteAllText(Path.Combine(_contentDir, "empty.md"), "");

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() => DoctorMarkdownChecker.CheckMarkdownEmptyBody(ctx));

        Assert.Contains("empty body", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckMarkdown_All_NoMarkdownFiles_NoOutput()
    {
        if (Directory.Exists(_contentDir))
        {
            Directory.Delete(_contentDir, recursive: true);
        }

        var ctx = CreateContext();
        var output = CaptureConsoleOutput(() =>
        {
            DoctorMarkdownChecker.CheckMarkdownFrontMatter(ctx);
            DoctorMarkdownChecker.CheckMarkdownSyntax(ctx);
            DoctorMarkdownChecker.CheckMarkdownEmptyBody(ctx);
        });

        Assert.Empty(output);
    }

    private static string CaptureConsoleOutput(Action action)
    {
        var original = Console.Out;
        using var sw = new StringWriter(new StringBuilder());
        Console.SetOut(sw);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return sw.ToString();
    }
}
