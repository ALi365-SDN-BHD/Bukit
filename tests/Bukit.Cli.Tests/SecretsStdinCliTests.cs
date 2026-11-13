using System.Diagnostics;
using System.Text;
using Bukit.Cli.Commands;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

// --secrets-stdin: option handling in-process, plus end-to-end runs of the real CLI entry point in a
// child process with a fake token on stdin and every token variable removed from its environment.
[Collection("CWD")]
public sealed class SecretsStdinCliTests : IDisposable
{
    private const string FakeNotionToken = "fake_stdin_notion_token_0123456789";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-secrets-stdin-" + Guid.NewGuid().ToString("N"));

    public SecretsStdinCliTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        SecretStore.Reset();
        TestCleanup.DeleteDirectory(_root, recursive: true);
    }

    [Fact]
    public void Strip_RemovesFlagAndKeepsOtherArguments()
    {
        var args = SecretsStdinOption.Strip(["--config", "site.yaml", "--secrets-stdin", "--ci"], out var requested);

        Assert.True(requested);
        Assert.Equal(["--config", "site.yaml", "--ci"], args);
        Assert.Equal(["--ci"], SecretsStdinOption.Strip(["--ci"], out var notRequested));
        Assert.False(notRequested);
    }

    [Theory]
    [InlineData("build", new string[0], true)]
    [InlineData("doctor", new string[0], true)]
    [InlineData("dev", new[] { "--no-watch" }, true)]
    [InlineData("deploy", new[] { "--dry-run" }, true)]
    [InlineData("config", new[] { "check", "--config", "x" }, true)]
    [InlineData("config", new[] { "--config", "x", "check" }, true)]
    [InlineData("config", new[] { "schema" }, false)]
    [InlineData("preview", new string[0], false)]
    [InlineData("plugin", new string[0], false)]
    public void IsSupported_LimitsFlagToTokenConsumingCommands(string command, string[] args, bool expected)
        => Assert.Equal(expected, SecretsStdinOption.IsSupported(command, args));

    [Fact]
    public void Load_UnsupportedCommand_ReturnsDiagnosticWithoutReadingStdin()
    {
        var diagnostic = SecretsStdinOption.Load("preview", [], inputRedirected: true, () => throw new InvalidOperationException("stdin must not be read"));

        Assert.NotNull(diagnostic);
        Assert.Equal("secrets-stdin-invalid", diagnostic!.Code);
        Assert.Contains("bukit build", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_StdinNotRedirected_ReturnsDiagnosticInsteadOfBlocking()
    {
        var diagnostic = SecretsStdinOption.Load("build", [], inputRedirected: false, () => throw new InvalidOperationException("stdin must not be read"));

        Assert.NotNull(diagnostic);
        Assert.Contains("pipe or file", diagnostic!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ValidInput_InstallsSecrets()
    {
        var diagnostic = SecretsStdinOption.Load("doctor", [], inputRedirected: true,
            () => new MemoryStream(Encoding.UTF8.GetBytes("{\"BUKIT_TEST_STDIN_LOAD\":\"fake-value\"}")));

        Assert.Null(diagnostic);
        Assert.Equal("fake-value", SecretStore.Get("BUKIT_TEST_STDIN_LOAD"));
    }

    [Fact]
    public async Task ConfigCheck_NotionSite_ReadsTokenFromStdinWithoutEnvironment()
    {
        var site = WriteNotionSite();

        var withoutSecrets = await RunCliAsync(["config", "check", "--config", site], stdin: null);
        Assert.True(withoutSecrets.ExitCode != 0, withoutSecrets.Output);
        Assert.Contains("NOTION_TOKEN is required", withoutSecrets.Output, StringComparison.Ordinal);

        var withSecrets = await RunCliAsync(
            ["config", "check", "--config", site, "--secrets-stdin"],
            stdin: $"{{\"NOTION_TOKEN\":\"{FakeNotionToken}\"}}");
        Assert.True(withSecrets.ExitCode == 0, withSecrets.Output);
        Assert.DoesNotContain(FakeNotionToken, withSecrets.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Doctor_AcceptsSecretsStdin()
    {
        var site = WriteMarkdownSite(Path.Combine(_root, "doctor"));

        var result = await RunCliAsync(["doctor", "--config", site, "--secrets-stdin"], stdin: $"{{\"NOTION_TOKEN\":\"{FakeNotionToken}\"}}");

        Assert.DoesNotContain("secrets-stdin-invalid", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeNotionToken, result.Output, StringComparison.Ordinal);
        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Fact]
    public async Task Build_AcceptsSecretsStdin()
    {
        var root = Path.Combine(_root, "build");
        var site = WriteMarkdownSite(root);

        var result = await RunCliAsync(["build", "--config", site, "--secrets-stdin"], stdin: $"{{\"NOTION_TOKEN\":\"{FakeNotionToken}\"}}");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.True(File.Exists(Path.Combine(root, "dist", "index.html")), result.Output);
        Assert.DoesNotContain(FakeNotionToken, result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_MultilingualWorkers_ReceiveSecretsOverStdin()
    {
        // Each language worker is a child Bukit process started with --secrets-stdin; it fails
        // with secrets-stdin-invalid unless the parent forwards the secrets on its stdin.
        var root = Path.Combine(_root, "i18n");
        var site = WriteMarkdownSite(root, languages: "[en, zh-CN]");
        await InitializeRepositoryAsync(root);

        var result = await RunCliAsync(["build", "--config", site, "--secrets-stdin"], stdin: $"{{\"NOTION_TOKEN\":\"{FakeNotionToken}\"}}", workingDirectory: root);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.DoesNotContain("secrets-stdin-invalid", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeNotionToken, result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"NOTION_TOKEN\":\"fake_leak_marker\"", "not valid JSON")]
    [InlineData("{\"NOTION_TOKEN\":424242}", "must be JSON strings")]
    [InlineData("", "standard input was empty")]
    public async Task Build_InvalidSecretsInput_FailsWithCliErrorThatNeverEchoesInput(string stdin, string expected)
    {
        var site = WriteMarkdownSite(Path.Combine(_root, "invalid"));

        var result = await RunCliAsync(["build", "--config", site, "--secrets-stdin", "--log-format", "json"], stdin: stdin);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expected, result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("fake_leak_marker", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("424242", result.Output, StringComparison.Ordinal);
        Assert.Contains("\"secrets-stdin-invalid\"", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_TooLargeSecretsInput_Fails()
    {
        var site = WriteMarkdownSite(Path.Combine(_root, "large"));

        var result = await RunCliAsync(
            ["build", "--config", site, "--secrets-stdin"],
            stdin: "{\"NOTION_TOKEN\":\"" + new string('q', SecretStore.MaxStdinBytes) + "\"}");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("64 KiB", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("qqqq", result.Output, StringComparison.Ordinal);
    }

    private string WriteNotionSite()
    {
        var root = Path.Combine(_root, "notion");
        Directory.CreateDirectory(root);
        var site = Path.Combine(root, "site.yaml");
        File.WriteAllText(site, """
            site:
              name: test
              title: Test
              url: https://example.com
            content:
              sources:
                - type: notion
                  name: page
                  notion:
                    databaseId: abc
            """);
        return site;
    }

    private static string WriteMarkdownSite(string root, string languages = "[en]")
    {
        Directory.CreateDirectory(Path.Combine(root, "content"));
        Directory.CreateDirectory(Path.Combine(root, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
        File.WriteAllText(Path.Combine(root, ".gitignore"), "/dist/\n/.cache/\n");
        var site = Path.Combine(root, "site.yaml");
        File.WriteAllText(site, $$"""
            site:
              name: secrets-stdin-test
              title: Secrets Stdin Test
              url: https://example.test
              languages: {{languages}}
              defaultLanguage: en
              collections:
                page:
                  permalink: /{slug}/
                  template: pages/page.html
            content:
              sources:
                - type: markdown
                  name: pages
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
              clean: false
              languageJobs: 1
            theme:
              layouts: layouts
            """);
        File.WriteAllText(Path.Combine(root, "content", "index.md"), "---\ntitle: Home\nlanguage: en\ni18nKey: home\n---\nBody");
        if (languages.Contains("zh-CN", StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(root, "content", "index.zh.md"), "---\ntitle: Home ZH\nlanguage: zh-CN\ni18nKey: home\nslug: index\n---\nBody");
        }

        File.WriteAllText(Path.Combine(root, "layouts", "layouts", "base.html"), "<!doctype html><html><head><title>{{ page.title }}</title></head><body>{{ content }}</body></html>");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ content }}");
        File.WriteAllText(Path.Combine(root, "layouts", "pages", "index.html"), "{{ content }}");
        return site;
    }

    private static async Task InitializeRepositoryAsync(string root)
    {
        foreach (var args in new[]
                 {
                     new[] { "init" },
                     new[] { "config", "user.email", "bukit-tests@example.test" },
                     new[] { "config", "user.name", "Bukit Tests" },
                     new[] { "add", "." },
                     new[] { "commit", "-m", "fixture" }
                 })
        {
            var result = await RunProcessAsync("git", args, root, stdin: null, scrubTokens: false);
            Assert.True(result.ExitCode == 0, result.Output);
        }
    }

    private Task<(int ExitCode, string Output)> RunCliAsync(IReadOnlyList<string> args, string? stdin, string? workingDirectory = null)
        => RunProcessAsync(
            "dotnet",
            [typeof(BuildCommand).Assembly.Location, .. args],
            workingDirectory ?? _root,
            stdin,
            scrubTokens: true);

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        string workingDirectory,
        string? stdin,
        bool scrubTokens)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (scrubTokens)
        {
            // The child must prove it works with tokens on stdin only.
            process.StartInfo.Environment.Remove("NOTION_TOKEN");
            process.StartInfo.Environment.Remove("GITHUB_TOKEN");
        }

        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            if (stdin is not null)
            {
                await process.StandardInput.WriteAsync(stdin);
            }
        }
        catch (IOException)
        {
            // The child may reject oversized input and exit before reading all of it.
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (IOException)
            {
            }
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await stdout + await stderr);
    }
}
