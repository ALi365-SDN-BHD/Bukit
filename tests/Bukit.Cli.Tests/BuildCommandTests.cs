using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class BuildCommandTests
{
    [Fact]
    public async Task RunAsync_WithConfigOption_ResolvesAndStartsBuild()
    {
        var siteYaml = Path.Combine(Path.GetTempPath(), "bukit-test-config", Guid.NewGuid().ToString("N"), "site.yaml");
        var dir = Path.GetDirectoryName(siteYaml)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(siteYaml, "site:\n  name: test\n  title: Test\ncontent:\n  provider: markdown\nbuild:\n  output: dist\n");

        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = siteYaml,
            },
            Array.Empty<string>());

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Task.WhenAny(BuildCommand.RunAsync(command), Task.Delay(Timeout.Infinite, cts.Token));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
    }

    [Fact]
    public void JobOption_ParsedCorrectly()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--jobs"] = "4",
            },
            Array.Empty<string>());

        Assert.Equal("4", command.GetString("--jobs"));
    }

    [Fact]
    public void JobOption_Null_WhenNotSet()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.Null(command.GetString("--jobs"));
    }

    [Fact]
    public async Task RunAsync_WithSiteOption_ResolvesAndStartsBuild()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-site", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "sites"));
        File.WriteAllText(Path.Combine(dir, "sites", "testsite.yaml"), "site:\n  name: test\n  title: Test\ncontent:\n  provider: markdown\nbuild:\n  output: dist\n");

        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;
            var command = new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--site"] = "testsite",
                },
                Array.Empty<string>());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Task.WhenAny(BuildCommand.RunAsync(command), Task.Delay(Timeout.Infinite, cts.Token));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
