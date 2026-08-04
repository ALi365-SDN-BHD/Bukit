using Bukit.Engine.Abstractions.Content;
using SixLabors.ImageSharp;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Content.Media;
using Bukit.Shared;
using Bukit.Config;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ImageAssetLocalizerTests
{
    [Fact]
    public void CreateDefaultHandler_DisablesAutomaticRedirects()
    {
        using var handler = ImageAssetLocalizer.CreateDefaultHandler(new MediaConfig());

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public void Constructor_InjectedHttpClient_FailsClosed()
    {
        using var httpClient = new HttpClient(new CountingHandler(
            HttpStatusCode.OK,
            "image/jpeg",
            "unexpected"));

        Assert.Throws<NotSupportedException>(() =>
            new ImageAssetLocalizer(new MediaConfig(), httpClient));
    }

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
        using var localizer = CreateLocalizer(cfg, handler);
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
            string result;
            using (var localizer = CreateLocalizer(cfg, handler))
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
                CreateImagePayload("image/jpeg", "fake-image-streaming-payload"),
                chunkSize: 4);
            var handler = new StreamingHandler("image/jpeg", stream);
            using var localizer = CreateLocalizer(cfg, handler);

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

        using var localizer = CreateLocalizer(
            cfg,
            new CountingHandler(HttpStatusCode.InternalServerError, "text/plain", "error"));
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
    public async Task LocalizeAsync_SamePathDifferentQuery_UsesDistinctStoredFilesWithoutPersistingSecrets()
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
            string a;
            string b;
            using (var localizer = CreateLocalizer(cfg, handler))
            {
                a = await localizer.LocalizeAsync("https://img.example/path/a.jpg?X-Amz-Expires=100", CancellationToken.None);
                b = await localizer.LocalizeAsync("https://img.example/path/a.jpg?X-Amz-Expires=200", CancellationToken.None);
                Assert.NotEqual(a, b);
                Assert.Equal(2, handler.RequestCount);
            }

            Assert.Equal(3, Directory.GetFiles(dir).Length); // two images + .media-index.json
            var index = File.ReadAllText(Path.Combine(dir, ".media-index.json"));
            Assert.DoesNotContain("X-Amz-Expires", index, StringComparison.Ordinal);
            Assert.DoesNotContain("X-Amz-Expires=100", index, StringComparison.Ordinal);
            Assert.DoesNotContain("X-Amz-Expires=200", index, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("https://img.example/a%2Fb.jpg", "https://img.example/a/b.jpg")]
    [InlineData("https://img.example/a%3Fb.jpg", "https://img.example/a?b.jpg")]
    [InlineData("https://img.example/a//b.jpg", "https://img.example/a/b.jpg")]
    public async Task LocalizeAsync_DistinctHttpRequestTargets_UseDistinctIdentities(
        string firstSource,
        string secondSource)
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
            using var localizer = CreateLocalizer(cfg, handler);

            var first = await localizer.LocalizeAsync(firstSource, CancellationToken.None);
            var second = await localizer.LocalizeAsync(secondSource, CancellationToken.None);

            Assert.NotEqual(first, second);
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
    public async Task LocalizeAsync_DifferentFragments_ShareHttpRequestIdentity()
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
            using var localizer = CreateLocalizer(cfg, handler);

            var first = await localizer.LocalizeAsync(
                "https://img.example/a.jpg#first",
                CancellationToken.None);
            var second = await localizer.LocalizeAsync(
                "https://img.example/a.jpg#second",
                CancellationToken.None);

            Assert.Equal(first, second);
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
    public async Task LocalizeAsync_StoredFileName_UsesFullSha256Identity()
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
            using var localizer = CreateLocalizer(
                cfg,
                new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image"));

            var localized = await localizer.LocalizeAsync(
                "https://img.example/a.jpg",
                CancellationToken.None);

            var identity = Path.GetFileNameWithoutExtension(localized);
            Assert.Equal(64, identity.Length);
            Assert.All(identity, character =>
                Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));
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
            using var localizer = CreateLocalizer(cfg, handler);
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
            using var localizer = CreateLocalizer(cfg, handler);
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
            using var localizer = CreateLocalizer(cfg, handler);
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
        var stream = new CancellableBlockingReadStream(CreateImagePayload("image/jpeg", "fake-image"));
        var handler = new StreamingHandler("image/jpeg", stream);
        var localizer = CreateLocalizer(cfg, handler);
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
            using (var first = CreateLocalizer(cfg, firstHandler))
            {
                var a = await first.LocalizeAsync("https://img.example/path/a?token=one", CancellationToken.None);
                Assert.StartsWith("/assets/uploads/", a, StringComparison.Ordinal);
            }

            var secondHandler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image");
            using (var second = CreateLocalizer(cfg, secondHandler))
            {
                var b = await second.LocalizeAsync("https://img.example/path/a?token=one", CancellationToken.None);
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
    public async Task LocalizeAsync_WhenIndexedFileSignatureIsInvalid_RedownloadsBeforeReturning()
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
            const string source = "https://img.example/path/indexed.jpg";
            string firstUrl;
            using (var first = CreateLocalizer(
                cfg,
                new CountingHandler(HttpStatusCode.OK, "image/jpeg", "first")))
            {
                firstUrl = await first.LocalizeAsync(source, CancellationToken.None);
            }

            var storedPath = Path.Combine(dir, Path.GetFileName(firstUrl));
            File.WriteAllText(storedPath, "not-an-image");

            var secondHandler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "replacement");
            using var second = CreateLocalizer(cfg, secondHandler);

            var secondUrl = await second.LocalizeAsync(source, CancellationToken.None);

            Assert.Equal(firstUrl, secondUrl);
            Assert.Equal(1, secondHandler.RequestCount);
            Assert.True(await ImageContentSignature.MatchesFileAsync(
                storedPath,
                "image/jpeg",
                CancellationToken.None));
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
    public async Task LocalizeAsync_WhenMemoryCachedFileIsModified_RedownloadsBeforeReturning()
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
            const string source = "https://img.example/path/memory-cache.jpg";
            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "replacement");
            using var localizer = CreateLocalizer(cfg, handler);

            var firstUrl = await localizer.LocalizeAsync(source, CancellationToken.None);
            var storedPath = Path.Combine(dir, Path.GetFileName(firstUrl));
            File.WriteAllText(storedPath, "not-an-image");

            var secondUrl = await localizer.LocalizeAsync(source, CancellationToken.None);

            Assert.Equal(firstUrl, secondUrl);
            Assert.Equal(2, handler.RequestCount);
            Assert.True(await ImageContentSignature.MatchesFileAsync(
                storedPath,
                "image/jpeg",
                CancellationToken.None));
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
    public async Task LocalizeAsync_WhenOrphanHashFileSignatureIsInvalid_RedownloadsBeforeReturning()
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
            const string source = "https://img.example/path/orphan.jpg";
            string firstUrl;
            using (var first = CreateLocalizer(
                cfg,
                new CountingHandler(HttpStatusCode.OK, "image/jpeg", "first")))
            {
                firstUrl = await first.LocalizeAsync(source, CancellationToken.None);
            }

            File.Delete(Path.Combine(dir, ".media-index.json"));
            var storedPath = Path.Combine(dir, Path.GetFileName(firstUrl));
            File.WriteAllText(storedPath, "not-an-image");

            var secondHandler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "replacement");
            using var second = CreateLocalizer(cfg, secondHandler);

            var secondUrl = await second.LocalizeAsync(source, CancellationToken.None);

            Assert.Equal(firstUrl, secondUrl);
            Assert.Equal(1, secondHandler.RequestCount);
            Assert.True(await ImageContentSignature.MatchesFileAsync(
                storedPath,
                "image/jpeg",
                CancellationToken.None));
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
    public async Task LocalizeAsync_WhenVersionedIndexReferencesUnsafeExtension_RedownloadsSafeAsset()
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
            const string source = "https://img.example/path/unsafe.jpg";
            string firstUrl;
            using (var first = CreateLocalizer(
                cfg,
                new CountingHandler(HttpStatusCode.OK, "image/jpeg", "first")))
            {
                firstUrl = await first.LocalizeAsync(source, CancellationToken.None);
            }

            string indexKey;
            using (var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, ".media-index.json"))))
            {
                indexKey = Assert.Single(index.RootElement.GetProperty("entries").EnumerateObject()).Name;
            }

            File.Delete(Path.Combine(dir, Path.GetFileName(firstUrl)));
            File.WriteAllText(Path.Combine(dir, "legacy.svg"), "<svg><script>alert(1)</script></svg>");
            File.WriteAllText(
                Path.Combine(dir, ".media-index.json"),
                JsonSerializer.Serialize(new
                {
                    version = 3,
                    entries = new Dictionary<string, string> { [indexKey] = "legacy.svg" }
                }));

            var secondHandler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "replacement");
            using var second = CreateLocalizer(cfg, secondHandler);

            var secondUrl = await second.LocalizeAsync(source, CancellationToken.None);

            Assert.DoesNotContain(".svg", secondUrl, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, secondHandler.RequestCount);
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
            using var localizer = CreateLocalizer(cfg, handler);
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
            using var localizer = CreateLocalizer(cfg, handler);
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
    public async Task LocalizeAsync_ApplicationOctetStream_IsRejected()
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
            using var localizer = CreateLocalizer(cfg, handler);
            var result = await localizer.LocalizeAsync("https://img.example/a.png", CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            Assert.Empty(Directory.GetFiles(dir));
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
    public async Task LocalizeAsync_MissingContentType_IsRejected()
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
            using var localizer = CreateLocalizer(cfg, handler);
            var result = await localizer.LocalizeAsync("https://img.example/a.webp", CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            Assert.Equal(1, handler.RequestCount);
            Assert.Empty(Directory.GetFiles(dir));
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
    public async Task LocalizeAsync_SvgContentType_IsRejected()
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
            var handler = new CountingHandler(HttpStatusCode.OK, "image/svg+xml", "<svg></svg>");
            using var localizer = CreateLocalizer(cfg, handler);

            var result = await localizer.LocalizeAsync(
                "https://img.example/a.svg",
                CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            Assert.Empty(Directory.GetFiles(dir));
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
    public async Task LocalizeAsync_ContentTypeSignatureMismatch_DeletesTempAndDoesNotIndex()
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
            var handler = new ByteArrayHandler(
                "image/png",
                CreateImagePayload("image/jpeg", "not-a-png"));
            using var localizer = CreateLocalizer(cfg, handler);

            var result = await localizer.LocalizeAsync(
                "https://img.example/a.png",
                CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            var failure = Assert.Single(localizer.Failures);
            Assert.Equal("Image content signature does not match Content-Type or is not decodable.", failure.Reason);
            localizer.Dispose();
            Assert.Empty(Directory.GetFiles(dir));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    public static TheoryData<string, byte[]> SupportedImageSignatures => new()
    {
        { "image/jpeg", [0xFF, 0xD8, 0xFF, 0x00] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
        { "image/gif", Encoding.ASCII.GetBytes("GIF89a") },
        { "image/webp", [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50] },
        { "image/avif", [0, 0, 0, 16, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66, 0, 0, 0, 0] },
        { "image/bmp", [0x42, 0x4D] },
        { "image/x-icon", [0, 0, 1, 0] },
        { "image/tiff", [0x49, 0x49, 0x2A, 0] }
    };

    [Theory]
    [MemberData(nameof(SupportedImageSignatures))]
    public void ImageContentSignature_MatchesSupportedFormats(string contentType, byte[] payload)
    {
        Assert.True(ImageContentSignature.Matches(contentType, payload));
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
            using var localizer = CreateLocalizer(cfg, handler);
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

        using var localizer = CreateLocalizer(
            cfg,
            new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image"));
        var result = await localizer.LocalizeAsync("http://127.0.0.1:8080/secret.jpg", CancellationToken.None);

        Assert.Equal("/assets/images/noneimg-news.jpg", result);
    }

    [Fact]
    public async Task LocalizeAsync_InjectedClient_DnsFailureFailsClosedBeforeSend()
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
                BlockPrivateNetworks = true
            };
            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "must-not-send");
            using var localizer = CreateLocalizer(
                cfg,
                handler,
                resolveHostAddresses: static (_, _) =>
                    Task.FromException<IPAddress[]>(new SocketException()));

            var result = await localizer.LocalizeAsync(
                "https://dns-failure.invalid/image.jpg",
                CancellationToken.None);

            Assert.Equal(cfg.DefaultImageUrl, result);
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LocalizeAsync_RedirectToPrivateTarget_FailsBeforeSecondSend()
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
                BlockPrivateNetworks = true
            };
            var handler = new SequenceHandler(
                _ => new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("http://127.0.0.1/private.jpg") }
                },
                _ => Response(HttpStatusCode.OK, "image/jpeg", "must-not-send"));
            using var localizer = CreateLocalizer(cfg, handler);

            var result = await localizer.LocalizeAsync(
                "https://img.example/image.jpg",
                CancellationToken.None);

            Assert.Equal(cfg.DefaultImageUrl, result);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LocalizeAsync_RedirectToValidatedPublicTarget_UsesSingleHopRequests()
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
                BlockPrivateNetworks = true
            };
            var handler = new SequenceHandler(
                _ => new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://cdn.example/image.jpg") }
                },
                _ => Response(HttpStatusCode.OK, "image/jpeg", "redirected"));
            using var localizer = CreateLocalizer(cfg, handler);

            var result = await localizer.LocalizeAsync(
                "https://img.example/image.jpg",
                CancellationToken.None);

            Assert.StartsWith(cfg.UrlBase, result, StringComparison.Ordinal);
            Assert.Equal(2, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LocalizeAsync_MoveCollisionWithInvalidWinner_FailsClosed()
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
            const string source = "https://img.example/collision.jpg";
            var handler = new ControlledResponseHandler("image/jpeg", "downloaded");
            using var localizer = CreateLocalizer(cfg, handler);
            var localization = localizer.LocalizeAsync(source, CancellationToken.None);
            await handler.RequestStarted.WaitAsync(TimeSpan.FromSeconds(1));
            var winnerPath = Path.Combine(dir, BuildExpectedMediaFileName(source, ".jpg"));
            File.WriteAllText(winnerPath, "not-an-image");
            handler.ReleaseResponse();

            var result = await localization;

            Assert.Equal(cfg.DefaultImageUrl, result);
            Assert.Equal("Media winner file failed content validation.", Assert.Single(localizer.Failures).Reason);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LocalizeAsync_MoveCollisionWithDifferentValidWinner_FailsIdentityClosed()
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
            const string source = "https://img.example/valid-collision.jpg";
            var handler = new ControlledResponseHandler("image/jpeg", "downloaded");
            using var localizer = CreateLocalizer(cfg, handler);
            var localization = localizer.LocalizeAsync(source, CancellationToken.None);
            await handler.RequestStarted.WaitAsync(TimeSpan.FromSeconds(1));
            var winnerPath = Path.Combine(dir, BuildExpectedMediaFileName(source, ".jpg"));
            var winnerBytes = CreateImagePayload("image/jpeg", "different-winner");
            await File.WriteAllBytesAsync(winnerPath, winnerBytes);
            handler.ReleaseResponse();

            var result = await localization;

            Assert.Equal(cfg.DefaultImageUrl, result);
            Assert.Equal(
                "Media winner file did not match downloaded content identity.",
                Assert.Single(localizer.Failures).Reason);
            Assert.Equal(winnerBytes, await File.ReadAllBytesAsync(winnerPath));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(dir),
                static path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LocalizeAsync_MoveCollisionWithIdenticalValidWinner_PublishesWinnerAndIndex()
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
            const string source = "https://img.example/identical-collision.jpg";
            const string payload = "identical-winner";
            var handler = new ControlledResponseHandler("image/jpeg", payload);
            using var localizer = CreateLocalizer(cfg, handler);
            var localization = localizer.LocalizeAsync(source, CancellationToken.None);
            await handler.RequestStarted.WaitAsync(TimeSpan.FromSeconds(1));
            var winnerPath = Path.Combine(dir, BuildExpectedMediaFileName(source, ".jpg"));
            var winnerBytes = CreateImagePayload("image/jpeg", payload);
            await File.WriteAllBytesAsync(winnerPath, winnerBytes);
            handler.ReleaseResponse();

            var result = await localization;

            Assert.Equal($"{cfg.UrlBase}/{Path.GetFileName(winnerPath)}", result);
            Assert.Empty(localizer.Failures);
            Assert.Equal(winnerBytes, await File.ReadAllBytesAsync(winnerPath));
            localizer.Dispose();
            using var index = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(dir, ".media-index.json")));
            var indexEntry = Assert.Single(index.RootElement.GetProperty("entries").EnumerateObject());
            Assert.Equal(Path.GetFileName(winnerPath), indexEntry.Value.GetString());
            Assert.DoesNotContain(
                Directory.EnumerateFiles(dir),
                static path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Dispose_DuringCollisionWinnerVerification_PropagatesCancellationWithoutIndexSuccess()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var downloadedBytes = CreateImagePayload("image/jpeg", "downloaded");
        var stream = new CancellableBlockingReadStream(downloadedBytes);
        var localizer = CreateLocalizer(
            new MediaConfig
            {
                DownloadToLocal = true,
                DownloadDir = dir,
                UrlBase = "/assets/uploads",
                DefaultImageUrl = "/assets/images/noneimg-news.jpg"
            },
            new StreamingHandler("image/jpeg", stream));
        Task<string>? localization = null;
        try
        {
            const string source = "https://img.example/cancel-collision.jpg";
            localization = localizer.LocalizeAsync(source, CancellationToken.None);
            await stream.ReadStarted.WaitAsync(TimeSpan.FromSeconds(1));
            var tempPath = Assert.Single(
                Directory.EnumerateFiles(dir),
                static path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.Ordinal));
            var winnerPath = Path.Combine(dir, BuildExpectedMediaFileName(source, ".jpg"));
            await using (var winner = new FileStream(
                winnerPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                await winner.WriteAsync(CreateImagePayload("image/jpeg", "different-winner"));
                winner.SetLength(128L * 1024 * 1024);
            }

            stream.ReleaseRead();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (File.Exists(tempPath) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1);
            }

            Assert.False(File.Exists(tempPath));
            localizer.Dispose();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await localization.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(localizer.Failures);
            Assert.False(File.Exists(Path.Combine(dir, ".media-index.json")));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(dir),
                static path => Path.GetFileName(path).EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            stream.ReleaseRead();
            localizer.Dispose();
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

            Directory.Delete(dir, recursive: true);
        }
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

        using var localizer = CreateLocalizer(
            cfg,
            new CountingHandler(HttpStatusCode.OK, "image/jpeg", "fake-image"));
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

        using var localizer = CreateLocalizer(
            cfg,
            new CountingHandler(HttpStatusCode.OK, "image/jpeg", "metadata"));
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
            using var localizer = CreateLocalizer(cfg, handler);
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
            using var localizer = CreateLocalizer(cfg, handler);
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
    public async Task LocalizeAsync_UnsafeExtensionWithOctetStream_IsRejected()
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

            var handler = new CountingHandler(HttpStatusCode.OK, "application/octet-stream", "fake-image");
            using var localizer = CreateLocalizer(cfg, handler);
            var result = await localizer.LocalizeAsync("https://img.example/a.exe", CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            Assert.Empty(Directory.GetFiles(dir));
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
            using var localizer = CreateLocalizer(cfg, handler);
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
            using var localizer = CreateLocalizer(cfg, handler);
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
    public async Task LocalizeAsync_WhenValidHashNamedFileAlreadyExists_ReusesFileAndWritesIndex()
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

            const string source = "https://img.example/path/a.jpg";
            string result;
            using (var first = CreateLocalizer(
                cfg,
                new CountingHandler(HttpStatusCode.OK, "image/jpeg", "existing")))
            {
                result = await first.LocalizeAsync(source, CancellationToken.None);
            }

            File.Delete(Path.Combine(dir, ".media-index.json"));
            var handler = new CountingHandler(HttpStatusCode.OK, "image/jpeg", "should-not-download");
            using (var localizer = CreateLocalizer(cfg, handler))
            {
                var reused = await localizer.LocalizeAsync(source, CancellationToken.None);
                Assert.Equal(result, reused);
            }

            Assert.Equal(0, handler.RequestCount);
            Assert.DoesNotContain(source, File.ReadAllText(Path.Combine(dir, ".media-index.json")), StringComparison.Ordinal);
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
            using var localizer = CreateLocalizer(cfg, handler);

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
            using var localizer = CreateLocalizer(cfg, new ThrowingHandler(), logger);
            var source = "https://img.example/a.jpg?token=secret#fragment";

            var result = await localizer.LocalizeAsync(source, CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            var failure = Assert.Single(localizer.Failures);
            Assert.Equal("https://img.example/<redacted-path>", failure.SourceUrl);
            Assert.Contains("HttpRequestException", failure.Reason, StringComparison.Ordinal);
            var warnings = string.Join('\n', logger.Warnings);
            Assert.Contains("event=media.download_error", warnings, StringComparison.Ordinal);
            Assert.Contains("https://img.example/<redacted-path>", warnings, StringComparison.Ordinal);
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
            using var localizer = CreateLocalizer(cfg, new NestedTlsFailureHandler(), logger);
            var source = "https://img.example/a.jpg?token=secret#fragment";

            var result = await localizer.LocalizeAsync(source, CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            var failure = Assert.Single(localizer.Failures);
            Assert.Equal("https://img.example/<redacted-path>", failure.SourceUrl);
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
    public async Task LocalizeAsync_LegacyIndexRootObject_IsInvalidatedAndRedownloaded()
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
            using var localizer = CreateLocalizer(cfg, handler);

            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);
            localizer.Dispose();

            Assert.NotEqual("/assets/uploads/stored.jpg", result);
            Assert.Equal(1, handler.RequestCount);
            var migratedIndex = File.ReadAllText(Path.Combine(dir, ".media-index.json"));
            Assert.Contains("\"version\":3", migratedIndex, StringComparison.Ordinal);
            Assert.DoesNotContain("https://img.example/a.jpg", migratedIndex, StringComparison.Ordinal);
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
            using var localizer = CreateLocalizer(cfg, handler);

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

    private static ImageAssetLocalizer CreateLocalizer(
        MediaConfig config,
        HttpMessageHandler handler,
        ILogger? logger = null,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHostAddresses = null)
        => new(
            config,
            handler,
            resolveHostAddresses ?? ResolvePublicHostAsync,
            logger);

    private static Task<IPAddress[]> ResolvePublicHostAsync(
        string host,
        CancellationToken cancellationToken)
    {
        _ = host;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
    }

    private static string BuildExpectedMediaFileName(string source, string extension)
    {
        var uri = new Uri(source);
        var requestTarget = uri.GetComponents(
            UriComponents.HttpRequestUrl,
            UriFormat.UriEscaped);
        var normalizedKey = $"v3:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(requestTarget)))}";
        var identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey)));
        return identity + extension;
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
                Content = new ByteArrayContent(CreateImagePayload(_contentType, _payload))
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
                Content = new ByteArrayContent(CreateImagePayload(_contentType, _payload))
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
                Content = new ByteArrayContent(CreateImagePayload(_contentType, _payload))
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

    private sealed class ByteArrayHandler(string contentType, byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return Task.FromResult(response);
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
                Content = new ByteArrayContent(CreateImagePayload(_contentType, _payload))
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
            Content = new ByteArrayContent(CreateImagePayload(contentType, payload))
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return response;
    }

    private static byte[] CreateImagePayload(string contentType, string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return [];
        }

        // Encode a real decodable 1x1 image. The payload seeds the pixel color so
        // distinct payloads produce distinct file bytes while remaining valid.
        var seed = 0;
        foreach (var character in payload)
        {
            seed = unchecked(seed * 31 + character);
        }

        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1, 1);
        image[0, 0] = new SixLabors.ImageSharp.PixelFormats.Rgba32(
            (byte)(seed & 0xFF),
            (byte)((seed >> 8) & 0xFF),
            (byte)((seed >> 16) & 0xFF));

        using var stream = new MemoryStream();
        switch (contentType.ToLowerInvariant())
        {
            case "image/jpeg" or "image/jpg":
                image.SaveAsJpeg(stream);
                break;
            case "image/png":
                image.SaveAsPng(stream);
                break;
            case "image/gif":
                image.SaveAsGif(stream);
                break;
            case "image/webp":
                image.SaveAsWebp(stream);
                break;
            case "image/bmp":
                image.SaveAsBmp(stream);
                break;
            default:
                // AVIF/ICO have no approved decoder: return a magic-only payload
                // so signature-level tests still observe the declared behavior
                return contentType.ToLowerInvariant() switch
                {
                    "image/avif" => [0, 0, 0, 16, 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66, 0, 0, 0, 0],
                    "image/x-icon" or "image/vnd.microsoft.icon" or "image/ico" => [0, 0, 1, 0],
                    "image/tiff" => [0x49, 0x49, 0x2A, 0],
                    _ => []
                };
        }

        return stream.ToArray();
    }

    [Fact]
    public async Task ValidateAsync_ValidHeaderWithTruncatedPixels_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-validator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            byte[] full;
            using (var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(128, 128))
            {
                for (var y = 0; y < 128; y++)
                {
                    for (var x = 0; x < 128; x++)
                    {
                        image[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgba32((byte)x, (byte)y, 0);
                    }
                }

                using var stream = new MemoryStream();
                image.SaveAsJpeg(stream);
                full = stream.ToArray();
            }

            // Header stays intact but the scan data is cut off: only a full decode
            // can prove the payload is unusable.
            var path = Path.Combine(dir, "truncated.jpg");
            File.WriteAllBytes(path, full.AsSpan(0, full.Length / 5).ToArray());

            Assert.False(await new ImageContentValidator().ValidateAsync(path, "image/jpeg", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_TotalDecodedPixelsOverBudget_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-validator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 20000 x 20000 declares 400,000,000 pixels, above the 100,000,000 budget.
            var path = Path.Combine(dir, "huge.png");
            File.WriteAllBytes(path, CreatePngHeaderWithDimensions(20000, 20000));

            Assert.False(await new ImageContentValidator().ValidateAsync(path, "image/png", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_MoreThan256Frames_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-validator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1, 1);
            for (var i = 0; i < 256; i++)
            {
                image.Frames.AddFrame(image.Frames.RootFrame);
            }

            var path = Path.Combine(dir, "frames.gif");
            using (var stream = File.Create(path))
            {
                image.SaveAsGif(stream);
            }

            Assert.False(await new ImageContentValidator().ValidateAsync(path, "image/gif", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static byte[] CreatePngHeaderWithDimensions(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // RGBA color type
        WritePngChunk(stream, "IHDR", ihdr);
        WritePngChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static void WritePngChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var lengthBytes = new byte[4];
        WriteBigEndian(lengthBytes, 0, data.Length);
        stream.Write(lengthBytes);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, unchecked((int)Crc32(crcInput)));
        stream.Write(crcBytes);
    }

    private static uint Crc32(byte[] input)
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        var crc = 0xFFFFFFFFu;
        foreach (var b in input)
        {
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

}
