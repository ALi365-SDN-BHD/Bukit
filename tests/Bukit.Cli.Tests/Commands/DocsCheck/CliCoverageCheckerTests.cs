using Bukit.Cli;
using Bukit.Cli.Commands.DocsCheck;
using Xunit;

namespace Bukit.Cli.Tests.Commands.DocsCheck;

public sealed class CliCoverageCheckerTests
{
    [Fact]
    public void Check_MarksDirectAndDotnetRunCommandsAsCovered()
    {
        using var temp = TempMarkdown(
            """
            ```bash
            bukit build --output dist
            dotnet run --project src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj -c Release -- config check --config site.yaml
            bukit import old-demo
            ```
            """);

        var issues = CliCoverageChecker.Check(
            Directory.GetCurrentDirectory(),
            [new DocFile(temp.Path, DocCategory.Guide)],
            BukitCliSpecs.CreateRegistry());

        Assert.DoesNotContain(issues, i => i.Message.Contains("'build'", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, i => i.Message.Contains("'config check'", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, i => i.Message.Contains("'import'", StringComparison.Ordinal));
    }

    private static TempFile TempMarkdown(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
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
