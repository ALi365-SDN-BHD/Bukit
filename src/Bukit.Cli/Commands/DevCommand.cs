using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Bukit.Cli;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class DevCommand
{
    private const string LivereloadScript =
"""
<script>
(function(){var s=new WebSocket('ws://'+(location.host||'localhost:__PORT__').split(':')[0]+':__PORT__/__ws__');s.onclose=function(){console.log('[bukit] livereload disconnected, retrying in 1s...');setTimeout(function(){location.reload();},1000);};s.onmessage=function(e){if(e.data==='reload'){console.log('[bukit] change detected, reloading...');location.reload();}};s.onerror=function(){}})();
</script>
""";

    private static readonly ConcurrentDictionary<string, WebSocket> _wsClients = new();
    private static volatile int _devPort;

    public static async Task<int> RunAsync(string[] args)
    {
        var configPath = (string?)null;
        var site = (string?)null;
        var host = "localhost";
        var port = 35729;
        var noWatch = false;
        var outputOverride = (string?)null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config" when i + 1 < args.Length: configPath = args[++i]; break;
                case "--site" when i + 1 < args.Length: site = args[++i]; break;
                case "--host" when i + 1 < args.Length: host = args[++i]; break;
                case "--port" when i + 1 < args.Length: port = int.Parse(args[++i]); break;
                case "--output" or "--dir" when i + 1 < args.Length: outputOverride = args[++i]; break;
                case "--no-watch": noWatch = true; break;
            }
        }

        var resolved = ConfigPathResolver.Resolve(configPath, site);
        var config = ConfigLoader.Load(resolved.FullConfigPath);
        var rootDir = resolved.RootDir;
        var outputDir = outputOverride is not null
            ? Path.GetFullPath(outputOverride)
            : Path.GetFullPath(Path.Combine(rootDir, config.Build.Output));

        var cacheDir = Path.Combine(rootDir, ".cache");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine($"bukit dev \u2014 HMR development server");
        Console.WriteLine($"  root: {rootDir}");

        var logger = new ConsoleLogger(LogLevel.Info);
        var engine = new SiteEngine(logger);

        Console.WriteLine("\n[build] initial full build...");
        var sw = Stopwatch.StartNew();
        await engine.BuildAsync(
            config, rootDir,
            CreateBuildOverrides(clean: true, outputOverride, cacheDir),
            cts.Token);
        sw.Stop();
        Console.WriteLine($"[build] done in {sw.ElapsedMilliseconds}ms, serving {outputDir}\n");

        var listener = CreateListener(host, port, out var actualPort);
        _devPort = actualPort;
        var prefix = $"http://{host}:{actualPort}/";

        var watchedDirs = ResolveWatchDirs(rootDir, config);
        if (!noWatch && watchedDirs.Count > 0)
        {
            StartFileWatchers(watchedDirs, rootDir, outputDir, outputOverride, config, cacheDir, engine, logger, cts.Token);
        }

        Console.WriteLine($"  dev server: {prefix}");
        if (!noWatch)
        {
            Console.WriteLine($"  live reload: ws://{host}:{actualPort}/__ws__");
            Console.WriteLine($"  watching: {watchedDirs.Count} directorie(s)");
        }
        Console.WriteLine("  Press Ctrl+C to stop.\n");

        _ = Task.Run(() => AcceptLoop(listener, outputDir, config, actualPort, cts.Token), cts.Token);

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        Console.WriteLine("\ndev server stopped.");
        listener.Close();
        return 0;
    }

    private static HttpListener CreateListener(string host, int port, out int actualPort)
    {
        var chosen = port == 0 ? PickFreePort() : port;
        var listener = new HttpListener();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = chosen + attempt;
            if (candidate > 65535) break;

            try
            {
                listener.Prefixes.Clear();
                listener.Prefixes.Add($"http://{host}:{candidate}/");
                listener.Start();
                if (attempt > 0)
                    Console.WriteLine($"Port {chosen} unavailable, using {candidate}.");
                actualPort = candidate;
                return listener;
            }
            catch (HttpListenerException)
            {
            }
        }

        listener.Close();
        throw new InvalidOperationException($"Failed to listen on {host}:{chosen}");
    }

    private static int PickFreePort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var p = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return p;
    }

    internal static ConfigOverrides CreateBuildOverrides(bool clean, string? outputOverride, string cacheDir)
    {
        return new ConfigOverrides
        {
            Clean = clean,
            Output = outputOverride,
            Incremental = true,
            CacheDir = cacheDir
        };
    }

    private static async Task AcceptLoop(HttpListener listener, string outputDir, AppConfig config, int port, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await listener.GetContextAsync().WaitAsync(ct);
                var path = context.Request.Url?.AbsolutePath ?? "/";

                if (path == "/__ws__")
                {
                    _ = HandleWebSocketUpgradeAsync(context, ct);
                }
                else
                {
                    _ = Task.Run(() => HandleFileRequest(outputDir, context, config, port, ct));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }

        listener.Stop();
    }

    private static async Task HandleWebSocketUpgradeAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var wsCtx = await context.AcceptWebSocketAsync(null);
            var ws = wsCtx.WebSocket;
            var clientId = Guid.NewGuid().ToString("N");
            _wsClients[clientId] = ws;

            var buffer = new byte[256];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                try
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                        break;
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
            }

            _wsClients.TryRemove(clientId, out _);
        }
        catch
        {
        }
    }

    private static async Task BroadcastReloadAsync()
    {
        var payload = Encoding.UTF8.GetBytes("reload");
        var deadClients = new List<string>();

        foreach (var (id, ws) in _wsClients)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(new ArraySegment<byte>(payload),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    deadClients.Add(id);
                }
            }
            catch
            {
                deadClients.Add(id);
            }
        }

        foreach (var id in deadClients)
        {
            _wsClients.TryRemove(id, out _);
        }
    }

    private static List<string> ResolveWatchDirs(string rootDir, AppConfig config)
    {
        var dirs = new List<string>();

        var contentDir = Path.Combine(rootDir, "content");
        if (Directory.Exists(contentDir)) dirs.Add(contentDir);

        if (!string.IsNullOrWhiteSpace(config.Theme.Name))
        {
            var themeDir = Path.Combine(rootDir, "themes", config.Theme.Name);
            if (Directory.Exists(themeDir)) dirs.Add(themeDir);

            if (!string.IsNullOrWhiteSpace(config.Theme.Extends))
            {
                var parentDir = Path.Combine(rootDir, "themes", config.Theme.Extends);
                if (Directory.Exists(parentDir)) dirs.Add(parentDir);
            }
        }

        void AddIfNotUnderTheme(string relPath)
        {
            var full = Path.Combine(rootDir, relPath);
            if (Directory.Exists(full) && !dirs.Any(d => full.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
                dirs.Add(full);
        }

        AddIfNotUnderTheme(config.Theme.Layouts);
        AddIfNotUnderTheme(config.Theme.Assets);
        AddIfNotUnderTheme(config.Theme.Static);

        return dirs;
    }

    private static void StartFileWatchers(
        List<string> watchDirs, string rootDir, string outputDir, string? outputOverride,
        AppConfig config, string cacheDir, SiteEngine engine, ILogger logger,
        CancellationToken ct)
    {
        var rebuildLock = new SemaphoreSlim(1, 1);
        var pending = 0;
        const int debounceMs = 300;

        foreach (var dir in watchDirs)
        {
            var watcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName |
                               NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnChange;
            watcher.Created += OnChange;
            watcher.Deleted += OnChange;
            watcher.Renamed += (_, e) => ScheduleRebuild(e.FullPath);
            watcher.Error += (_, e) => logger.Warn($"dev.filewatcher: {e.GetException().Message}");
        }

        void OnChange(object sender, FileSystemEventArgs e)
        {
            var name = e.Name ?? string.Empty;
            if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}.cache{Path.DirectorySeparatorChar}") ||
                e.FullPath.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}") ||
                name.StartsWith('.'))
            {
                return;
            }

            ScheduleRebuild(e.FullPath);
        }

        async void ScheduleRebuild(string file)
        {
            Interlocked.Increment(ref pending);
            await Task.Delay(debounceMs, ct).ConfigureAwait(false);
            if (Interlocked.Decrement(ref pending) > 0) return;

            await rebuildLock.WaitAsync(ct);
            try
            {
                var rel = Path.GetRelativePath(rootDir, file);
                logger.Info($"dev.change {rel}");

                var sw = Stopwatch.StartNew();
                await engine.BuildAsync(config, rootDir,
                    CreateBuildOverrides(clean: false, outputOverride, cacheDir),
                    ct);
                sw.Stop();
                logger.Info($"dev.rebuild {sw.ElapsedMilliseconds}ms");

                _ = BroadcastReloadAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.Error($"dev.rebuild.error: {ex.Message}");
            }
            finally
            {
                rebuildLock.Release();
            }
        }
    }

    private static void HandleFileRequest(string rootDir, HttpListenerContext context, AppConfig config, int port, CancellationToken ct)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);

            var fullRoot = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(fullRoot, relative));
            var safeRoot = fullRoot + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase)
                && candidate != fullRoot)
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
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
                context.Response.Close();
                return;
            }

            var ext = Path.GetExtension(candidate).ToLowerInvariant();
            context.Response.ContentType = ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".xml" => "application/xml; charset=utf-8",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".txt" => "text/plain; charset=utf-8",
                _ => "application/octet-stream"
            };

            if (ext == ".html")
            {
                var html = File.ReadAllText(candidate);
                html = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, ResolveDisableAnalytics(rootDir));

                var script = LivereloadScript.Replace("__PORT__", port.ToString());
                var idx = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    html = html.Insert(idx, script);
                }
                else
                {
                    html += script;
                }

                var bytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                using var fs = File.OpenRead(candidate);
                context.Response.ContentLength64 = fs.Length;
                fs.CopyTo(context.Response.OutputStream);
            }
        }
        catch
        {
            try { context.Response.StatusCode = 500; } catch { }
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private static bool ResolveDisableAnalytics(string dir)
    {
        var current = new DirectoryInfo(Path.GetFullPath(dir));
        while (current is not null)
        {
            var configPath = Path.Combine(current.FullName, "site.yaml");
            if (File.Exists(configPath))
            {
                try
                {
                    var c = ConfigLoader.Load(configPath);
                    return c.Site.Analytics.DisableInPreview &&
                           !string.IsNullOrWhiteSpace(c.Site.Analytics.GoogleAnalyticsId);
                }
                catch
                {
                    return false;
                }
            }

            current = current.Parent;
        }

        return false;
    }
}
