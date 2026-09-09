using Bukit.Cli.Commands;
using Bukit.Cli.Deploy;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DeployReadinessTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("{", false)]
    [InlineData("[]", false)]
    [InlineData("{\"version\":2,\"status\":\"completed\"}", false)]
    [InlineData("{\"version\":1,\"status\":\"started\"}", false)]
    [InlineData("{\"version\":1,\"status\":42}", false)]
    [InlineData("{\"version\":1,\"status\":\"completed\"}", true)]
    public async Task SkipBuildRequiresSupportedCompletedState(string? state, bool ready)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-readiness-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "dist");
        Directory.CreateDirectory(output);
        try
        {
            File.WriteAllText(Path.Combine(output, "index.html"), "hello");
            if (state is not null) File.WriteAllText(Path.Combine(output, ".bukit-build-state.json"), state);
            Assert.Equal(ready, DeploymentReadinessValidator.Validate(output) is null);
            File.WriteAllText(Path.Combine(root, "site.yaml"), """
                site:
                  name: test
                  title: Test
                content:
                  sources:
                    - type: markdown
                      name: page
                      collection: page
                      markdown:
                        dir: content
                """);
            var previousToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            var previousError = Console.Error;
            using var errors = new StringWriter();
            try
            {
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
                Console.SetError(errors);
                foreach (var dryRun in ready ? new[] { true } : new[] { true, false })
                {
                    errors.GetStringBuilder().Clear();
                    var result = await DeployCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>
                    {
                        ["--config"] = Path.Combine(root, "site.yaml"),
                        ["--output"] = "dist",
                        ["--skip-build"] = "true",
                        ["--dry-run"] = dryRun ? "true" : "false"
                    }, []));
                    Assert.Equal(ready ? 0 : 1, result);
                    if (!ready) Assert.Contains("Rebuild", errors.ToString());
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", previousToken);
                Console.SetError(previousError);
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
