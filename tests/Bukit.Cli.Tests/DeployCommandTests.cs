using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DeployCommandTests
{
    [Fact]
    public async Task RunAsync_DryRunSkipBuild_ReturnsZero()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
            site:
              name: test
              title: Test
              url: https://example.com
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            deploy:
              provider: github-pages
            """);

            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--output"] = "dist",
                    ["--branch"] = "pages",
                    ["--message"] = "deploy test"
                },
                Array.Empty<string>()));

            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("--dry-run", output, StringComparison.Ordinal);
            Assert.Contains("branch: pages", output, StringComparison.Ordinal);
            Assert.Contains("message: deploy test", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
