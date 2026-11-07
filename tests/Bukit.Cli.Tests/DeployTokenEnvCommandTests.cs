using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DeployTokenEnvCommandTests
{
    [Fact]
    public async Task RunAsync_InvalidTokenEnv_ReturnsOneWithoutEchoingPastedValue()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-token-env-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var config = Path.Combine(root, "site.yaml");
            File.WriteAllText(config, """
            site:
              name: test
              title: Test
              url: https://example.com
            content:
              sources:
                - type: markdown
                  name: page
                  markdown:
                    dir: content
            deploy:
              provider: github-pages
            build:
              output: dist
            """);

            const string pasted = "ghp_pastedtokenvalue123";
            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = config,
                    ["--token-env"] = pasted,
                    ["--dry-run"] = "true"
                },
                Array.Empty<string>()));

            Assert.Equal(1, exitCode);
            Assert.Contains("--token-env must be", writer.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(pasted, writer.ToString(), StringComparison.Ordinal);
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
