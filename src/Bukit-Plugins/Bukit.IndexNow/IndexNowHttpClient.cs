using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Bukit.IndexNow;

public sealed partial class IndexNowHttpClient : IIndexNowTransport
{
    private static readonly Uri Endpoint = new("https://api.indexnow.org/indexnow");
    private readonly HttpClient _client;

    public IndexNowHttpClient(HttpClient? client = null)
    {
        _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
    }

    public async Task<IndexNowPageResponse> GetPageAsync(Uri url, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        string? canonical = null;
        string? body = null;
        if (response.StatusCode == HttpStatusCode.OK)
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = CanonicalLink().Match(body);
            canonical = match.Success ? WebUtility.HtmlDecode(match.Groups["url"].Value) : null;
        }

        return new IndexNowPageResponse((int)response.StatusCode, canonical, body);
    }

    public async Task<IndexNowSubmitResponse> SubmitAsync(
        IndexNowSubmissionPayload payload,
        CancellationToken cancellationToken)
    {
        using var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            host = payload.Host,
            key = payload.Key,
            keyLocation = payload.KeyLocation,
            urlList = payload.Urls
        }, cancellationToken);
        return new IndexNowSubmitResponse((int)response.StatusCode);
    }

    [GeneratedRegex(
        """<link\b[^>]*\brel\s*=\s*["'][^"']*\bcanonical\b[^"']*["'][^>]*\bhref\s*=\s*["'](?<url>[^"']+)["'][^>]*>|<link\b[^>]*\bhref\s*=\s*["'](?<url>[^"']+)["'][^>]*\brel\s*=\s*["'][^"']*\bcanonical\b[^"']*["'][^>]*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalLink();
}
