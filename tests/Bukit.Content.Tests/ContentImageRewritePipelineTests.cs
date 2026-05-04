using Bukit.Content.Media;
using Bukit.Config;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ContentImageRewritePipelineTests
{
    [Fact]
    public async Task RewriteAsync_RewritesHtmlAndFieldUrls()
    {
        var item = new ContentItem(
            Id: "1",
            Title: "t",
            Slug: "s",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>x</p><img src=\"https://img.example/a.jpg\" alt=\"\" />",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["cover"] = new ContentField("text", "https://img.example/cover.jpg"),
                ["title_image"] = new ContentField("text", "https://img.example/keep.jpg")
            });

        var cfg = new MediaConfig
        {
            FieldKeys = new[] { "cover" },
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var pipeline = new ContentImageRewritePipeline(cfg, new StubLocalizer());
        var result = await pipeline.RewriteAsync(new[] { item }, CancellationToken.None);
        var rewritten = Assert.Single(result);

        Assert.Contains("/assets/uploads/a.jpg", rewritten.ContentHtml, StringComparison.Ordinal);
        Assert.Equal("/assets/uploads/cover.jpg", rewritten.Fields!["cover"].Value);
        Assert.Equal("https://img.example/keep.jpg", rewritten.Fields["title_image"].Value);
    }

    [Fact]
    public async Task RewriteAsync_WhenHtmlImgSrcMissing_UsesDefaultImage()
    {
        var item = new ContentItem(
            Id: "1",
            Title: "t",
            Slug: "s",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<img src=\"\" alt=\"\" />",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: null);

        var cfg = new MediaConfig
        {
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var pipeline = new ContentImageRewritePipeline(cfg, new StubLocalizer());
        var result = await pipeline.RewriteAsync(new[] { item }, CancellationToken.None);

        Assert.Contains("/assets/images/noneimg-news.jpg", Assert.Single(result).ContentHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RewriteAsync_HtmlDecodesAmpersandInImgSrc()
    {
        // Simulates Notion-rendered HTML where & in URLs becomes &amp;
        var item = new ContentItem(
            Id: "1",
            Title: "t",
            Slug: "s",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<img src=\"https://s3.example/image.png?X-Amz-Algorithm=AWS4&amp;X-Amz-Date=20260212\" alt=\"\" />",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: null);

        var cfg = new MediaConfig
        {
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var recorder = new RecordingLocalizer();
        var pipeline = new ContentImageRewritePipeline(cfg, recorder);
        await pipeline.RewriteAsync(new[] { item }, CancellationToken.None);

        // The localizer should receive the decoded URL with & not &amp;
        var received = Assert.Single(recorder.ReceivedUrls);
        Assert.Contains("&X-Amz-Date=", received, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;", received, StringComparison.Ordinal);
    }

    private sealed class StubLocalizer : IImageAssetLocalizer
    {
        public Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                return Task.FromResult("/assets/images/noneimg-news.jpg");
            }

            if (sourceUrl.Contains("/a.jpg", StringComparison.Ordinal))
            {
                return Task.FromResult("/assets/uploads/a.jpg");
            }

            if (sourceUrl.Contains("/cover.jpg", StringComparison.Ordinal))
            {
                return Task.FromResult("/assets/uploads/cover.jpg");
            }

            return Task.FromResult(sourceUrl);
        }
    }

    private sealed class RecordingLocalizer : IImageAssetLocalizer
    {
        public List<string> ReceivedUrls { get; } = new();

        public Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                ReceivedUrls.Add(sourceUrl);
            }

            return Task.FromResult(sourceUrl ?? "/assets/images/noneimg-news.jpg");
        }
    }
}
