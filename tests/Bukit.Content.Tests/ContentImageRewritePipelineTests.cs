using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Media;
using Bukit.Config;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ContentImageRewritePipelineTests
{
    [Fact]
    public async Task RewriteAsync_RewritesHtmlAndFieldUrls()
    {
        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>x</p><img src=\"https://img.example/a.jpg\" alt=\"\" />",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
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

        Assert.Contains("/assets/uploads/a.jpg", rewritten.Body.Html, StringComparison.Ordinal);
        Assert.Equal("/assets/uploads/cover.jpg", rewritten.CustomFields!["cover"].Value);
        Assert.Equal("https://img.example/keep.jpg", rewritten.CustomFields["title_image"].Value);
    }

    [Fact]
    public async Task RewriteAsync_WhenHtmlImgSrcMissing_UsesDefaultImage()
    {
        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<img src=\"\" alt=\"\" />",
            fields: null);

        var cfg = new MediaConfig
        {
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var pipeline = new ContentImageRewritePipeline(cfg, new StubLocalizer());
        var result = await pipeline.RewriteAsync(new[] { item }, CancellationToken.None);

        Assert.Contains("/assets/images/noneimg-news.jpg", Assert.Single(result).Body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RewriteAsync_HtmlDecodesAmpersandInImgSrc()
    {
        // Simulates Notion-rendered HTML where & in URLs becomes &amp;
        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<img src=\"https://s3.example/image.png?X-Amz-Algorithm=AWS4&amp;X-Amz-Date=20260212\" alt=\"\" />",
            fields: null);

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

    [Fact]
    public async Task RewriteAsync_DeduplicatesRepeatedUrlsWithinSingleItem()
    {
        var repeatedUrl = "https://img.example/repeat.jpg";
        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: $"<img src=\"{repeatedUrl}\" /><img src=\"{repeatedUrl}\" />",
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["cover"] = new ContentField("text", repeatedUrl),
                ["gallery"] = new ContentField("files", new[] { repeatedUrl, repeatedUrl })
            });

        var cfg = new MediaConfig
        {
            FieldKeys = new[] { "cover", "gallery" },
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var recorder = new CountingLocalizer();
        var pipeline = new ContentImageRewritePipeline(cfg, recorder);

        var result = await pipeline.RewriteAsync(new[] { item }, CancellationToken.None);
        var rewritten = Assert.Single(result);

        Assert.Equal(1, recorder.GetCallCount(repeatedUrl));
        Assert.Contains("/assets/uploads/repeat.jpg", rewritten.Body.Html, StringComparison.Ordinal);
        Assert.Equal("/assets/uploads/repeat.jpg", rewritten.CustomFields!["cover"].Value);
        Assert.Equal(
            new[] { "/assets/uploads/repeat.jpg", "/assets/uploads/repeat.jpg" },
            Assert.IsAssignableFrom<IReadOnlyList<string>>(rewritten.CustomFields["gallery"].Value));
    }

    [Fact]
    public async Task RewriteAsync_LocalizesDistinctFieldListUrlsConcurrently()
    {
        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["gallery"] = new ContentField("files", new[]
                {
                    "https://img.example/a.jpg",
                    "https://img.example/b.jpg",
                    "https://img.example/c.jpg"
                })
            });

        var cfg = new MediaConfig
        {
            FieldKeys = new[] { "gallery" },
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var localizer = new ParallelProbeLocalizer();
        var pipeline = new ContentImageRewritePipeline(cfg, localizer);

        await pipeline.RewriteAsync(new[] { item }, CancellationToken.None);

        Assert.True(localizer.MaxConcurrency >= 2, $"Expected concurrent localize calls, actual max concurrency was {localizer.MaxConcurrency}.");
    }

    [Fact]
    public async Task RewriteAsync_LocalizesDistinctHtmlUrlsConcurrentlyWithinSamePass()
    {
        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: """
                         <img src="https://img.example/a.jpg" />
                         <img src="https://img.example/b.jpg" />
                         <img src="https://img.example/c.jpg" />
                         """,
            fields: null);

        var cfg = new MediaConfig
        {
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var localizer = new ParallelProbeLocalizer();
        var pipeline = new ContentImageRewritePipeline(cfg, localizer);

        await pipeline.RewriteAsync(new[] { item }, CancellationToken.None);

        Assert.True(localizer.MaxConcurrency >= 2, $"Expected concurrent HTML localize calls, actual max concurrency was {localizer.MaxConcurrency}.");
    }

    [Fact]
    public async Task RewriteAsync_LocalizesDistinctHtmlUrlsConcurrentlyAcrossDifferentPasses()
    {
        var item = ContentDocument.Create(
            id: "1",
            title: "t",
            slug: "s",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: """
                         <img src="https://img.example/a.jpg" />
                         <video poster="https://img.example/b.jpg"></video>
                         <a href="https://img.example/c.jpg">download</a>
                         """,
            fields: null);

        var cfg = new MediaConfig
        {
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };

        var localizer = new ParallelProbeLocalizer();
        var pipeline = new ContentImageRewritePipeline(cfg, localizer);

        await pipeline.RewriteAsync(new[] { item }, CancellationToken.None);

        Assert.True(localizer.MaxConcurrency >= 2, $"Expected concurrent cross-pass localize calls, actual max concurrency was {localizer.MaxConcurrency}.");
    }

    [Fact]
    public async Task RewriteBodyHtmlAsync_RewritesSrcsetEntriesAndDecodesHtmlEntities()
    {
        var html = """
                   <img
                     srcset="https://img.example/small.jpg 480w, https://img.example/large.jpg?x=1&amp;y=2 960w"
                     src="https://img.example/fallback.jpg" />
                   """;
        var cfg = new MediaConfig
        {
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };
        var localizer = new MappingLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["https://img.example/small.jpg"] = "/assets/uploads/small.jpg",
            ["https://img.example/large.jpg?x=1&y=2"] = "/assets/uploads/large.jpg",
            ["https://img.example/fallback.jpg"] = "/assets/uploads/fallback.jpg"
        });
        var pipeline = new ContentImageRewritePipeline(cfg, localizer);

        var rewritten = await pipeline.RewriteBodyHtmlAsync(html, CancellationToken.None);

        Assert.NotNull(rewritten);
        Assert.Contains("/assets/uploads/small.jpg 480w", rewritten);
        Assert.Contains("/assets/uploads/large.jpg 960w", rewritten);
        Assert.Contains("src=\"/assets/uploads/fallback.jpg\"", rewritten);
        Assert.Contains("https://img.example/large.jpg?x=1&y=2", localizer.ReceivedUrls);
    }

    [Fact]
    public async Task RewriteBodyHtmlAsync_RewritesMultipleReferencesOnSameImageTag()
    {
        var html = """
                   <img
                     data-src="https://img.example/lazy.jpg"
                     src="https://img.example/fallback.jpg"
                     srcset="https://img.example/small.jpg 480w, https://img.example/large.jpg 960w" />
                   """;
        var cfg = new MediaConfig
        {
            DefaultImageUrl = "/assets/images/noneimg-news.jpg"
        };
        var localizer = new MappingLocalizer(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["https://img.example/lazy.jpg"] = "/assets/uploads/lazy.jpg",
            ["https://img.example/fallback.jpg"] = "/assets/uploads/fallback.jpg",
            ["https://img.example/small.jpg"] = "/assets/uploads/small.jpg",
            ["https://img.example/large.jpg"] = "/assets/uploads/large.jpg"
        });
        var pipeline = new ContentImageRewritePipeline(cfg, localizer);

        var rewritten = await pipeline.RewriteBodyHtmlAsync(html, CancellationToken.None);

        Assert.NotNull(rewritten);
        Assert.Contains("data-src=\"/assets/uploads/lazy.jpg\"", rewritten);
        Assert.Contains("src=\"/assets/uploads/fallback.jpg\"", rewritten);
        Assert.Contains("/assets/uploads/small.jpg 480w", rewritten);
        Assert.Contains("/assets/uploads/large.jpg 960w", rewritten);
    }

    [Fact]
    public void HtmlMediaReferenceScanner_FindsSupportedReferencesInDocumentOrder()
    {
        var html = """
                   <picture>
                     <source srcset="https://img.example/small.jpg 480w, https://img.example/large.jpg 960w" />
                     <img data-src='https://img.example/lazy.jpg' src="https://img.example/fallback.jpg" />
                   </picture>
                   <video poster="https://img.example/poster.jpg" src="https://media.example/clip.mp4"></video>
                   <a href="https://img.example/download.png?x=1&amp;y=2">image</a>
                   <a href="https://img.example/file.pdf">document</a>
                   <span data-src="https://img.example/span.gif"></span>
                   """;

        var references = HtmlMediaReferenceScanner.Find(html);

        Assert.Equal(
            new[]
            {
                "https://img.example/small.jpg 480w, https://img.example/large.jpg 960w",
                "https://img.example/lazy.jpg",
                "https://img.example/fallback.jpg",
                "https://img.example/poster.jpg",
                "https://media.example/clip.mp4",
                "https://img.example/download.png?x=1&amp;y=2",
                "https://img.example/span.gif"
            },
            references.Select(reference => reference.Value));
        Assert.Equal(HtmlMediaReferenceKind.Srcset, references[0].Kind);
        Assert.All(references.Skip(1), reference => Assert.Equal(HtmlMediaReferenceKind.Url, reference.Kind));
    }

    [Fact]
    public void HtmlMediaReferenceScanner_HandlesEmptyAndNullSafeInputs()
    {
        Assert.Empty(HtmlMediaReferenceScanner.Find(string.Empty));
        Assert.Empty(HtmlMediaReferenceScanner.Find("plain text without tags"));
        Assert.Empty(HtmlMediaReferenceScanner.Find("<p>no media references here</p>"));
    }

    [Fact]
    public void HtmlMediaReferenceScanner_IgnoresMalformedTags()
    {
        var html = "<img src=\"https://img.example/ok.jpg\"><img src=\"unterminated";
        var references = HtmlMediaReferenceScanner.Find(html);
        Assert.Single(references);
        Assert.Equal("https://img.example/ok.jpg", references[0].Value);
    }

    [Fact]
    public void HtmlMediaReferenceScanner_PreservesValuePositionsForSubstringReplacement()
    {
        var html = "before <img src=\"https://img.example/a.jpg\"> after";
        var references = HtmlMediaReferenceScanner.Find(html);
        Assert.Single(references);
        var reference = references[0];
        Assert.Equal("https://img.example/a.jpg", html.Substring(reference.ValueStart, reference.ValueLength));
    }

    [Fact]
    public void HtmlMediaReferenceScanner_SkipsNonImageAnchors()
    {
        var html = "<a href=\"https://example.com/page.html\">link</a><a href=\"https://img.example/photo.jpg\">img</a>";
        var references = HtmlMediaReferenceScanner.Find(html);
        Assert.Single(references);
        Assert.Equal("https://img.example/photo.jpg", references[0].Value);
    }

    [Fact]
    public void HtmlMediaReferenceScanner_HandlesAttributesWithExtraWhitespace()
    {
        var html = "<img  src  =  \"https://img.example/a.jpg\" />";
        var references = HtmlMediaReferenceScanner.Find(html);
        Assert.Single(references);
        Assert.Equal("https://img.example/a.jpg", references[0].Value);
    }

    [Fact]
    public void HtmlMediaReferenceScanner_HandlesBooleanAndUnquotedAttributes()
    {
        var html = "<img loading data-id=42 src=\"https://img.example/a.jpg\" />";
        var references = HtmlMediaReferenceScanner.Find(html);
        Assert.Single(references);
        Assert.Equal("https://img.example/a.jpg", references[0].Value);
    }

    [Fact]
    public void HtmlMediaReferenceScanner_AppendOnlyToReturnInDocumentOrder()
    {
        var html = string.Concat(
            "<img src=\"https://img.example/1.jpg\">",
            "<span data-src=\"https://img.example/2.jpg\"></span>",
            "<img src=\"https://img.example/3.jpg\">");
        var references = HtmlMediaReferenceScanner.Find(html);
        Assert.Equal(
            new[]
            {
                "https://img.example/1.jpg",
                "https://img.example/2.jpg",
                "https://img.example/3.jpg"
            },
            references.Select(r => r.Value));
    }

    [Fact]
    public void HtmlMediaReferenceScanner_FastReturnForNonMediaHtml()
    {
        var html = string.Join("", Enumerable.Range(0, 100).Select(_ => "<p>Some text</p>"));
        Assert.Empty(HtmlMediaReferenceScanner.Find(html));
    }

    [Fact]
    public void HtmlMediaReferenceScanner_FastReturnForPureText()
    {
        var html = string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet. ", 200));
        Assert.Empty(HtmlMediaReferenceScanner.Find(html));
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

    private sealed class CountingLocalizer : IImageAssetLocalizer
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        public Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var key = sourceUrl ?? string.Empty;
            _counts[key] = _counts.TryGetValue(key, out var count) ? count + 1 : 1;

            if (key.Contains("/repeat.jpg", StringComparison.Ordinal))
            {
                return Task.FromResult("/assets/uploads/repeat.jpg");
            }

            return Task.FromResult(sourceUrl ?? "/assets/images/noneimg-news.jpg");
        }

        public int GetCallCount(string sourceUrl)
        {
            return _counts.TryGetValue(sourceUrl, out var count) ? count : 0;
        }
    }

    private sealed class ParallelProbeLocalizer : IImageAssetLocalizer
    {
        private int _active;

        public int MaxConcurrency { get; private set; }

        public async Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            if (active > MaxConcurrency)
            {
                MaxConcurrency = active;
            }

            try
            {
                await Task.Delay(25, cancellationToken);
                return sourceUrl ?? "/assets/images/noneimg-news.jpg";
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private sealed class MappingLocalizer : IImageAssetLocalizer
    {
        private readonly IReadOnlyDictionary<string, string> _map;

        public MappingLocalizer(IReadOnlyDictionary<string, string> map)
        {
            _map = map;
        }

        public List<string> ReceivedUrls { get; } = new();

        public Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var key = sourceUrl ?? string.Empty;
            ReceivedUrls.Add(key);
            return Task.FromResult(_map.TryGetValue(key, out var mapped) ? mapped : key);
        }
    }
}
