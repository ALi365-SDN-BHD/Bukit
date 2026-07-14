using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Importing;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class ImportBuildListTitleIntegrationTests : IDisposable
{
    private readonly string _rootDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-import-build-list-title-" + Guid.NewGuid().ToString("N"));

    public ImportBuildListTitleIntegrationTests()
    {
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task ImportThenBuild_GeneratedListPageRendersListTitle()
    {
        var demoDir = Path.Combine(_rootDir, "demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1><p>About.</p></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "insights.html"),
            "<html><head><title>Insights</title></head><body><main><h1>Insights</h1><article class=\"article-card\"><h3>Guide</h3><p>Summary.</p></article></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "article-detail.html"),
            "<html><head><title>Guide</title></head><body><main><h1>Guide</h1><p>Body.</p></main></body></html>");

        var import = await ImportCommandWorkflow.RunAsync(new ImportCommandOptions
        {
            Subcommand = "html-demo",
            RootDir = _rootDir,
            WorkingDir = _rootDir,
            DemoDir = demoDir,
            ThemeName = "imported",
            SitePath = ".",
            ContentSource = "json",
            Force = true
        });

        Assert.Equal(0, import.ExitCode);
        var generatedTemplate = File.ReadAllText(Path.Combine(
            _rootDir,
            "themes",
            "imported",
            "layouts",
            "pages",
            "insights.html"));
        Assert.Contains("<h1>{{ page.title }}</h1>", generatedTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("{{ this.title }}", generatedTemplate, StringComparison.Ordinal);

        var buildCommand = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = Path.Combine(_rootDir, "site.yaml")
            },
            Array.Empty<string>());
        var (doctorExitCode, doctorOutput) = await CaptureStdOutAsync(
            () => DoctorCommand.RunAsync(buildCommand));
        Assert.Equal(0, doctorExitCode);
        Assert.Contains("Known-context template variable check", doctorOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("this.title", doctorOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("term.title", doctorOutput, StringComparison.Ordinal);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var exitCode = await BuildCommand.RunAsync(buildCommand, timeout.Token);

        Assert.Equal(0, exitCode);
        var html = File.ReadAllText(Path.Combine(_rootDir, "dist", "insights", "index.html"));
        Assert.Contains("<h1>Insights</h1>", html, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> CaptureStdOutAsync(Func<Task<int>> action)
    {
        using var writer = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(writer);
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
