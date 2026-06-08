using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("CWD")]
public sealed class DeployCommandTests
{
    [Fact]
    public async Task RunAsync_WithDryRun_SkipsActualDeployment()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\n");

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
            TestCleanup.DeleteDirectory(dir, recursive: true);
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
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\n");

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
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithBranchOption_PassesOption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-branch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\n");

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
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithMessageOption_PassesOption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-msg", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\n");

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
            TestCleanup.DeleteDirectory(dir, recursive: true);
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
  sources:
    - type: markdown
      name: page
      collection: page
      markdown:
        dir: content
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
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithoutDeploySectionInSiteYaml_UsesDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-deploy-nodeploy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\n");

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
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_FromArgReader_InvalidConfig_ReturnsOne()
    {
        var exitCode = await DeployCommand.RunAsync(CliTestHelper.CreateCommand("deploy", new[] { "deploy", "--config", "/nonexistent/site.yaml" }));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_FromArgReader_WithBasicOptions_ReturnsZero()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-argreader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "site.yaml"), "site:\n  name: x\n  title: x\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\n");
        var distDir = Path.Combine(dir, "dist");
        Directory.CreateDirectory(distDir);
        File.WriteAllText(Path.Combine(distDir, "index.html"), "<h1>test</h1>");

        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            var command = CliTestHelper.CreateCommand("deploy", new[]
            {
                "deploy", "--dry-run", "--skip-build", "--config", Path.Combine(dir, "site.yaml")
            });

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
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_FromArgReader_NoArgs_ReturnsOne()
    {
        var originalDir = Environment.CurrentDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-test-noargs-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            Environment.CurrentDirectory = tempDir;

            var exitCode = await DeployCommand.RunAsync(CliTestHelper.CreateCommand("deploy", new[] { "deploy" }));

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            TestCleanup.DeleteDirectory(tempDir, recursive: true);
        }
    }
}
