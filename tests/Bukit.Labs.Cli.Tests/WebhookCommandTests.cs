using System.Net;
using System.Net.Http;
using System.Reflection;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class WebhookCommandTests
{
    private static readonly MethodInfo s_normalizePath = typeof(WebhookCommand)
        .GetMethod("NormalizePath", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    private static readonly MethodInfo s_fixedTimeTokenEquals = typeof(WebhookCommand)
        .GetMethod("FixedTimeTokenEquals", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    private static readonly MethodInfo s_createHttpClient = typeof(WebhookCommand)
        .GetMethod("CreateGitHubHttpClient", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    private static readonly MethodInfo s_handleRequestAsync = typeof(WebhookCommand)
        .GetMethod("HandleRequestAsync", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    private static readonly Type s_rateLimiterType = typeof(WebhookCommand)
        .GetNestedType("SlidingWindowRateLimiter", BindingFlags.NonPublic)!
        ;

    [Fact]
    public async Task RunAsync_Help_ReturnsZero()
    {
        var command = new CliBoundCommand(new Dictionary<string, string?>(), ["help"]);

        var (exitCode, output, _) = await CaptureConsoleAsync(() => WebhookCommand.RunAsync(command));

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage: bukit webhook [start] [options]", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_InvalidSubcommand_ReturnsTwo()
    {
        var command = new CliBoundCommand(new Dictionary<string, string?>(), ["push"]);

        var (exitCode, output, error) = await CaptureConsoleAsync(() => WebhookCommand.RunAsync(command));

        Assert.Equal(2, exitCode);
        Assert.Contains("Unknown webhook subcommand: push", error, StringComparison.Ordinal);
        Assert.Contains("Usage: bukit webhook [start] [options]", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_InvalidPort_ReturnsTwo()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--port"] = "70000"
            },
            Array.Empty<string>());

        var (exitCode, _, error) = await CaptureConsoleAsync(() => WebhookCommand.RunAsync(command));

        Assert.Equal(2, exitCode);
        Assert.Contains("Invalid --port.", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MissingToken_ReturnsTwo()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--port"] = "8787"
            },
            Array.Empty<string>());

        var (exitCode, _, error) = await CaptureConsoleAsync(() => WebhookCommand.RunAsync(command));

        Assert.Equal(2, exitCode);
        Assert.Contains("Missing env: BUKIT_WEBHOOK_TOKEN", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MissingRepo_ReturnsTwo()
    {
        var command = new CliBoundCommand(new Dictionary<string, string?>(), Array.Empty<string>());

        var (exitCode, _, error) = await WithEnvironmentAsync(
            ("BUKIT_WEBHOOK_TOKEN", "secret"),
            ("BUKIT_GITHUB_REPO", null),
            ("BUKIT_GITHUB_TOKEN", null),
            () => CaptureConsoleAsync(() => WebhookCommand.RunAsync(command)));

        Assert.Equal(2, exitCode);
        Assert.Contains("Missing --repo <owner/repo> or env: BUKIT_GITHUB_REPO", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_MissingGitHubToken_ReturnsTwo()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--repo"] = "owner/repo"
            },
            Array.Empty<string>());

        var (exitCode, _, error) = await WithEnvironmentAsync(
            ("BUKIT_WEBHOOK_TOKEN", "secret"),
            ("BUKIT_GITHUB_TOKEN", null),
            ("GITHUB_TOKEN", null),
            () => CaptureConsoleAsync(() => WebhookCommand.RunAsync(command)));

        Assert.Equal(2, exitCode);
        Assert.Contains("Missing env: BUKIT_GITHUB_TOKEN", error, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizePath_DefaultsAndAddsLeadingSlash()
    {
        Assert.Equal("/webhook/notion", (string)s_normalizePath.Invoke(null, [null!])!);
        Assert.Equal("/custom", (string)s_normalizePath.Invoke(null, ["custom"])!);
    }

    [Fact]
    public void FixedTimeTokenEquals_ComparesValues()
    {
        Assert.True((bool)s_fixedTimeTokenEquals.Invoke(null, ["abc", "abc"])!);
        Assert.False((bool)s_fixedTimeTokenEquals.Invoke(null, ["abc", "xyz"])!);
    }

    [Fact]
    public void CreateGitHubHttpClient_SetsHeaders()
    {
        using var client = (HttpClient)s_createHttpClient.Invoke(null, ["secret-token"])!;

        Assert.Contains(client.DefaultRequestHeaders.UserAgent, header => header.Product?.Name == "bukit");
        Assert.Contains(client.DefaultRequestHeaders.Accept, header => header.MediaType == "application/vnd.github+json");
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization?.Scheme);
    }

    [Fact]
    public async Task HandleRequestAsync_GetMethod_Returns405()
    {
        var limiter = Activator.CreateInstance(s_rateLimiterType, 1, TimeSpan.FromMinutes(1))!;
        var response = await InvokeHandleRequestAsync(
            HttpMethod.Get,
            "/webhook/notion",
            headers: Array.Empty<(string, string)>(),
            token: "secret",
            limiter: limiter);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response);
    }

    [Fact]
    public async Task HandleRequestAsync_BadToken_Returns401()
    {
        var limiter = Activator.CreateInstance(s_rateLimiterType, 1, TimeSpan.FromMinutes(1))!;
        var response = await InvokeHandleRequestAsync(
            HttpMethod.Post,
            "/webhook/notion",
            headers: [("X-Sitegen-Token", "wrong")],
            token: "secret",
            limiter: limiter);

        Assert.Equal(HttpStatusCode.Unauthorized, response);
    }

    [Fact]
    public async Task HandleRequestAsync_RateLimited_Returns429()
    {
        var limiter = Activator.CreateInstance(s_rateLimiterType, 0, TimeSpan.FromMinutes(1))!;
        var response = await InvokeHandleRequestAsync(
            HttpMethod.Post,
            "/webhook/notion",
            headers: [("X-Sitegen-Token", "secret")],
            token: "secret",
            limiter: limiter);

        Assert.Equal((HttpStatusCode)429, response);
    }

    private static async Task<HttpStatusCode> InvokeHandleRequestAsync(
        HttpMethod method,
        string path,
        IReadOnlyList<(string Name, string Value)> headers,
        string token,
        object limiter)
    {
        using var listener = StartListener(out var prefix);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var contextTask = listener.GetContextAsync();
        using var request = new HttpRequestMessage(method, new Uri(new Uri(prefix), path.TrimStart('/')));
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        var responseTask = client.SendAsync(request);
        var context = await contextTask.WaitAsync(TimeSpan.FromSeconds(5));
        using var http = new HttpClient();
        using var gate = new SemaphoreSlim(1, 1);
        var task = (Task)s_handleRequestAsync.Invoke(null,
            [context, "/webhook/notion", token, "owner/repo", "bukit_notion", http, gate, limiter])!;
        await task;
        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        return response.StatusCode;
    }

    private static HttpListener StartListener(out string prefix)
    {
        using var tcp = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((System.Net.IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();

        prefix = $"http://localhost:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        return listener;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> CaptureConsoleAsync(Func<Task<int>> action)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await action();
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static async Task<T> WithEnvironmentAsync<T>(
        (string Name, string? Value) first,
        (string Name, string? Value) second,
        (string Name, string? Value) third,
        Func<Task<T>> action)
    {
        var entries = new[] { first, second, third };
        var originals = entries.ToDictionary(entry => entry.Name, entry => Environment.GetEnvironmentVariable(entry.Name), StringComparer.Ordinal);

        try
        {
            foreach (var (name, value) in entries)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            return await action();
        }
        finally
        {
            foreach (var (name, value) in originals)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
