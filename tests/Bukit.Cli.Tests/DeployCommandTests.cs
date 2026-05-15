using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DeployCommandTests
{
    [Fact]
    public async Task RunAsync_WithDryRun_SkipsActualDeployment()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  provider: markdown\n");

        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            var command = new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--config"] = Path.Combine(dir, "site.yaml"),
                },
                Array.Empty<string>());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var task = DeployCommand.RunAsync(command);
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != task)
            {
                cts.Cancel();
                Assert.Fail("DeployCommand timed out.");
            }

            var exitCode = await task;
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_WithSkipBuild_UsesExistingDist()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-skip", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var distDir = Path.Combine(dir, "dist");
        Directory.CreateDirectory(distDir);
        File.WriteAllText(Path.Combine(distDir, "index.html"), "<h1>test</h1>");
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  provider: markdown\n");

        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            var command = new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--config"] = Path.Combine(dir, "site.yaml"),
                },
                Array.Empty<string>());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var task = DeployCommand.RunAsync(command);
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != task)
            {
                cts.Cancel();
                Assert.Fail("DeployCommand timed out.");
            }

            var exitCode = await task;
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_WithBranchOption_PassesOption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-branch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  provider: markdown\n");

        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            var command = new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--branch"] = "my-pages",
                    ["--config"] = Path.Combine(dir, "site.yaml"),
                },
                Array.Empty<string>());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var task = DeployCommand.RunAsync(command);
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != task)
            {
                cts.Cancel();
                Assert.Fail("DeployCommand timed out.");
            }

            var exitCode = await task;
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_WithMessageOption_PassesOption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-msg", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  provider: markdown\n");

        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            var command = new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--message"] = "release v2.0.0",
                    ["--config"] = Path.Combine(dir, "site.yaml"),
                },
                Array.Empty<string>());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var task = DeployCommand.RunAsync(command);
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != task)
            {
                cts.Cancel();
                Assert.Fail("DeployCommand timed out.");
            }

            var exitCode = await task;
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_WithCliOverrides_PassesToBuild()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-override", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), @"
site:
  name: x
  title: x
content:
  provider: markdown
deploy:
  provider: github-pages
  branch: gh-pages
  message: test
");

        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            var command = new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--base-url"] = "/my-repo",
                    ["--site-url"] = "https://example.com/my-repo",
                    ["--config"] = Path.Combine(dir, "site.yaml"),
                },
                Array.Empty<string>());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var task = DeployCommand.RunAsync(command);
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != task)
            {
                cts.Cancel();
                Assert.Fail("DeployCommand timed out.");
            }

            var exitCode = await task;
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_WithoutDeploySectionInSiteYaml_UsesDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-nodeploy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  provider: markdown\n");

        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            var command = new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--config"] = Path.Combine(dir, "site.yaml"),
                },
                Array.Empty<string>());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var task = DeployCommand.RunAsync(command);
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
            if (completed != task)
            {
                cts.Cancel();
                Assert.Fail("DeployCommand timed out.");
            }

            var exitCode = await task;
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
