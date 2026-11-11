using System.Net;
using Bukit.Content.Media;
using Xunit;

namespace Bukit.Content.Tests;

// fix-media-expired-signature-fail-fast: a refused URL whose signature expired is not retried.
public sealed partial class ImageAssetLocalizerTests
{
    // Signed far in the future, so it has not expired when the tests run.
    private static string S3Live(string signature)
        => "https://prod-files-secure.s3.us-west-2.amazonaws.com/ws/file-id/a.png" +
           "?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIA%2F20991231%2Fus-west-2%2Fs3%2Faws4_request" +
           $"&X-Amz-Date=20991231T000000Z&X-Amz-Expires=3600&X-Amz-Signature={signature}&X-Amz-SignedHeaders=host&x-id=GetObject";

    private const string ExpiredNotionUrl =
        "https://file.notion.so/f/f/ws/id/a.png?table=block&id=b1&expirationTimestamp=100&signature=one";

    [Theory]
    [InlineData("https://s3.example/a.png?X-Amz-Signature=s&X-Amz-Date=20261110T000000Z&X-Amz-Expires=3600", "2026-11-10T01:00:00+00:00")]
    [InlineData("https://s3.example/a.png?x-amz-signature=s&x-amz-date=20261110T000000Z&x-amz-expires=60", "2026-11-10T00:01:00+00:00")]
    [InlineData("https://gcs.example/a.png?X-Goog-Signature=s&X-Goog-Date=20261110T120000Z&X-Goog-Expires=900", "2026-11-10T12:15:00+00:00")]
    [InlineData("https://cdn.example/a.png?Expires=1762732800&Signature=s&Key-Pair-Id=k", "2025-11-10T00:00:00+00:00")]
    [InlineData("https://file.notion.so/f/a.png?expirationTimestamp=1762732800000&signature=s", "2025-11-10T00:00:00+00:00")]
    public void TryGetSignatureExpiry_ReadsRecognizedSchemes(string url, string expected)
    {
        Assert.True(SignedUrlIdentity.TryGetSignatureExpiry(new Uri(url), out var expiry));
        Assert.Equal(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), expiry);
    }

    [Theory]
    [InlineData("https://img.example/a.png")]
    [InlineData("https://img.example/a.png?w=100")]
    [InlineData("https://s3.example/a.png?X-Amz-Date=20261110T000000Z&X-Amz-Expires=3600")]
    [InlineData("https://s3.example/a.png?X-Amz-Signature=s&X-Amz-Expires=3600")]
    [InlineData("https://s3.example/a.png?X-Amz-Signature=s&X-Amz-Date=bad&X-Amz-Expires=3600")]
    [InlineData("https://s3.example/a.png?X-Amz-Signature=s&X-Amz-Date=20261110T000000Z&X-Amz-Expires=-1")]
    [InlineData("https://s3.example/a.png?X-Amz-Signature=s&X-Amz-Date=99991231T235959Z&X-Amz-Expires=3600")]
    [InlineData("https://cdn.example/a.png?Expires=1&Signature=s")]
    [InlineData("https://img.example/a.png?expirationTimestamp=1&signature=s")]
    [InlineData("https://file.notion.so/f/a.png?expirationTimestamp=99999999999999999999&signature=s")]
    public void TryGetSignatureExpiry_UnknownOrUnreadable_ReturnsFalse(string url)
        => Assert.False(SignedUrlIdentity.TryGetSignatureExpiry(new Uri(url), out _));

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task LocalizeAsync_ExpiredSignatureRefused_FailsWithoutRetry(HttpStatusCode status)
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => status);
            var logger = new SharedEventLogger();
            using var localizer = CreateLocalizer(ControlConfig(dir) with { MaxRetries = 3 }, handler, logger);

            Assert.Equal("/assets/images/noneimg-news.jpg", await localizer.LocalizeAsync(S3("old"), CancellationToken.None));

            Assert.Equal(1, handler.RequestCount);
            var failure = Assert.Single(localizer.Failures);
            Assert.Equal($"HTTP {(int)status}", failure.Reason);
            Assert.Contains(logger.Warnings, w => w.Contains("signature=expired", StringComparison.Ordinal));
        });

    [Theory]
    [InlineData("placeholder")]
    [InlineData("keep")]
    public async Task LocalizeAsync_ExpiredNotionUrlRefused_KeepsOnFailureWithOneRequest(string onFailure)
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.Forbidden);
            using var localizer = CreateLocalizer(ControlConfig(dir, onFailure) with { MaxRetries = 3 }, handler);

            var result = await localizer.LocalizeAsync(ExpiredNotionUrl, CancellationToken.None);

            Assert.Equal(onFailure == "keep" ? ExpiredNotionUrl : "/assets/images/noneimg-news.jpg", result);
            Assert.Equal(1, handler.RequestCount);
            Assert.Single(localizer.Failures);
        });

    // Refusals that may be transient keep the configured retries.
    [Theory]
    [InlineData("live")]
    [InlineData("unsigned")]
    public async Task LocalizeAsync_RefusalWithoutExpiredSignature_KeepsRetries(string kind)
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.Forbidden);
            using var localizer = CreateLocalizer(ControlConfig(dir) with { MaxRetries = 3 }, handler);
            var source = kind == "live" ? S3Live("sig") : "https://img.example/a.png";

            await localizer.LocalizeAsync(source, CancellationToken.None);

            Assert.Equal(4, handler.RequestCount);
            Assert.Single(localizer.Failures);
        });

    [Fact]
    public async Task LocalizeAsync_ExpiredSignatureServerError_KeepsRetries()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.ServiceUnavailable);
            using var localizer = CreateLocalizer(ControlConfig(dir) with { MaxRetries = 3 }, handler);

            await localizer.LocalizeAsync(S3("old"), CancellationToken.None);

            Assert.Equal(4, handler.RequestCount);
        });

    // A readonly cache mixes images saved by an earlier build with expired links to new ones: the
    // saved file is reused with no request and the unsaved image fails after a single request.
    [Fact]
    public async Task LocalizeAsync_ReadonlyCacheExpiredLinks_ReusesSavedFileAndFailsFastOnRest()
        => await WithTempDirAsync(async dir =>
        {
            var saved = S3("saved");
            var legacyKey = "v3:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(SignedUrlIdentity.RequestTarget(new Uri(saved)))));
            var legacyName = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(legacyKey))) + ".png";
            File.WriteAllBytes(Path.Combine(dir, legacyName), CreateImagePayload("image/png", "signed"));
            var handler = new SignedScriptHandler(_ => HttpStatusCode.Forbidden);
            using var localizer = CreateLocalizer(ControlConfig(dir, "keep") with { MaxRetries = 3 }, handler);

            Assert.Equal($"/assets/uploads/{legacyName}", await localizer.LocalizeAsync(saved, CancellationToken.None));
            Assert.Equal(0, handler.RequestCount);

            Assert.Equal(ExpiredNotionUrl, await localizer.LocalizeAsync(ExpiredNotionUrl, CancellationToken.None));
            Assert.Equal(1, handler.RequestCount);
            Assert.Single(localizer.Failures);

            // Seen again in the same build: neither requested nor reported again.
            Assert.Equal(ExpiredNotionUrl, await localizer.LocalizeAsync(ExpiredNotionUrl, CancellationToken.None));
            Assert.Equal(1, handler.RequestCount);
            Assert.Single(localizer.Failures);
        });
}
