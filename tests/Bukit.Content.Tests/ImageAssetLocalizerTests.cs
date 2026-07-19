using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using Bukit.Content.Media;
using Bukit.Shared;
using Bukit.Config;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ImageAssetLocalizerTests
{
    [Fact]
    public async Task LocalizeAsync_WhenTokenAlreadyCanceled_ThrowsBeforeRemoteSideEffects()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = dir,
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };
        var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
        using var http = new HttpClient(handler);
        using var localizer = new ImageAssetLocalizer(cfg, http);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => localizer.LocalizeAsync("https://img.example/a.jpg", cancellation.Token));

            Assert.False(Directory.Exists(dir));
            Assert.Equal(0, handler.RequestCount);
            Assert.Empty(localizer.Failures);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_WhenTokenAlreadyCanceledAndUrlIsLocal_ThrowsWithoutLogging()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };
        var logger = new RecordingLogger();
        using var localizer = new ImageAssetLocalizer(cfg, logger);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => localizer.LocalizeAsync("/assets/style.png", cancellation.Token));

        Assert.Empty(logger.Debugs);
        Assert.Empty(logger.Warnings);
        Assert.Empty(localizer.Failures);
    }

    [Fact]
    public async Task LocalizeAsync_WhenSourceMissing_ReturnsDefaultImage()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var localizer = new ImageAssetLocalizer(cfg);
        var result = await localizer.LocalizeAsync(null, CancellationToken.None);

        Assert.Equal("/assets/images/noneimg-news.jpg", result);
    }

    [Fact]
    public async Task LocalizeAsync_WhenDownloadSuccess_SavesAndReturnsLocalUrl()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
            using var http = new HttpClient(handler);
            string result;
            using (var localizer = new ImageAssetLocalizer(cfg, http))
            {
                result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);
                Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
                Assert.Equal(1, handler.RequestCount);
            }

            Assert.Equal(2, Directory.GetFiles(dir).Length); // image + .media-index.json
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_WhenDownloadSuccess_StreamsToTempFileBeforeCompleting()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var stream = new DirectoryObservingReadStream(
                dir,
                Encoding.UTF8.GetBytes("fake-image-streaming-payload"),
                chunkSize: 4);
            var handler = new StreamingHandler("image/jpeg", stream);
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);

            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            Assert.True(stream.SawDownloadFileBeforeSecondRead);
            Assert.DoesNotContain(Directory.GetFiles(dir), path => path.EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_WhenDownloadFails_ReturnsDefaultImage()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg",
            RetryBaseDelayMs = 0 // no delay to keep test fast
        };

        using var http = new HttpClient(new CountingHandler(HttpStatusCode.InternalServerError, "text/plain", "error"));
        using var localizer = new ImageAssetLocalizer(cfg, http);
        var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

        Assert.Equal("/assets/images/noneimg-news.jpg", result);
    }

    [Fact]
    public async Task LocalizeAsync_WhenUrlIsAlreadyLocal_ReturnsOriginal()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var logger = new RecordingLogger();
        using var localizer = new ImageAssetLocalizer(cfg, logger);
        var result = await localizer.LocalizeAsync("/assets/style.png", CancellationToken.None);

        Assert.Equal("/assets/style.png", result);
        Assert.Empty(logger.Warnings);
        Assert.Single(logger.Debugs);
        Assert.Contains("event=media.skip_local", logger.Debugs[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalizeAsync_WhenUrlIsNonHttpExternalReference_StillWarns()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var logger = new RecordingLogger();
        using var localizer = new ImageAssetLocalizer(cfg, logger);
        var result = await localizer.LocalizeAsync("data:image/svg+xml,<svg></svg>", CancellationToken.None);

        Assert.Equal("data:image/svg+xml,<svg></svg>", result);
        Assert.Single(logger.Warnings);
        Assert.Contains("event=media.skip_non_http", logger.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalizeAsync_WhenDownloadDisabled_ReturnsRemoteUrl()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = false,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        using var localizer = new ImageAssetLocalizer(cfg);
        var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

        Assert.Equal("https://img.example/a.jpg", result);
    }

    [Fact]
    public async Task LocalizeAsync_WhenDownloadConfigMissing_ReturnsDefaultImage()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = "",
            UrlBase = "",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        using var localizer = new ImageAssetLocalizer(cfg);
        var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

        Assert.Equal("/assets/images/noneimg-news.jpg", result);
    }

    [Fact]
    public async Task LocalizeAsync_SamePathDifferentQuery_UsesSameStoredFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
            using var http = new HttpClient(handler);
            string a;
            string b;
            using (var localizer = new ImageAssetLocalizer(cfg, http))
            {
                a = await localizer.LocalizeAsync("https://img.example/path/a.jpg?X-Amz-Expires=100", CancellationToken.None);
                b = await localizer.LocalizeAsync("https://img.example/path/a.jpg?X-Amz-Expires=200", CancellationToken.None);
                Assert.Equal(a, b);
                Assert.Equal(1, handler.RequestCount);
            }

            Assert.Equal(2, Directory.GetFiles(dir).Length); // image + .media-index.json
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_ConcurrentSameUrl_StartsExactlyOneDownload()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            using var startGate = new ManualResetEventSlim();
            using var handler = new ConcurrentStartHandler("image/jpeg", "fake-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var tasks = Enumerable.Range(0, 16)
                .Select(_ => Task.Factory.StartNew(
                    async () =>
                    {
                        startGate.Wait();
                        return await localizer.LocalizeAsync(
                            "https://img.example/path/a.jpg",
                            CancellationToken.None);
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap())
                .ToArray();

            startGate.Set();
            var results = await Task.WhenAll(tasks);

            Assert.Single(results.Distinct(StringComparer.Ordinal));
            Assert.All(results, result =>
                Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal));
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_WhenSharedOwnerCancels_OnlyOwnerWaitIsCanceled()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new ControlledResponseHandler("image/jpeg", "fake-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            using var ownerCancellation = new CancellationTokenSource();
            var owner = localizer.LocalizeAsync(
                "https://img.example/path/a.jpg",
                ownerCancellation.Token);
            await handler.RequestStarted;
            var joiner = localizer.LocalizeAsync(
                "https://img.example/path/a.jpg",
                CancellationToken.None);

            ownerCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner);
            handler.ReleaseResponse();
            var joinedResult = await joiner;

            Assert.StartsWith("/assets/uploads/", joinedResult, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_WhenSharedJoinerCancels_OwnerStillCompletes()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new ControlledResponseHandler("image/jpeg", "fake-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var owner = localizer.LocalizeAsync(
                "https://img.example/path/a.jpg",
                CancellationToken.None);
            await handler.RequestStarted;
            using var joinerCancellation = new CancellationTokenSource();
            var joiner = localizer.LocalizeAsync(
                "https://img.example/path/a.jpg",
                joinerCancellation.Token);

            joinerCancellation.Cancel();
            handler.ReleaseResponse();
            var ownerResult = await owner;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => joiner);

            Assert.StartsWith("/assets/uploads/", ownerResult, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Dispose_CancelsOwnedInflightDownload()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = dir,
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };
        var stream = new CancellableBlockingReadStream(Encoding.UTF8.GetBytes("fake-image"));
        var handler = new StreamingHandler("image/jpeg", stream);
        using var http = new HttpClient(handler);
        var localizer = new ImageAssetLocalizer(cfg, http);
        Task<string>? localization = null;
        try
        {
            localization = localizer.LocalizeAsync(
                "https://img.example/path/a.jpg",
                CancellationToken.None);
            await stream.ReadStarted;
            Assert.Contains(
                Directory.GetFiles(dir),
                path => path.EndsWith(".tmp", StringComparison.Ordinal));

            localizer.Dispose();
            var exception = await Record.ExceptionAsync(
                async () => await localization.WaitAsync(TimeSpan.FromSeconds(1)));

            Assert.IsAssignableFrom<OperationCanceledException>(exception);
            Assert.DoesNotContain(
                Directory.GetFiles(dir),
                path => path.EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            stream.ReleaseRead();
            if (localization is not null)
            {
                try
                {
                    await localization;
                }
                catch (OperationCanceledException)
                {
                }
            }

            localizer.Dispose();
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_SecondBuild_HitsDiskIndexAndSkipsDownload()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var firstHandler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
            using (var firstHttp = new HttpClient(firstHandler))
            using (var first = new ImageAssetLocalizer(cfg, firstHttp))
            {
                var a = await first.LocalizeAsync("https://img.example/path/a?token=one", CancellationToken.None);
                Assert.StartsWith("/assets/uploads/", a, StringComparison.Ordinal);
            }

            var secondHandler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
            using (var secondHttp = new HttpClient(secondHandler))
            using (var second = new ImageAssetLocalizer(cfg, secondHttp))
            {
                var b = await second.LocalizeAsync("https://img.example/path/a?token=two", CancellationToken.None);
                Assert.StartsWith("/assets/uploads/", b, StringComparison.Ordinal);
            }

            Assert.Equal(1, firstHandler.RequestCount);
            Assert.Equal(0, secondHandler.RequestCount);
            Assert.Equal(2, Directory.GetFiles(dir).Length); // image + .media-index.json
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_CorruptIndex_StillDownloadsSuccessfully()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".media-index.json"), "{not-json");
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("https://img.example/path/a.jpg?token=one", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // ── New tests for security hardening ────────────────────────────────

    [Fact]
    public async Task LocalizeAsync_NonImageContentType_ReturnsDefaultImage()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "text/html", "<html>evil</html>");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            Assert.Empty(Directory.GetFiles(dir)); // nothing saved
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_ApplicationOctetStream_IsAllowed()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "application/octet-stream", "binary-image-data");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("https://img.example/a.png", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_MissingContentType_IsAllowedAndFallsBackToUrlExtension()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new NullContentTypeHandler("image-bytes");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("https://img.example/a.webp", CancellationToken.None);

            Assert.EndsWith(".webp", result, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_OversizedResponse_ReturnsDefaultImage()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg",
                MaxFileSizeBytes = 10 // very small limit for testing
            };

            // Payload exceeds 10 bytes
            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "this-is-definitely-more-than-10-bytes");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            Assert.Empty(Directory.GetFiles(dir)); // nothing saved
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_PrivateIpUrl_ReturnsDefaultImage()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg",
            BlockPrivateNetworks = true
        };

        // The injected HttpClient path uses pre-flight DNS check.
        // 127.0.0.1 parses directly as private.
        using var http = new HttpClient(new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image"));
        using var localizer = new ImageAssetLocalizer(cfg, http);
        var result = await localizer.LocalizeAsync("http://127.0.0.1:8080/secret.jpg", CancellationToken.None);

        Assert.Equal("/assets/images/noneimg-news.jpg", result);
    }

    [Fact]
    public async Task LocalizeAsync_PrivateIpUrl_10Network_ReturnsDefaultImage()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg",
            BlockPrivateNetworks = true
        };

        using var http = new HttpClient(new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image"));
        using var localizer = new ImageAssetLocalizer(cfg, http);
        var result = await localizer.LocalizeAsync("http://10.0.0.1/image.jpg", CancellationToken.None);

        Assert.Equal("/assets/images/noneimg-news.jpg", result);
    }

    [Fact]
    public async Task LocalizeAsync_CloudMetadataUrl_ReturnsDefaultImage()
    {
        var cfg = new MediaConfig
        {
            DownloadToLocal = true,
            DownloadDir = Path.GetTempPath(),
            UrlBase = "/assets/uploads",
            DefaultImageUrl = "/assets/images/noneimg-news.jpg",
            BlockPrivateNetworks = true
        };

        using var http = new HttpClient(new CountingHandler(HttpStatusCode.OK, "image/jpeg", "metadata"));
        using var localizer = new ImageAssetLocalizer(cfg, http);
        var result = await localizer.LocalizeAsync("http://169.254.169.254/latest/meta-data/", CancellationToken.None);

        Assert.Equal("/assets/images/noneimg-news.jpg", result);
    }

    [Fact]
    public async Task LocalizeAsync_PrivateNetworksDisabled_AllowsPrivateIp()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg",
                BlockPrivateNetworks = false
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("http://127.0.0.1:8080/image.jpg", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void IsPrivateAddress_DetectsPrivateRanges()
    {
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("127.0.0.1")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("10.0.0.1")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("172.16.0.1")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("192.168.1.1")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("169.254.169.254")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("0.0.0.0")));

        Assert.False(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("8.8.8.8")));
        Assert.False(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("1.1.1.1")));
        Assert.False(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("172.32.0.1")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("::ffff:192.168.1.10")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("fe80::1")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("fc00::1")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("fd00::1")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("::")));
        Assert.True(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("ff00::1")));
        Assert.False(SsrfGuard.IsPrivateAddress(System.Net.IPAddress.Parse("2001:4860:4860::8888")));
    }

    [Fact]
    public async Task LocalizeAsync_UnsafeExtension_FallsBackToImg()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            // URL ends with .exe but Content-Type is image; extension from content-type takes precedence
            var handler = new CountingHandler(HttpStatusCode.OK, "image/png", "fake-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("https://img.example/a.exe", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            // Content-Type image/png maps to .png, not .exe
            Assert.EndsWith(".png", result, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_UnsafeExtensionNoContentType_UsesImgExtension()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            // URL ends with .exe and no recognized content-type
            var handler = new CountingHandler(HttpStatusCode.OK, "application/octet-stream", "fake-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("https://img.example/a.exe", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            // .exe is not in AllowedExtensions, so it should fall back to .img
            Assert.EndsWith(".img", result, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_IndexPathTraversal_IgnoresMaliciousEntry()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        // Write a malicious index that tries path traversal
        var indexJson = """{"version":1,"entries":{"https://img.example/a.jpg":"../../etc/passwd"}}""";
        File.WriteAllText(Path.Combine(dir, ".media-index.json"), indexJson);

        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            // The localizer should reject the malicious entry and proceed to download
            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "safe-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount); // had to re-download
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_HtmlEncodedAmpersand_DecodesBeforeDownload()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            // URL with &amp; (HTML-encoded &) simulating Notion S3 signed URL leak
            var handler = new UrlRecordingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);
            var result = await localizer.LocalizeAsync(
                "https://s3.example/image.png?X-Amz-Algorithm=AWS4&amp;X-Amz-Date=20260212&amp;X-Amz-Expires=3600",
                CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount);

            // The actual HTTP request must use decoded & not &amp;
            var requestedUrl = Assert.Single(handler.RequestedUrls);
            Assert.Contains("&X-Amz-Date=", requestedUrl, StringComparison.Ordinal);
            Assert.DoesNotContain("&amp;", requestedUrl, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_WhenHashNamedFileAlreadyExists_ReusesFileAndWritesIndex()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var normalizedKey = "https://img.example/path/a.jpg";
            var existingName = BuildHashPrefix(normalizedKey) + ".jpg";
            File.WriteAllText(Path.Combine(dir, existingName), "existing");
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "should-not-download");
            using var http = new HttpClient(handler);
            string result;
            using (var localizer = new ImageAssetLocalizer(cfg, http))
            {
                result = await localizer.LocalizeAsync(normalizedKey, CancellationToken.None);
            }

            Assert.Equal("/assets/uploads/" + existingName, result);
            Assert.Equal(0, handler.RequestCount);
            Assert.Contains(normalizedKey, File.ReadAllText(Path.Combine(dir, ".media-index.json")), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_EmptyBodyRetriesAndThenSucceeds()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg",
                MaxRetries = 1,
                RetryBaseDelayMs = 1
            };

            var handler = new SequenceHandler(
                _ => Response(HttpStatusCode.OK, "image/jpeg", ""),
                _ => Response(HttpStatusCode.OK, "image/jpeg", "image-bytes"));
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);

            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_HandlerThrowsRecordsFailure()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg",
                MaxRetries = 0
            };

            var logger = new RecordingLogger();
            using var http = new HttpClient(new ThrowingHandler());
            using var localizer = new ImageAssetLocalizer(cfg, http, logger);
            var source = "https://img.example/a.jpg?token=secret#fragment";

            var result = await localizer.LocalizeAsync(source, CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            var failure = Assert.Single(localizer.Failures);
            Assert.Equal("https://img.example/a.jpg?[REDACTED]", failure.SourceUrl);
            Assert.Contains("HttpRequestException", failure.Reason, StringComparison.Ordinal);
            var warnings = string.Join('\n', logger.Warnings);
            Assert.Contains("event=media.download_error", warnings, StringComparison.Ordinal);
            Assert.Contains("https://img.example/a.jpg?[REDACTED]", warnings, StringComparison.Ordinal);
            Assert.DoesNotContain("token=secret", warnings, StringComparison.Ordinal);
            Assert.DoesNotContain("fragment", warnings, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_NestedTlsFailureLogsDeepestExceptionWithoutChangingFailureReason()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg",
                MaxRetries = 0
            };
            var logger = new RecordingLogger();
            using var http = new HttpClient(new NestedTlsFailureHandler());
            using var localizer = new ImageAssetLocalizer(cfg, http, logger);
            var source = "https://img.example/a.jpg?token=secret#fragment";

            var result = await localizer.LocalizeAsync(source, CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            var failure = Assert.Single(localizer.Failures);
            Assert.Equal("https://img.example/a.jpg?[REDACTED]", failure.SourceUrl);
            Assert.Equal(
                "HttpRequestException: SSL connection could not be established.",
                failure.Reason);

            var warning = Assert.Single(logger.Warnings);
            Assert.Contains("event=media.download_error", warning, StringComparison.Ordinal);
            Assert.Contains("error=HttpRequestException", warning, StringComparison.Ordinal);
            Assert.Contains(
                "root_error=Bukit.Content.Tests.ImageAssetLocalizerTests+TestSslException",
                warning,
                StringComparison.Ordinal);
            Assert.Contains(
                "root_message=\"bad protocol version\\r\\nforged=entry\"",
                warning,
                StringComparison.Ordinal);
            Assert.DoesNotContain('\r', warning);
            Assert.DoesNotContain('\n', warning);
            Assert.DoesNotContain("token=secret", warning, StringComparison.Ordinal);
            Assert.DoesNotContain("fragment", warning, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_LegacyIndexRootObject_HitsExistingFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "stored.jpg"), "existing");
            File.WriteAllText(Path.Combine(dir, ".media-index.json"), """{"https://img.example/a.jpg":"stored.jpg","skip":123,"blank":"   "}""");
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "should-not-download");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);

            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

            Assert.Equal("/assets/uploads/stored.jpg", result);
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LocalizeAsync_IndexEntryMissingFile_RemovesEntryAndDownloads()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, ".media-index.json"), """{"version":1,"entries":{"https://img.example/a.jpg":"missing.jpg"}}""");
            var cfg = new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            };

            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "downloaded");
            using var http = new HttpClient(handler);
            using var localizer = new ImageAssetLocalizer(cfg, http);

            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

            Assert.StartsWith("/assets/uploads/", result, StringComparison.Ordinal);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private sealed class UrlRecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _contentType;
        private readonly string _payload;

        public UrlRecordingHandler(HttpStatusCode statusCode, string contentType, string payload)
        {
            _statusCode = statusCode;
            _contentType = contentType;
            _payload = payload;
        }

        public int RequestCount { get; private set; }
        public List<string> RequestedUrls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestedUrls.Add(request.RequestUri?.ToString() ?? string.Empty);
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_payload, Encoding.UTF8)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);
            return Task.FromResult(response);
        }
    }

    private sealed class StreamingHandler : HttpMessageHandler
    {
        private readonly string _contentType;
        private readonly Stream _stream;

        public StreamingHandler(string contentType, Stream stream)
        {
            _contentType = contentType;
            _stream = stream;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(_stream)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);
            return Task.FromResult(response);
        }
    }

    private sealed class ConcurrentStartHandler : HttpMessageHandler
    {
        private readonly string _contentType;
        private readonly string _payload;
        private readonly ManualResetEventSlim _secondRequestEntered = new();
        private int _requestCount;

        public ConcurrentStartHandler(string contentType, string payload)
        {
            _contentType = contentType;
            _payload = payload;
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = request;
            var requestCount = Interlocked.Increment(ref _requestCount);
            if (requestCount == 1)
            {
                _secondRequestEntered.Wait(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            else
            {
                _secondRequestEntered.Set();
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8)
            };
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _secondRequestEntered.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ControlledResponseHandler : HttpMessageHandler
    {
        private readonly string _contentType;
        private readonly string _payload;
        private readonly TaskCompletionSource _requestStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseResponse =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        public ControlledResponseHandler(string contentType, string payload)
        {
            _contentType = contentType;
            _payload = payload;
        }

        public int RequestCount => Volatile.Read(ref _requestCount);
        public Task RequestStarted => _requestStarted.Task;

        public void ReleaseResponse() => _releaseResponse.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = request;
            Interlocked.Increment(ref _requestCount);
            _requestStarted.TrySetResult();
            await _releaseResponse.Task.WaitAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8)
            };
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);
            return response;
        }
    }

    private sealed class CancellableBlockingReadStream : Stream
    {
        private readonly byte[] _payload;
        private readonly TaskCompletionSource _readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseRead =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readState;

        public CancellableBlockingReadStream(byte[] payload)
        {
            _payload = payload;
        }

        public Task ReadStarted => _readStarted.Task;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _payload.Length;
        public override long Position
        {
            get => Volatile.Read(ref _readState) == 0 ? 0 : _payload.Length;
            set => throw new NotSupportedException();
        }

        public void ReleaseRead() => _releaseRead.TrySetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _readState, 1) != 0)
            {
                return 0;
            }

            _readStarted.TrySetResult();
            await _releaseRead.Task.WaitAsync(cancellationToken);
            var count = Math.Min(buffer.Length, _payload.Length);
            _payload.AsMemory(0, count).CopyTo(buffer);
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(new Memory<byte>(buffer, offset, count))
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class DirectoryObservingReadStream : Stream
    {
        private readonly string _directory;
        private readonly byte[] _payload;
        private readonly int _chunkSize;
        private int _offset;
        private int _readCount;

        public DirectoryObservingReadStream(string directory, byte[] payload, int chunkSize)
        {
            _directory = directory;
            _payload = payload;
            _chunkSize = chunkSize;
        }

        public bool SawDownloadFileBeforeSecondRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _payload.Length;
        public override long Position
        {
            get => _offset;
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            if (_readCount == 1)
            {
                SawDownloadFileBeforeSecondRead = Directory
                    .EnumerateFiles(_directory)
                    .Where(static path => !string.Equals(Path.GetFileName(path), ".media-index.json", StringComparison.Ordinal))
                    .Any();
            }

            if (_offset >= _payload.Length)
            {
                return ValueTask.FromResult(0);
            }

            var count = Math.Min(Math.Min(_chunkSize, buffer.Length), _payload.Length - _offset);
            _payload.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            _readCount++;
            return ValueTask.FromResult(count);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var memory = new Memory<byte>(buffer, offset, count);
            return ReadAsync(memory).AsTask().GetAwaiter().GetResult();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class NullContentTypeHandler : HttpMessageHandler
    {
        private readonly string _payload;

        public NullContentTypeHandler(string payload)
        {
            _payload = payload;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(_payload))
            });
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses;

        public SequenceHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            _responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            RequestCount++;
            var next = _responses.Count > 0 ? _responses.Dequeue() : _ => Response(HttpStatusCode.InternalServerError, "text/plain", "unexpected");
            return Task.FromResult(next(request));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            throw new HttpRequestException("network failed");
        }
    }

    private sealed class NestedTlsFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            throw new HttpRequestException(
                "SSL connection could not be established.",
                new AuthenticationException(
                    "Authentication failed.",
                    new TestSslException("bad protocol version\r\nforged=entry")));
        }
    }

    private sealed class TestSslException : Exception
    {
        public TestSslException(string message)
            : base(message)
        {
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _contentType;
        private readonly string _payload;

        public CountingHandler(HttpStatusCode statusCode, string contentType, string payload)
        {
            _statusCode = statusCode;
            _contentType = contentType;
            _payload = payload;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            RequestCount++;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_payload, Encoding.UTF8)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Debugs { get; } = new();
        public List<string> Warnings { get; } = new();

        public void Debug(string message) => Debugs.Add(message);
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string contentType, string payload)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return response;
    }

    private static string BuildHashPrefix(string normalizedKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }
}
