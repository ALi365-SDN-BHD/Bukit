using System.Text.RegularExpressions;
using Bukit.Cli.Commands.DocsCheck;
using Xunit;

namespace Bukit.Cli.Tests.Commands.DocsCheck;

public class FileRefCheckerTests
{
    // Access the private ShouldSkipReferencedPath via reflection or test via Check
    // Instead, we test the Check method with known patterns

    private static string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName() + ".md";
        File.WriteAllText(path, content);
        return path;
    }

    private static readonly string RepoRoot =
        new DirectoryInfo(Directory.GetCurrentDirectory()).Root.FullName;

    [Fact]
    public void Check_ShouldSkipGlobPatterns()
    {
        var docContent = """
            Some content with a glob pattern:
            `content/*.md` and `static/*.html` are glob examples.
            """;

        var docPath = CreateTempFile(docContent);
        try
        {
            var docFiles = new List<DocFile>
            {
                new(docPath, DocCategory.Guide)
            };

            var issues = FileRefChecker.Check(RepoRoot, docFiles);

            Assert.DoesNotContain(issues, i =>
                i.Message.Contains("content/*.md") || i.Message.Contains("static/*.html"));
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    [Fact]
    public void Check_ShouldSkipBuildOutputPaths()
    {
        var docContent = """
            Output path example: `blog/hello-world/index.html`
            Another one: `blog/index.html`
            """;

        var docPath = CreateTempFile(docContent);
        try
        {
            var docFiles = new List<DocFile>
            {
                new(docPath, DocCategory.Guide)
            };

            var issues = FileRefChecker.Check(RepoRoot, docFiles);

            Assert.DoesNotContain(issues, i =>
                i.Message.Contains("blog/hello-world/index.html") ||
                i.Message.Contains("blog/index.html"));
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    [Fact]
    public void Check_ShouldSkipAssetsPrefix()
    {
        var docContent = """
            Theme asset: `assets/style.css`
            Another asset: `assets/script.js`
            """;

        var docPath = CreateTempFile(docContent);
        try
        {
            var docFiles = new List<DocFile>
            {
                new(docPath, DocCategory.Guide)
            };

            var issues = FileRefChecker.Check(RepoRoot, docFiles);

            Assert.DoesNotContain(issues, i =>
                i.Message.Contains("assets/style.css") ||
                i.Message.Contains("assets/script.js"));
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    [Fact]
    public void Check_ShouldSkipStaticPrefix()
    {
        var docContent = """
            Static file: `static/about.html`
            Another: `static/docs/index.html`
            """;

        var docPath = CreateTempFile(docContent);
        try
        {
            var docFiles = new List<DocFile>
            {
                new(docPath, DocCategory.Guide)
            };

            var issues = FileRefChecker.Check(RepoRoot, docFiles);

            Assert.DoesNotContain(issues, i =>
                i.Message.Contains("static/about.html") ||
                i.Message.Contains("static/docs/index.html"));
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    [Fact]
    public void Check_ShouldReportMissingRealFileRefs()
    {
        var docContent = """
            Reference: `src/Bukit.Cli/Commands/NonExistentFile.cs`
            """;

        var docPath = CreateTempFile(docContent);
        try
        {
            var docFiles = new List<DocFile>
            {
                new(docPath, DocCategory.Guide)
            };

            var issues = FileRefChecker.Check(RepoRoot, docFiles);

            Assert.Contains(issues, i =>
                i.Message.Contains("NonExistentFile.cs") &&
                i.Severity == Severity.Error);
        }
        finally
        {
            File.Delete(docPath);
        }
    }
}
