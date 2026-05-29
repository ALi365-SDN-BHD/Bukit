using System.Net;
using System.Net.Http.Headers;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class WebhookCommand
{
    private const int RateLimitMaxRequests = 10;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0);
        if (sub is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(sub) && sub is not "start")
        {
            Console.Error.WriteLine($"Unknown webhook subcommand: {sub}");
            PrintHelp();
            return 2;
        }

        var host = (command.GetString("--host") ?? "localhost").Trim();
        var portText = (command.GetString("--port") ?? "8787").Trim();
        var path = NormalizePath(command.GetString("--path") ?? "/webhook/notion");

        if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
        {
            Console.Error.WriteLine("Invalid --port.");
            return 2;
        }

        var token = Environment.GetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Missing env: BUKIT_WEBHOOK_TOKEN");
            return 2;
        }

        var repo = command.GetString("--repo") ?? Environment.GetEnvironmentVariable("BUKIT_GITHUB_REPO");
        if (string.IsNullOrWhiteSpace(repo) || !repo.Contains('/', StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Missing --repo <owner/repo> or env: BUKIT_GITHUB_REPO");
            return 2;
        }

        var githubToken =
            Environment.GetEnvironmentVariable("BUKIT_GITHUB_TOKEN") ??
            Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        if (string.IsNullOrWhiteSpace(githubToken))
        {
            Console.Error.WriteLine("Missing env: BUKIT_GITHUB_TOKEN (or GITHUB_TOKEN)");
            return 2;
        }

        var eventType = command.GetString("--event") ?? "bukit_notion";
        var prefix = $"http://{host}:{port}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        Console.WriteLine($"Webhook: {prefix.TrimEnd('/')}{path}");
        Console.WriteLine("Press Ctrl+C to stop.");

        using var http = CreateGitHubHttpClient(githubToken);
        var gate = new SemaphoreSlim(1, 1);
        var rateLimiter = new SlidingWindowRateLimiter(RateLimitMaxRequests, RateLimitWindow);

        while (true)
        {
            var ctx = await listener.GetContextAsync();
            _ = Task.Run(() => HandleRequestAsync(ctx, path, token.Trim(), repo.Trim(), eventType.Trim(), http, gate, rateLimiter));
        }
    }

    private static async Task HandleRequestAsync(
        HttpListenerContext context,
        string path,
        string token,
        string repo,
        string eventType,
        HttpClient http,
        SemaphoreSlim gate,
        SlidingWindowRateLimiter rateLimiter)
    {
        try
        {
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 405;
                return;
            }

            if (!string.Equals(context.Request.Url?.AbsolutePath, path, StringComparison.Ordinal))
            {
                context.Response.StatusCode = 404;
                return;
            }

            var headerToken = context.Request.Headers["X-Sitegen-Token"] ?? string.Empty;
            if (!FixedTimeTokenEquals(headerToken, token))
            {
                context.Response.StatusCode = 401;
                return;
            }

            if (!rateLimiter.TryAcquire())
            {
                context.Response.StatusCode = 429;
                return;
            }

            await gate.WaitAsync();
            try
            {
                await DispatchAsync(http, repo, eventType);
            }
            finally
            {
                gate.Release();
            }

            context.Response.StatusCode = 202;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Webhook error: {ex.GetType().Name}: {ex.Message}");
            try { context.Response.StatusCode = 500; } catch (Exception scEx) { Console.Error.WriteLine($"Webhook: failed to set status code: {scEx.GetType().Name}"); }
        }
        finally
        {
            try { context.Response.Close(); } catch (Exception clEx) { Console.Error.WriteLine($"Webhook: failed to close response: {clEx.GetType().Name}"); }
        }
    }

    private static async Task DispatchAsync(HttpClient http, string repo, string eventType)
    {
        var url = $"https://api.github.com/repos/{repo}/dispatches";
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("event_type", eventType);
            writer.WritePropertyName("client_payload");
            writer.WriteStartObject();
            writer.WriteString("source", "bukit-webhook");
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using var content = new ByteArrayContent(buffer.WrittenSpan.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using var res = await http.PostAsync(url, content);

        if (!res.IsSuccessStatusCode)
        {
            var text = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"GitHub dispatch failed: {(int)res.StatusCode} {res.ReasonPhrase} {text}");
        }
    }

    private static HttpClient CreateGitHubHttpClient(string token)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("bukit", "2"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private static bool FixedTimeTokenEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static string NormalizePath(string path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/webhook/notion";
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("webhook — 启动 Notion 触发 GitHub Actions 的 Webhook 服务");
        Console.WriteLine();
        Console.WriteLine("Usage: bukit webhook [start] [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --host <host>        监听地址 (default: localhost)");
        Console.WriteLine("  --port <port>        监听端口 (default: 8787)");
        Console.WriteLine("  --path <path>        回调路径 (default: /webhook/notion)");
        Console.WriteLine("  --repo <owner/repo>  GitHub 仓库 (或环境变量 BUKIT_GITHUB_REPO)");
        Console.WriteLine("  --event <type>       GitHub dispatch event_type (default: bukit_notion)");
        Console.WriteLine();
        Console.WriteLine("Required environment:");
        Console.WriteLine("  BUKIT_WEBHOOK_TOKEN                         (webhook 端验证令牌)");
        Console.WriteLine("  BUKIT_GITHUB_TOKEN or GITHUB_TOKEN           (GitHub API 个人令牌)");
        Console.WriteLine("  BUKIT_GITHUB_REPO (or --repo <owner/repo>)   (目标仓库)");
    }

    private sealed class SlidingWindowRateLimiter
    {
        private readonly int _maxRequests;
        private readonly TimeSpan _window;
        private readonly Queue<long> _timestamps = new();
        private readonly object _lock = new();

        public SlidingWindowRateLimiter(int maxRequests, TimeSpan window)
        {
            _maxRequests = maxRequests;
            _window = window;
        }

        public bool TryAcquire()
        {
            var now = Environment.TickCount64;
            var cutoff = now - (long)_window.TotalMilliseconds;

            lock (_lock)
            {
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count >= _maxRequests)
                {
                    return false;
                }

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }
}
