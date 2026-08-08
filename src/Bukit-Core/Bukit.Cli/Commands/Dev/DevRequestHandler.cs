using System.Net;
using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevRequestHandler
{
    private const string LivereloadScript =
        """
        <script>
        (function(){const protocol = location.protocol === 'https:' ? 'wss://' : 'ws://';const host = location.hostname || 'localhost';const socketHost = host.indexOf(':') >= 0 ? '[' + host + ']' : host;const port = location.port ? ':' + location.port : '';var s=new WebSocket(protocol+socketHost+port+'/__ws__');s.onclose=function(){console.log('[bukit] livereload disconnected, retrying in 1s...');setTimeout(function(){location.reload();},1000);};s.onmessage=function(e){if(e.data==='reload'){console.log('[bukit] change detected, reloading...');location.reload();}};s.onerror=function(){}})();
        </script>
        """;

    private readonly string _outputDir;
    private readonly bool _removeManagedAnalytics;
    private readonly ILogger _logger;

    public DevRequestHandler(string outputDir, bool removeManagedAnalytics, ILogger logger)
    {
        _outputDir = outputDir ?? throw new ArgumentNullException(nameof(outputDir));
        _removeManagedAnalytics = removeManagedAnalytics;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var path = GetRawPath(context.Request);

            var candidate = DevPathGuard.TryResolveWithinRoot(_outputDir, path);
            if (candidate is null)
            {
                context.Response.StatusCode = 403;
                return;
            }

            if (StaticServerInternalPathPolicy.IsInternalOutputPath(_outputDir, candidate))
            {
                context.Response.StatusCode = 404;
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

            var ext = Path.GetExtension(candidate).ToLowerInvariant();
            context.Response.ContentType = ResolveMimeType(ext);

            if (ext == ".html")
            {
                var source = await File.ReadAllBytesAsync(candidate, ct).ConfigureAwait(false);
                var bytes = HtmlResponseByteTransformer.RewriteUtf8(
                    source,
                    html => InjectLivereload(
                        PreviewCommand.ApplyPreviewAnalyticsPolicy(html, _removeManagedAnalytics)));
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
            }
            else
            {
                using var fs = File.OpenRead(candidate);
                context.Response.ContentLength64 = fs.Length;
                await fs.CopyToAsync(context.Response.OutputStream, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancellation; nothing to log.
        }
        catch (Exception ex)
        {
            _logger.Warn($"dev.request: {ex.Message}");
            TrySetStatusCode(context.Response, 500);
        }
        finally
        {
            CloseResponseBestEffort(context.Response);
        }
    }

    private void TrySetStatusCode(HttpListenerResponse response, int statusCode)
    {
        try
        {
            response.StatusCode = statusCode;
        }
        catch (ObjectDisposedException ex)
        {
            _logger.Warn($"dev.response_status_skipped: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warn($"dev.response_status_skipped: {ex.Message}");
        }
    }

    private void CloseResponseBestEffort(HttpListenerResponse response)
    {
        try
        {
            response.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warn($"dev.response_close_skipped: {ex.Message}");
        }
    }

    private static string GetRawPath(HttpListenerRequest request)
    {
        var raw = request.RawUrl ?? request.Url?.AbsolutePath ?? "/";
        var queryIndex = raw.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? raw[..queryIndex] : raw;
    }

    /// <summary>
    /// Pure helper exposed for unit tests: injects the livereload script into
    /// <paramref name="html"/> (before <c>&lt;/head&gt;</c> when present, else appended).
    /// </summary>
    internal static string InjectLivereload(string html)
    {
        var script = LivereloadScript;
        var idx = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? html.Insert(idx, script) : html + script;
    }

    private static string ResolveMimeType(string ext)
        => StaticAssetContentTypeResolver.ResolveExtension(ext);
}
