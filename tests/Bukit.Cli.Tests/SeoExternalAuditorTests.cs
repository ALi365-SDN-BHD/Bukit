using System.Reflection;
using System.Text.Json;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class SeoExternalAuditorTests
{
    private static readonly MethodInfo s_extractImageUrls = typeof(SeoExternalAuditor)
        .GetMethod("ExtractImageUrls", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_extractLinks = typeof(SeoExternalAuditor)
        .GetMethod("ExtractLinks", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_analyzeExternalResponse = typeof(SeoExternalAuditor)
        .GetMethod("AnalyzeExternalResponse", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void ExtractImageUrls_OgImage_ReturnsUrl()
    {
        var html = "<meta property=\"og:image\" content=\"https://example.com/img.jpg\">";

        var result = InvokeExtractImageUrls(html);

        Assert.Contains("https://example.com/img.jpg", result);
    }

    [Fact]
    public void ExtractImageUrls_TwitterImage_ReturnsUrl()
    {
        var html = "<meta name=\"twitter:image\" content=\"https://example.com/tweet.png\">";

        var result = InvokeExtractImageUrls(html);

        Assert.Contains("https://example.com/tweet.png", result);
    }

    [Fact]
    public void ExtractImageUrls_ImgTag_ReturnsSrc()
    {
        var html = "<img src=\"https://example.com/photo.jpg\" alt=\"photo\">";

        var result = InvokeExtractImageUrls(html);

        Assert.Contains("https://example.com/photo.jpg", result);
    }

    [Fact]
    public void ExtractImageUrls_HtmlEncoded_ReturnsDecoded()
    {
        var html = "<meta property=\"og:image\" content=\"https://example.com/img%20with%20spaces.jpg\">";

        var result = InvokeExtractImageUrls(html);

        Assert.Contains("https://example.com/img%20with%20spaces.jpg", result);
    }

    [Fact]
    public void ExtractImageUrls_NoImages_ReturnsEmpty()
    {
        var html = "<html><body>no images here</body></html>";

        var result = InvokeExtractImageUrls(html);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractImageUrls_Duplicates_ReturnsUnique()
    {
        var html = "<meta property=\"og:image\" content=\"https://example.com/img.jpg\"><img src=\"https://example.com/img.jpg\">";

        var result = InvokeExtractImageUrls(html);

        Assert.Single(result);
    }

    [Fact]
    public void ExtractLinks_ValidHref_ReturnsAbsolute()
    {
        var html = "<a href=\"/about\">About</a>";

        var result = InvokeExtractLinks(html, "https://example.com/");

        Assert.Contains("https://example.com/about", result);
    }

    [Fact]
    public void ExtractLinks_Mailto_Excluded()
    {
        var html = "<a href=\"mailto:test@example.com\">Email</a>";

        var result = InvokeExtractLinks(html, "https://example.com/");

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractLinks_Tel_Excluded()
    {
        var html = "<a href=\"tel:+123456789\">Call</a>";

        var result = InvokeExtractLinks(html, "https://example.com/");

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractLinks_InvalidCanonical_ReturnsEmpty()
    {
        var html = "<a href=\"/page\">Page</a>";

        var result = InvokeExtractLinks(html, "not-a-valid-url");

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractLinks_AbsoluteExternal_Kept()
    {
        var html = "<a href=\"https://other.com/page\">Other</a>";

        var result = InvokeExtractLinks(html, "https://example.com/");

        Assert.Contains("https://other.com/page", result);
    }

    [Fact]
    public void ExtractLinks_Duplicates_ReturnsUnique()
    {
        var html = "<a href=\"/page\">Link 1</a><a href=\"/page\">Link 2</a>";

        var result = InvokeExtractLinks(html, "https://example.com/");

        Assert.Single(result);
    }

    [Fact]
    public void AnalyzeExternalResponse_Status400_ReturnsTrue()
    {
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);

        var result = (bool)s_analyzeExternalResponse.Invoke(null, new object[] { response, "https://example.com", "label", false, "warning" })!;

        Assert.True(result);
    }

    [Fact]
    public void AnalyzeExternalResponse_Status200_ReturnsFalse()
    {
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        var result = (bool)s_analyzeExternalResponse.Invoke(null, new object[] { response, "https://example.com", "label", false, "warning" })!;

        Assert.False(result);
    }

    [Fact]
    public void AnalyzeExternalResponse_ImageCheckNonImageType_ReturnsTrue()
    {
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html");

        var result = (bool)s_analyzeExternalResponse.Invoke(null, new object[] { response, "https://example.com/img", "label", true, "warning" })!;

        Assert.True(result);
    }

    [Fact]
    public void AnalyzeExternalResponse_ImageCheckImageType_ReturnsFalse()
    {
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        var result = (bool)s_analyzeExternalResponse.Invoke(null, new object[] { response, "https://example.com/img.png", "label", true, "warning" })!;

        Assert.False(result);
    }

    private static IReadOnlyList<string> InvokeExtractImageUrls(string html)
    {
        return (IReadOnlyList<string>)s_extractImageUrls.Invoke(null, new object[] { html })!;
    }

    private static IReadOnlyList<string> InvokeExtractLinks(string html, string canonical)
    {
        return (IReadOnlyList<string>)s_extractLinks.Invoke(null, new object[] { html, canonical })!;
    }
}
