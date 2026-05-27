using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bukit.Content.Media;
using Bukit.Config;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ImageAssetLocalizerTests
{
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

        using var localizer = new ImageAssetLocalizer(cfg);
        var result = await localizer.LocalizeAsync("/assets/style.png", CancellationToken.None);

        Assert.Equal("/assets/style.png", result);
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
        Assert.True(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("127.0.0.1")));
        Assert.True(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("10.0.0.1")));
        Assert.True(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("172.16.0.1")));
        Assert.True(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("192.168.1.1")));
        Assert.True(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("169.254.169.254")));
        Assert.True(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("0.0.0.0")));

        Assert.False(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("8.8.8.8")));
        Assert.False(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("1.1.1.1")));
        Assert.False(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("172.32.0.1")));
        Assert.True(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("::ffff:192.168.1.10")));
        Assert.True(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("fe80::1")));
        Assert.False(ImageAssetLocalizer.IsPrivateAddress(System.Net.IPAddress.Parse("2001:4860:4860::8888")));
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

            using var http = new HttpClient(new ThrowingHandler());
            using var localizer = new ImageAssetLocalizer(cfg, http);

            var result = await localizer.LocalizeAsync("https://img.example/a.jpg", CancellationToken.None);

            Assert.Equal("/assets/images/noneimg-news.jpg", result);
            var failure = Assert.Single(localizer.Failures);
            Assert.Equal("https://img.example/a.jpg", failure.SourceUrl);
            Assert.Contains("HttpRequestException", failure.Reason, StringComparison.Ordinal);
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
