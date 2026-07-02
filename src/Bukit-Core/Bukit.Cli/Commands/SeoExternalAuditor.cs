using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class SeoExternalAuditor
{
    internal static async Task<(int Errors, int Warnings)> RunExternalAuditAsync(JsonElement report, string outputDir)
    {
        var ssrfHandler = new System.Net.Http.SocketsHttpHandler
        {
            ConnectCallback = SsrfGuard.SsrfSafeConnectAsync
        };
        using var http = new HttpClient(ssrfHandler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("bukit-seo-audit/1.0");
        var errors = 0;
        var warnings = 0;
        var checkedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in report.GetProperty("routes").EnumerateArray())
        {
            var routeUrl = SeoReportValidator.ReadRequiredString(route, "route", "url");
            var canonical = SeoReportValidator.ReadRequiredString(route, "route", "canonical");
            if (await CheckUrlAsync(http, canonical, $"canonical {routeUrl}", checkedUrls, severity: "error"))
            {
                errors++;
            }

            var outputPath = Path.Combine(outputDir, SeoReportValidator.ReadRequiredString(route, "route", "outputPath"));
            if (!File.Exists(outputPath))
            {
                continue;
            }

            var html = File.ReadAllText(outputPath);
            foreach (var image in ExtractImageUrls(html))
            {
                var result = await CheckUrlAsync(http, image, $"image {routeUrl}", checkedUrls, requireImage: true);
                if (result)
                {
                    warnings++;
                }
            }

            foreach (var link in ExtractLinks(html, canonical))
            {
                var result = await CheckUrlAsync(http, link, $"link {routeUrl}", checkedUrls);
                if (result)
                {
                    warnings++;
                }
            }
        }

        return (errors, warnings);
    }

    private static async Task<bool> CheckUrlAsync(HttpClient http, string url, string label, HashSet<string> checkedUrls, bool requireImage = false, string severity = "warning")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not "http" and not "https" ||
            !checkedUrls.Add((requireImage ? "image:" : "url:") + url))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, uri);
                using var getResponse = await http.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                return AnalyzeExternalResponse(getResponse, url, label, requireImage, severity);
            }

            return AnalyzeExternalResponse(response, url, label, requireImage, severity);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"external {severity} seo.external_fetch_failed - {label} {url} error={ex.GetType().Name}");
            return true;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"external {severity} seo.external_fetch_timeout - {label} {url}");
            return true;
        }
    }

    private static bool AnalyzeExternalResponse(HttpResponseMessage response, string url, string label, bool requireImage, string severity)
    {
        if ((int)response.StatusCode >= 400)
        {
            Console.WriteLine($"external {severity} seo.external_http_status - {label} {url} status={(int)response.StatusCode}");
            return true;
        }

        if (requireImage && response.Content.Headers.ContentType?.MediaType is { } mediaType &&
            !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"external {severity} seo.external_image_mime - {label} {url} contentType={mediaType}");
            return true;
        }

        Console.WriteLine($"external ok {label} {url} status={(int)response.StatusCode}");
        return false;
    }

    private static IReadOnlyList<string> ExtractImageUrls(string html)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SeoReportValidator.SocialImageRegex().Matches(html))
        {
            values.Add(WebUtility.HtmlDecode(match.Groups[1].Value));
        }

        foreach (Match match in SeoReportValidator.ImageSourceRegex().Matches(html))
        {
            values.Add(WebUtility.HtmlDecode(match.Groups[1].Value));
        }

        return values.Where(SeoReportValidator.IsHttpUrl).ToArray();
    }

    private static IReadOnlyList<string> ExtractLinks(string html, string canonical)
    {
        if (!Uri.TryCreate(canonical, UriKind.Absolute, out var baseUri))
        {
            return Array.Empty<string>();
        }

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SeoReportValidator.AnchorHrefRegex().Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups[1].Value);
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(baseUri, href, out var absolute) ||
                absolute.Scheme is not "http" and not "https")
            {
                continue;
            }

            values.Add(absolute.ToString());
        }

        return values.ToArray();
    }
}
