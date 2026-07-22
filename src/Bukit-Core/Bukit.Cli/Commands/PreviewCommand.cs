using System.Net;
using System.Net.Sockets;
using System.Threading;
using Bukit.Cli.Commands.Dev;
using Bukit.Cli.Shared;
using Bukit.Config;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Engine.Analytics;

namespace Bukit.Cli.Commands;

public static class PreviewCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
        => await RunAsync(command, CancellationToken.None);

    public static async Task<int> RunAsync(CliBoundCommand command, CancellationToken cancellationToken)
    {
        var dirOpt = command.GetString("--dir");
        var configPathOpt = command.GetString("--config");
        var siteOpt = command.GetString("--site");
        ResolvedConfigPath? resolvedConfigPath = null;
        AppConfig? explicitConfig = null;

        if (!string.IsNullOrWhiteSpace(configPathOpt) || !string.IsNullOrWhiteSpace(siteOpt))
        {
            resolvedConfigPath = ConfigPathResolver.Resolve(configPathOpt, siteOpt);
            explicitConfig = ConfigLoader.Load(resolvedConfigPath.FullConfigPath);
        }

        string dir;

        if (!string.IsNullOrWhiteSpace(dirOpt))
        {
            dir = Path.GetFullPath(dirOpt);
        }
        else if (resolvedConfigPath is not null && explicitConfig is not null)
        {
            dir = Path.GetFullPath(Path.Combine(resolvedConfigPath.RootDir, explicitConfig.Build.Output));
        }
        else
        {
            dir = Path.GetFullPath("dist");
        }

        var host = (command.GetString("--host") ?? "localhost").Trim();
        var portText = (command.GetString("--port") ?? "4173").Trim();
        var strictPort = command.GetBool("--strict-port");

        var port = ParsePort(portText);
        if (port < 0 || port > 65535)
        {
            Console.Error.WriteLine("Invalid --port.");
            return 2;
        }

        if (!Directory.Exists(dir))
        {
            Console.Error.WriteLine($"Directory not found: {dir}");
            return 2;
        }

        var analyticsPolicy = explicitConfig is not null
            ? new PreviewAnalyticsPolicyResolution(
                ShouldRemoveManagedAnalytics(explicitConfig.Site)
                    ? PreviewAnalyticsPolicyDecision.Remove
                    : PreviewAnalyticsPolicyDecision.Keep,
                resolvedConfigPath!.FullConfigPath,
                ConfigFound: true,
                Error: null)
            : ResolveAnalyticsPolicyInPreview(dir);

        if (analyticsPolicy.Decision == PreviewAnalyticsPolicyDecision.Error)
        {
            var error = analyticsPolicy.Error!;
            Console.Error.WriteLine(
                $"Preview analytics policy error: failed to load '{analyticsPolicy.Source}' " +
                $"({error.GetType().Name}): {error.Message}");
            return 2;
        }

        if (!analyticsPolicy.ConfigFound)
        {
            Console.Error.WriteLine(
                $"Warning: No site.yaml found while resolving Preview analytics policy from '{dir}'; " +
                "managed Analytics blocks will be kept. Use --config or --site to select a policy explicitly.");
        }

        var removeManagedAnalytics = analyticsPolicy.Decision == PreviewAnalyticsPolicyDecision.Remove;
        var (listener, prefix) = CreateAndStartListener(host, port, strictPort);
        using var startedListener = listener;
        using var cancellationRegistration = cancellationToken.Register(listener.Stop);

        Console.WriteLine($"Preview: {prefix}");
        Console.WriteLine($"Serving: {dir}");
        Console.WriteLine($"Analytics policy: {analyticsPolicy.Decision.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Analytics policy source: {analyticsPolicy.Source}");
        Console.WriteLine("Press Ctrl+C to stop.");

        while (true)
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(
                    () => HandleRequest(dir, context, removeManagedAnalytics),
                    CancellationToken.None);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested || !listener.IsListening)
            {
                return 0;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested || !listener.IsListening)
            {
                return 0;
            }
        }
    }

    public static string ApplyPreviewAnalyticsPolicy(string html, bool removeManagedAnalytics)
    {
        if (!removeManagedAnalytics || string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        return AnalyticsManagedBlockFilter.Remove(html);
    }

    internal static bool ShouldRemoveManagedAnalytics(SiteConfig site)
    {
        var analytics = site.Analytics;
        if (!analytics.Enabled || !analytics.ProductionOnly || analytics.Providers.Count == 0)
        {
            return false;
        }

        return site.Plugins is null ||
               !site.Plugins.TryGetValue("analytics", out var plugin) ||
               plugin.Enabled;
    }

    private static int ParsePort(string portText)
    {
        if (string.Equals(portText, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!int.TryParse(portText, out var port))
        {
            return -1;
        }

        return port;
    }

    private static (HttpListener Listener, string Prefix) CreateAndStartListener(string host, int port, bool strictPort)
    {
        var baseHost = string.IsNullOrWhiteSpace(host) ? "localhost" : host;

        if (port == 0)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var chosen = PickFreeTcpPort();
                var prefix = $"http://{baseHost}:{chosen}/";
                var listener = new HttpListener();
                listener.Prefixes.Add(prefix);

                try
                {
                    listener.Start();
                    return (listener, prefix);
                }
                catch (HttpListenerException ex) when (IsPortConflict(ex))
                {
                    listener.Close();
                }
            }

            throw new InvalidOperationException($"Failed to listen on http://{baseHost}:auto/ (port conflict). Try a different --host or explicit --port.");
        }

        var maxAttempts = strictPort ? 1 : 20;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidatePort = port + attempt;
            if (candidatePort <= 0 || candidatePort > 65535)
            {
                break;
            }

            var prefix = $"http://{baseHost}:{candidatePort}/";
            if (!IsTcpPortAvailable(baseHost, candidatePort))
            {
                continue;
            }

            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                if (attempt > 0)
                {
                    Console.WriteLine($"Port {port} unavailable, switched to {candidatePort}.");
                }

                return (listener, prefix);
            }
            catch (HttpListenerException ex) when (!strictPort && IsPortConflict(ex))
            {
                listener.Close();
            }
        }

        throw new InvalidOperationException($"Failed to listen on http://{baseHost}:{port}/ (port conflict). Try --port auto or a different --port.");
    }

    private static bool IsTcpPortAvailable(string host, int port)
    {
        var address = IPAddress.TryParse(host, out var parsedAddress)
            ? parsedAddress
            : IPAddress.Loopback;

        TcpListener? probe = null;
        try
        {
            probe = new TcpListener(address, port);
            probe.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            probe?.Stop();
        }
    }

    private static bool IsPortConflict(HttpListenerException ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("conflicts with an existing registration", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("cannot access the file", StringComparison.OrdinalIgnoreCase);
    }

    private static int PickFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void HandleRequest(string rootDir, HttpListenerContext context, bool removeManagedAnalytics)
    {
        try
        {
            var path = GetRawPath(context.Request);
            var candidate = DevPathGuard.TryResolveWithinRoot(rootDir, path);
            if (candidate is null)
            {
                context.Response.StatusCode = 403;
                return;
            }

            if (path.EndsWith("/", StringComparison.Ordinal))
            {
                candidate = Path.Combine(candidate, "index.html");
            }

            if (!File.Exists(candidate) && !Path.HasExtension(candidate))
            {
                candidate = Path.Combine(candidate, "index.html");
            }

            if (!File.Exists(candidate))
            {
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = GetContentType(candidate);
            if (Path.GetExtension(candidate).Equals(".html", StringComparison.OrdinalIgnoreCase))
            {
                if (!removeManagedAnalytics)
                {
                    using var fs = File.OpenRead(candidate);
                    context.Response.ContentLength64 = fs.Length;
                    fs.CopyTo(context.Response.OutputStream);
                }
                else
                {
                    var source = File.ReadAllBytes(candidate);
                    var bytes = HtmlResponseByteTransformer.RewriteUtf8(
                        source,
                        html => ApplyPreviewAnalyticsPolicy(html, removeManagedAnalytics: true));
                    context.Response.ContentLength64 = bytes.Length;
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
            }
            else
            {
                using var fs = File.OpenRead(candidate);
                context.Response.ContentLength64 = fs.Length;
                fs.CopyTo(context.Response.OutputStream);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Preview error: {ex.GetType().Name}: {ex.Message}");
            try { context.Response.StatusCode = 500; } catch (Exception scEx) { Console.Error.WriteLine($"Preview: failed to set status code: {scEx.GetType().Name}"); }
        }
        finally
        {
            try { context.Response.Close(); } catch (Exception clEx) { Console.Error.WriteLine($"Preview: failed to close response: {clEx.GetType().Name}"); }
        }
    }

    private static string GetContentType(string path)
        => StaticAssetContentTypeResolver.ResolvePath(path);

    private static string GetRawPath(HttpListenerRequest request)
    {
        var raw = request.RawUrl ?? request.Url?.AbsolutePath ?? "/";
        var queryIndex = raw.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? raw[..queryIndex] : raw;
    }

    internal static PreviewAnalyticsPolicyResolution ResolveAnalyticsPolicyInPreview(string previewDir)
    {
        var current = new DirectoryInfo(Path.GetFullPath(previewDir));
        while (current is not null)
        {
            var configPath = Path.Combine(current.FullName, "site.yaml");
            if (File.Exists(configPath))
            {
                try
                {
                    var config = ConfigLoader.Load(configPath);
                    return new PreviewAnalyticsPolicyResolution(
                        ShouldRemoveManagedAnalytics(config.Site)
                            ? PreviewAnalyticsPolicyDecision.Remove
                            : PreviewAnalyticsPolicyDecision.Keep,
                        configPath,
                        ConfigFound: true,
                        Error: null);
                }
                catch (Exception ex)
                {
                    return new PreviewAnalyticsPolicyResolution(
                        PreviewAnalyticsPolicyDecision.Error,
                        configPath,
                        ConfigFound: true,
                        Error: ex);
                }
            }

            current = current.Parent;
        }

        return new PreviewAnalyticsPolicyResolution(
            PreviewAnalyticsPolicyDecision.Keep,
            $"no site.yaml found from {Path.GetFullPath(previewDir)}",
            ConfigFound: false,
            Error: null);
    }
}

internal enum PreviewAnalyticsPolicyDecision
{
    Keep,
    Remove,
    Error
}

internal sealed record PreviewAnalyticsPolicyResolution(
    PreviewAnalyticsPolicyDecision Decision,
    string Source,
    bool ConfigFound,
    Exception? Error);
