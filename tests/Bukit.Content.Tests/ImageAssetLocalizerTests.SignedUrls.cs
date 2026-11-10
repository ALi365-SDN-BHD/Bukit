using System.Collections.Concurrent;
using System.Net;
using Bukit.Content.Media;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

// fix-media-signed-url-identity: re-signed URLs keep one media file.
public sealed partial class ImageAssetLocalizerTests
{
    private static string S3(string signature, string extra = "")
        => "https://prod-files-secure.s3.us-west-2.amazonaws.com/ws/file-id/a.png" +
           "?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIA%2F20261110%2Fus-west-2%2Fs3%2Faws4_request" +
           $"&X-Amz-Date=20261110T000000Z&X-Amz-Expires=3600&X-Amz-Signature={signature}&X-Amz-SignedHeaders=host&x-id=GetObject{extra}";

    [Theory]
    [InlineData("https://img.example/a.png", "https://img.example/a.png")]
    [InlineData("https://img.example/a.png?", "https://img.example/a.png?")]
    [InlineData("https://img.example/a.png?w=1&&h=2", "https://img.example/a.png?w=1&&h=2")]
    [InlineData("https://s3.example/a.png?X-Amz-Signature=s&X-Amz-Date=d&w=1", "https://s3.example/a.png?w=1")]
    [InlineData("https://s3.example/a.png?x-amz-signature=s&X-AMZ-EXPIRES=1&versionId=v2", "https://s3.example/a.png?versionId=v2")]
    [InlineData("https://s3.example/a.png?X-Amz-Expires=100", "https://s3.example/a.png?X-Amz-Expires=100")]
    [InlineData("https://s3.example/a.png?X-Amz-Signature=s", "https://s3.example/a.png")]
    // Uri already restores escaped unreserved chars (%2D is '-'), which is also what is sent.
    [InlineData("https://s3.example/a.png?X%2DAmz%2DSignature=s&X-Amz-Date=d&w=1", "https://s3.example/a.png?w=1")]
    // Escapes Uri keeps are not decoded, so such a name never matches.
    [InlineData("https://s3.example/a.png?X-Amz-Signature%26=s&X-Amz-Date=d", "https://s3.example/a.png?X-Amz-Signature%26=s&X-Amz-Date=d")]
    [InlineData("https://gcs.example/a.png?X-Goog-Signature=s&X-Goog-Expires=1&alt=media", "https://gcs.example/a.png?alt=media")]
    [InlineData("https://cdn.example/a.png?Expires=1&Signature=s&Key-Pair-Id=k&w=1", "https://cdn.example/a.png?w=1")]
    [InlineData("https://cdn.example/a.png?Expires=1&Signature=s&w=1", "https://cdn.example/a.png?Expires=1&Signature=s&w=1")]
    [InlineData("https://file.notion.so/f/f/id/a.png?table=block&expirationTimestamp=1&signature=s", "https://file.notion.so/f/f/id/a.png?table=block")]
    [InlineData("https://x.notion.site/image/a.png?signature=s", "https://x.notion.site/image/a.png")]
    [InlineData("https://img.example/a.png?signature=s&expirationTimestamp=1", "https://img.example/a.png?signature=s&expirationTimestamp=1")]
    [InlineData("https://file.notion.so/f/a.png?table=block", "https://file.notion.so/f/a.png?table=block")]
    public void IdentityTarget_StripsOnlyRecognizedSignatureParameters(string url, string expected)
        => Assert.Equal(expected, SignedUrlIdentity.IdentityTarget(new Uri(url)));

    [Fact]
    public async Task LocalizeAsync_ReSignedS3Url_ReusesFileAcrossBuildsWithoutRequest()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.OK);
            string first;
            using (var localizer = CreateLocalizer(ControlConfig(dir), handler))
            {
                first = await localizer.LocalizeAsync(S3("aaa"), CancellationToken.None);
                Assert.Equal(first, await localizer.LocalizeAsync(S3("bbb"), CancellationToken.None));
            }

            using (var nextBuild = CreateLocalizer(ControlConfig(dir), handler))
            {
                Assert.Equal(first, await nextBuild.LocalizeAsync(S3("ccc"), CancellationToken.None));
            }

            Assert.Equal(1, handler.RequestCount);
            Assert.Contains("X-Amz-Signature=aaa", handler.Urls.Single(), StringComparison.Ordinal);
            Assert.Contains("x-id=GetObject", handler.Urls.Single(), StringComparison.Ordinal);
            var index = File.ReadAllText(Path.Combine(dir, ".media-index.json"));
            Assert.DoesNotContain("aaa", index, StringComparison.Ordinal);
        });

    // A readonly Notion cache keeps an expired signed URL; the file an earlier version stored under
    // the full signed URL must still serve it after the key change, with no request.
    [Fact]
    public async Task LocalizeAsync_FileStoredUnderLegacySignedKey_IsReusedWithoutRequest()
        => await WithTempDirAsync(async dir =>
        {
            var expired = S3("expired");
            var legacyKey = "v3:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(SignedUrlIdentity.RequestTarget(new Uri(expired)))));
            var legacyName = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(legacyKey))) + ".png";
            File.WriteAllBytes(Path.Combine(dir, legacyName), CreateImagePayload("image/png", "signed"));
            var handler = new SignedScriptHandler(_ => HttpStatusCode.Forbidden);

            using (var localizer = CreateLocalizer(ControlConfig(dir), handler))
            {
                Assert.Equal($"/assets/uploads/{legacyName}", await localizer.LocalizeAsync(expired, CancellationToken.None));
                Assert.Equal($"/assets/uploads/{legacyName}", await localizer.LocalizeAsync(S3("other"), CancellationToken.None));
                Assert.Empty(localizer.Failures);
            }

            using (var nextBuild = CreateLocalizer(ControlConfig(dir), handler))
            {
                Assert.Equal($"/assets/uploads/{legacyName}", await nextBuild.LocalizeAsync(S3("later"), CancellationToken.None));
            }

            Assert.Equal(0, handler.RequestCount);
        });

    // PagesIndexPlugin localizes page fields with its own localizer next to the body localizer;
    // both must land on the same file when they see different signatures of one object.
    [Fact]
    public async Task LocalizeAsync_TwoLiveLocalizers_DifferentSignaturesShareFile()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.OK);
            using var bodies = CreateLocalizer(ControlConfig(dir), handler);
            using var pageFields = CreateLocalizer(ControlConfig(dir), new SignedScriptHandler(_ => HttpStatusCode.OK));

            var body = await bodies.LocalizeAsync(S3("body"), CancellationToken.None);
            var field = await pageFields.LocalizeAsync(S3("field"), CancellationToken.None);

            Assert.Equal(body, field);
            Assert.Equal(1, handler.RequestCount);
        });

    [Fact]
    public async Task LocalizeAsync_NotionFileProxy_SignatureChangesKeepFile()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.OK);
            using var localizer = CreateLocalizer(ControlConfig(dir), handler);

            var a = await localizer.LocalizeAsync("https://file.notion.so/f/f/ws/id/a.png?table=block&id=b1&expirationTimestamp=100&signature=one", CancellationToken.None);
            var b = await localizer.LocalizeAsync("https://file.notion.so/f/f/ws/id/a.png?table=block&id=b1&expirationTimestamp=200&signature=two", CancellationToken.None);

            Assert.Equal(a, b);
            Assert.Equal(1, handler.RequestCount);
        });

    [Theory]
    [InlineData("&versionId=1", "&versionId=2")]
    [InlineData("&w=100", "&w=200")]
    public async Task LocalizeAsync_DifferentObjectParameters_UseDistinctFiles(string first, string second)
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.OK);
            using var localizer = CreateLocalizer(ControlConfig(dir), handler);

            var a = await localizer.LocalizeAsync(S3("aaa", first), CancellationToken.None);
            var b = await localizer.LocalizeAsync(S3("bbb", second), CancellationToken.None);

            Assert.NotEqual(a, b);
            Assert.Equal(2, handler.RequestCount);
        });

    [Theory]
    [InlineData("https://img.example/a.png?signature=one", "https://img.example/a.png?signature=two")]
    [InlineData("https://s3.example/a.png?X-Amz-Expires=100", "https://s3.example/a.png?X-Amz-Expires=200")]
    [InlineData("https://s3.example/a.png?X-Amz-Signature%26=one", "https://s3.example/a.png?X-Amz-Signature%26=two")]
    public async Task LocalizeAsync_UnrecognizedSignatureShapes_StayDistinct(string first, string second)
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.OK);
            using var localizer = CreateLocalizer(ControlConfig(dir), handler);

            Assert.NotEqual(
                await localizer.LocalizeAsync(first, CancellationToken.None),
                await localizer.LocalizeAsync(second, CancellationToken.None));
            Assert.Equal(2, handler.RequestCount);
        });

    [Fact]
    public async Task LocalizeAsync_UnsignedFileCachedBeforeUpgrade_KeepsNameWithoutRequest()
        => await WithTempDirAsync(async dir =>
        {
            const string source = "https://img.example/path/a.png?w=100";
            var fileName = BuildExpectedMediaFileName(source, ".png");
            File.WriteAllBytes(Path.Combine(dir, fileName), CreateImagePayload("image/png", "cached"));
            var handler = new SignedScriptHandler(_ => HttpStatusCode.OK);
            using var localizer = CreateLocalizer(ControlConfig(dir), handler);

            Assert.Equal("/assets/uploads/" + fileName, await localizer.LocalizeAsync(source, CancellationToken.None));
            Assert.Equal(0, handler.RequestCount);
        });

    [Fact]
    public async Task LocalizeAsync_ExpiredSignatureFirst_JoinerRetriesOwnUrlOnce()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(url => url.Contains("=old", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
            var hold = handler.Hold("=old");
            var logger = new SharedEventLogger();
            using var localizer = CreateLocalizer(ControlConfig(dir, "keep"), handler, logger);

            var expired = localizer.LocalizeAsync(S3("old"), CancellationToken.None);
            await hold.Started.WaitAsync(TimeSpan.FromSeconds(5));
            var fresh = localizer.LocalizeAsync(S3("new"), CancellationToken.None);
            await logger.WaitForSharedAsync(1);
            hold.Release();

            Assert.Equal(S3("old"), await expired);
            var local = await fresh;
            Assert.StartsWith("/assets/uploads/", local, StringComparison.Ordinal);
            Assert.Equal(2, handler.RequestCount);
            Assert.Single(localizer.Failures);
            // A later reference to the expired URL now finds the local file.
            Assert.Equal(local, await localizer.LocalizeAsync(S3("old"), CancellationToken.None));
            Assert.Equal(2, handler.RequestCount);
        });

    [Fact]
    public async Task LocalizeAsync_FreshSignatureFirst_ExpiredUrlHitsCacheWithoutFailure()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(url => url.Contains("=old", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
            using var localizer = CreateLocalizer(ControlConfig(dir, "keep"), handler);

            var fresh = await localizer.LocalizeAsync(S3("new"), CancellationToken.None);

            Assert.Equal(fresh, await localizer.LocalizeAsync(S3("old"), CancellationToken.None));
            Assert.Equal(1, handler.RequestCount);
            Assert.Empty(localizer.Failures);
        });

    [Fact]
    public async Task LocalizeAsync_SharedNotFound_ReportsEachTargetOnceWithoutExtraRequests()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.NotFound);
            var hold = handler.Hold("=one");
            var logger = new SharedEventLogger();
            using var localizer = CreateLocalizer(ControlConfig(dir, "keep"), handler, logger);

            var first = localizer.LocalizeAsync(S3("one"), CancellationToken.None);
            await hold.Started.WaitAsync(TimeSpan.FromSeconds(5));
            var second = localizer.LocalizeAsync(S3("two"), CancellationToken.None);
            var third = localizer.LocalizeAsync(S3("three"), CancellationToken.None);
            await logger.WaitForSharedAsync(2);
            hold.Release();

            Assert.Equal(S3("one"), await first);
            Assert.Equal(S3("two"), await second);
            Assert.Equal(S3("three"), await third);
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(3, localizer.Failures.Count);

            // A joiner's URL seen again is neither requested nor reported again.
            Assert.Equal(S3("two"), await localizer.LocalizeAsync(S3("two"), CancellationToken.None));
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(3, localizer.Failures.Count);
            Assert.Contains(logger.Warnings, w => w.StartsWith("event=media.failed_reference", StringComparison.Ordinal));
        });

    [Fact]
    public async Task LocalizeAsync_ExpiredRefusal_ManyFreshSignaturesRequestOnce()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(url => url.Contains("=old", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
            var hold = handler.Hold("=old");
            var logger = new SharedEventLogger();
            using var localizer = CreateLocalizer(ControlConfig(dir, "keep"), handler, logger);

            var expired = localizer.LocalizeAsync(S3("old"), CancellationToken.None);
            await hold.Started.WaitAsync(TimeSpan.FromSeconds(5));
            var fresh = Enumerable.Range(0, 4).Select(i => localizer.LocalizeAsync(S3($"new{i}"), CancellationToken.None)).ToArray();
            await logger.WaitForSharedAsync(4);
            hold.Release();

            await expired;
            var results = await Task.WhenAll(fresh);
            Assert.Single(results.Distinct());
            Assert.Equal(2, handler.RequestCount);
            Assert.Single(localizer.Failures);
        });

    [Fact]
    public async Task LocalizeAsync_TwoWaitersWithSameExpiredUrl_RequestAndReportOnce()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(_ => HttpStatusCode.Forbidden);
            var hold = handler.Hold("=first");
            var logger = new SharedEventLogger();
            using var localizer = CreateLocalizer(ControlConfig(dir, "keep"), handler, logger);

            var first = localizer.LocalizeAsync(S3("first"), CancellationToken.None);
            await hold.Started.WaitAsync(TimeSpan.FromSeconds(5));
            var a = localizer.LocalizeAsync(S3("old"), CancellationToken.None);
            var b = localizer.LocalizeAsync(S3("old"), CancellationToken.None);
            await logger.WaitForSharedAsync(2);
            hold.Release();

            await first;
            Assert.Equal(S3("old"), await a);
            Assert.Equal(S3("old"), await b);
            Assert.Equal(2, handler.RequestCount);
            Assert.Equal(2, localizer.Failures.Count);
        });

    [Fact]
    public async Task LocalizeAsync_CanceledWhileWaitingForRetry_RecordsNoFailure()
        => await WithTempDirAsync(async dir =>
        {
            var handler = new SignedScriptHandler(url => url.Contains("=old", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
            var holdOld = handler.Hold("=old");
            var holdB = handler.Hold("Signature=b&");
            var holdC = handler.Hold("Signature=c&");
            var logger = new SharedEventLogger();
            using var localizer = CreateLocalizer(ControlConfig(dir, "keep"), handler, logger);
            using var cancelB = new CancellationTokenSource();
            using var cancelC = new CancellationTokenSource();

            var expired = localizer.LocalizeAsync(S3("old"), CancellationToken.None);
            await holdOld.Started.WaitAsync(TimeSpan.FromSeconds(5));
            var b = localizer.LocalizeAsync(S3("b"), cancelB.Token);
            var c = localizer.LocalizeAsync(S3("c"), cancelC.Token);
            await logger.WaitForSharedAsync(2);
            holdOld.Release();
            await expired;

            // Both joiners were refused; one retries its own URL (held), the other waits behind it.
            var started = await Task.WhenAny(holdB.Started, holdC.Started).WaitAsync(TimeSpan.FromSeconds(5));
            var (retrying, waiting, cancelWaiting) = started == holdB.Started ? (b, c, cancelC) : (c, b, cancelB);
            await Task.Delay(50);
            cancelWaiting.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            holdB.Release();
            holdC.Release();

            Assert.StartsWith("/assets/uploads/", await retrying, StringComparison.Ordinal);
            Assert.Single(localizer.Failures);
            Assert.Equal(2, handler.RequestCount);
        });

    // Responds per URL; requests whose URL contains a held marker wait until released.
    private sealed class SignedScriptHandler(Func<string, HttpStatusCode> status) : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, HoldGate> _holds = new(StringComparer.Ordinal);

        public ConcurrentQueue<string> Urls { get; } = new();

        public int RequestCount => Urls.Count;

        public HoldGate Hold(string marker) => _holds.GetOrAdd(marker, _ => new HoldGate());

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Urls.Enqueue(url);
            foreach (var (marker, gate) in _holds)
            {
                if (url.Contains(marker, StringComparison.Ordinal))
                {
                    await gate.EnterAsync(cancellationToken);
                }
            }

            var code = status(url);
            return code == HttpStatusCode.OK ? Response(code, "image/png", "signed") : new HttpResponseMessage(code);
        }
    }

    private sealed class HoldGate
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public void Release() => _release.TrySetResult();

        public Task EnterAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            return _release.Task.WaitAsync(cancellationToken);
        }
    }

    // Counts callers that joined a running download, so a test can release it once all have joined.
    private sealed class SharedEventLogger : ILogger
    {
        private int _shared;

        public ConcurrentQueue<string> Warnings { get; } = new();

        public async Task WaitForSharedAsync(int count)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (Volatile.Read(ref _shared) < count)
            {
                Assert.True(DateTime.UtcNow < deadline, $"expected {count} shared downloads, saw {_shared}");
                await Task.Delay(10);
            }
        }

        public void Debug(string message)
        {
            if (message.StartsWith(ImageAssetLocalizer.SharedDownloadEvent, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _shared);
            }
        }

        public void Info(string message) { }
        public void Warn(string message) => Warnings.Enqueue(message);
        public void Error(string message) { }
    }
}
