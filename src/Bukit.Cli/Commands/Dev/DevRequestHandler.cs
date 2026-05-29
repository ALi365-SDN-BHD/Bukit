using System.Net;
using System.Text;
using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevRequestHandler
{
    private const string LivereloadScript =
"""
<script>
(function(){var s=new WebSocket('ws://'+(location.host||'localhost:__PORT__').split(':')[0]+':__PORT__/__ws__');s.onclose=function(){console.log('[bukit] livereload disconnected, retrying in 1s...');setTimeout(function(){location.reload();},1000);};s.onmessage=function(e){if(e.data==='reload'){console.log('[bukit] change detected, reloading...');location.reload();}};s.onerror=function(){}})();
</script>
""";

    private readonly string _outputDir;
    private readonly int _livereloadPort;
    private readonly bool _disableAnalytics;
    private readonly ILogger _logger;

    public DevRequestHandler(string outputDir, int livereloadPort, bool disableAnalytics, ILogger logger)
    {
        _outputDir = outputDir ?? throw new ArgumentNullException(nameof(outputDir));
        _livereloadPort = livereloadPort;
        _disableAnalytics = disableAnalytics;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";

            var candidate = DevPathGuard.TryResolveWithinRoot(_outputDir, path);
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

            var ext = Path.GetExtension(candidate).ToLowerInvariant();
            context.Response.ContentType = ResolveMimeType(ext);

            if (ext == ".html")
            {
                var html = await File.ReadAllTextAsync(candidate, ct).ConfigureAwait(false);
                html = PreviewCommand.ApplyPreviewAnalyticsPolicy(html, _disableAnalytics);
                html = InjectLivereload(html, _livereloadPort);

                var bytes = Encoding.UTF8.GetBytes(html);
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
            try { context.Response.StatusCode = 500; } catch { }
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    /// <summary>
    /// Pure helper exposed for unit tests: injects the livereload script into
    /// <paramref name="html"/> (before <c>&lt;/head&gt;</c> when present, else appended).
    /// </summary>
    internal static string InjectLivereload(string html, int port)
    {
        var script = LivereloadScript.Replace("__PORT__", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var idx = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? html.Insert(idx, script) : html + script;
    }

    private static string ResolveMimeType(string ext) => ext switch
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
}
