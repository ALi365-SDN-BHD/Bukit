using System.Net;
using Bukit.Content.Media;
using Bukit.Shared;
using Xunit;
using Bukit.Config;

namespace Bukit.Content.Tests;

public sealed partial class ImageAssetLocalizerTests
{
    private const string SignedNotionFile =
        "https://prod-files-secure.s3.us-west-2.amazonaws.com/space/file/cover.png" +
        "?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Date=20260601T100000Z&X-Amz-Expires=3600&X-Amz-Signature=first";

    [Fact]
    public async Task LocalizeAsync_NotionOffline_ReusesTheMediaCacheAndNeverDownloads()
        => await WithTempDirAsync(async dir =>
        {
            var online = new CountingHandler(HttpStatusCode.OK, "image/png", "cover");
            string localUrl;
            using (var localizer = CreateLocalizer(ControlConfig(dir), online))
            {
                localUrl = await localizer.LocalizeAsync(SignedNotionFile, CancellationToken.None);
            }

            Assert.StartsWith("/assets/uploads/", localUrl, StringComparison.Ordinal);
            Assert.Equal(1, online.RequestCount);

            var offline = new CountingHandler(HttpStatusCode.OK, "image/png", "never");
            var logger = new RecordingLogger();
            using (NotionSnapshotScope.Enter(new OfflineHandle()))
            using (var localizer = CreateLocalizer(ControlConfig(dir), offline, logger))
            {
                // The same file under an expired signature still maps to the cached copy.
                var expired = SignedNotionFile.Replace("X-Amz-Signature=first", "X-Amz-Signature=second", StringComparison.Ordinal)
                    .Replace("20260601T100000Z", "20260101T000000Z", StringComparison.Ordinal);
                Assert.Equal(localUrl, await localizer.LocalizeAsync(expired, CancellationToken.None));

                // Not in the cache: the URL is kept and nothing is downloaded.
                const string uncached = "https://img.example/never-downloaded.png";
                Assert.Equal(uncached, await localizer.LocalizeAsync(uncached, CancellationToken.None));
            }

            Assert.Equal(0, offline.RequestCount);
        });

    [Fact]
    public async Task LocalizeAsync_CloudBaseOfflineCacheMiss_UsesThePlaceholderNotTheGateway()
        => await WithTempDirAsync(async dir =>
        {
            const string recorded = "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/posts/a.png";
            var offline = new CountingHandler(HttpStatusCode.OK, "image/png", "never");
            using (NotionSnapshotScope.Enter(new OfflineHandle()))
            {
                // The media cache directory is missing, then present but without the object.
                using (var localizer = CreateLocalizer(ControlConfig(Path.Combine(dir, "missing")), offline))
                {
                    Assert.Equal("/assets/images/noneimg-news.jpg", await localizer.LocalizeAsync(recorded, CancellationToken.None));
                }

                using (var localizer = CreateLocalizer(ControlConfig(dir), offline))
                {
                    Assert.Equal("/assets/images/noneimg-news.jpg", await localizer.LocalizeAsync(recorded, CancellationToken.None));
                }
            }

            Assert.Equal(0, offline.RequestCount);
        });

    private sealed class OfflineHandle : INotionSnapshotHandle
    {
        public string Directory => "unused";
        public bool Offline => true;
    }
}
