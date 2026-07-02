using Bukit.Cli;
using Bukit.Cli.Commands.DocsCheck;
using Xunit;

namespace Bukit.Cli.Tests.Commands.DocsCheck;

public sealed class ExampleParserCheckerTests
{
    [Fact]
    public void Check_AcceptsBukitAndDotnetRunExamples()
    {
        using var temp = TempReadme(
            """
            ```bash
            bukit build --output dist --jobs 2
            dotnet run --project src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj -- config check --config site.yaml
            bukit deploy --message "ship site" --dry-run --skip-build
            ```
            """);

        var issues = ExampleParserChecker.Check(
            [new DocFile(temp.Path, DocCategory.Readme)],
            BukitCliSpecs.CreateRegistry());

        Assert.Empty(issues);
    }

    [Fact]
    public void Check_ReportsUnknownCommandAndInvalidOptions()
    {
        using var temp = TempReadme(
            """
            ```bash
            bukit import legacy-demo
            bukit build --jobs nope
            ```
            """);

        var issues = ExampleParserChecker.Check(
            [new DocFile(temp.Path, DocCategory.Readme)],
            BukitCliSpecs.CreateRegistry());

        Assert.Contains(issues, i =>
            i.Severity == Severity.Error &&
            i.Message.Contains("Unknown command", StringComparison.Ordinal));
        Assert.Contains(issues, i =>
            i.Severity == Severity.Error &&
            i.Message.Contains("--jobs", StringComparison.Ordinal));
    }

    private static TempFile TempReadme(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"README-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return new TempFile(path);
    }

    private sealed class TempFile : IDisposable
    {
        public TempFile(string path) => Path = path;

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
