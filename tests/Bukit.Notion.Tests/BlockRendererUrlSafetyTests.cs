using System.Text.Json;
using Bukit.Notion.Rendering;
using Bukit.Notion.Rendering.BlockRenderers;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class BlockRendererUrlSafetyTests
{
    #region AudioBlockRenderer (ForMedia)

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    public async Task AudioBlockRenderer_DangerousUrl_ReturnsNull(string fileUrl)
    {
        var json = $"{{\"audio\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new AudioBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData("https://example.com/audio.mp3")]
    [InlineData("/assets/audio.mp3")]
    public async Task AudioBlockRenderer_SafeUrl_RendersHtml(string fileUrl)
    {
        var json = $"{{\"audio\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new AudioBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains(fileUrl, html);
    }

    #endregion

    #region ImageBlockRenderer (ForMedia)

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.com/image.png")]
    [InlineData("//evil.com/x.js")]
    [InlineData("//cdn.evil.com/image.png")]
    public async Task ImageBlockRenderer_DangerousUrl_ReturnsNull(string fileUrl)
    {
        var json = $"{{\"image\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new ImageBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData("https://example.com/resource")]
    [InlineData("http://example.com/resource")]
    [InlineData("/assets/local-file.png")]
    public async Task ImageBlockRenderer_SafeUrl_RendersHtml(string fileUrl)
    {
        var json = $"{{\"image\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new ImageBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains(fileUrl, html);
    }

    #endregion

    #region VideoBlockRenderer (ForMedia / ForEmbed)

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.com/video.mp4")]
    [InlineData("//evil.com/x.js")]
    [InlineData("//cdn.evil.com/video.mp4")]
    public async Task VideoBlockRenderer_DangerousUrl_ReturnsNull(string fileUrl)
    {
        var json = $"{{\"video\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new VideoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData("https://example.com/video.mp4")]
    [InlineData("http://example.com/video.mp4")]
    [InlineData("/assets/video.mp4")]
    public async Task VideoBlockRenderer_SafeUrl_RendersHtml(string fileUrl)
    {
        var json = $"{{\"video\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new VideoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.com/watch?v=abc123")]
    public async Task VideoBlockRenderer_DangerousYouTubeUrl_ReturnsNull(string url)
    {
        var json = $"{{\"video\":{{\"type\":\"external\",\"external\":{{\"url\":\"{url}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new VideoBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    #endregion

    #region EmbedBlockRenderer (ForEmbed)

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.com")]
    [InlineData("//evil.com/x.js")]
    [InlineData("//cdn.evil.com/widget")]
    [InlineData("http://example.com/widget")]
    public async Task EmbedBlockRenderer_DangerousUrl_ReturnsNull(string url)
    {
        var json = $"{{\"embed\":{{\"url\":\"{url}\"}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new EmbedBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData("https://example.com/widget")]
    [InlineData("/local/widget.html")]
    public async Task EmbedBlockRenderer_SafeUrl_RendersHtml(string url)
    {
        var json = $"{{\"embed\":{{\"url\":\"{url}\"}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new EmbedBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
    }

    #endregion

    #region BookmarkBlockRenderer (ForLink)

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.com")]
    [InlineData("//evil.com/x.js")]
    [InlineData("//cdn.evil.com/page")]
    public async Task BookmarkBlockRenderer_DangerousUrl_ReturnsNull(string url)
    {
        var json = $"{{\"bookmark\":{{\"url\":\"{url}\"}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new BookmarkBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData("https://example.com/resource")]
    [InlineData("http://example.com/resource")]
    [InlineData("/internal/page")]
    [InlineData("mailto:user@example.com")]
    [InlineData("tel:+1234567890")]
    public async Task BookmarkBlockRenderer_SafeUrl_RendersHtml(string url)
    {
        var json = $"{{\"bookmark\":{{\"url\":\"{url}\"}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new BookmarkBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains(url, html);
    }

    #endregion

    #region FileBlockRenderer (ForLink)

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.com/file.bin")]
    [InlineData("//evil.com/x.js")]
    [InlineData("//cdn.evil.com/data.bin")]
    public async Task FileBlockRenderer_DangerousUrl_ReturnsNull(string fileUrl)
    {
        var json = $"{{\"file\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new FileBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData("https://example.com/resource")]
    [InlineData("http://example.com/resource")]
    [InlineData("/assets/local-file.bin")]
    [InlineData("mailto:user@example.com")]
    [InlineData("tel:+1234567890")]
    public async Task FileBlockRenderer_SafeUrl_RendersHtml(string fileUrl)
    {
        var json = $"{{\"file\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new FileBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
    }

    #endregion

    #region PdfBlockRenderer (ForMedia)

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.com/doc.pdf")]
    [InlineData("//evil.com/x.js")]
    [InlineData("//cdn.evil.com/doc.pdf")]
    public async Task PdfBlockRenderer_DangerousUrl_ReturnsNull(string fileUrl)
    {
        var json = $"{{\"pdf\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new PdfBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData("https://example.com/doc.pdf")]
    [InlineData("http://example.com/doc.pdf")]
    [InlineData("/assets/doc.pdf")]
    public async Task PdfBlockRenderer_SafeUrl_RendersHtml(string fileUrl)
    {
        var json = $"{{\"pdf\":{{\"type\":\"external\",\"external\":{{\"url\":\"{fileUrl}\"}}}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new PdfBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
    }

    #endregion

    #region LinkPreviewBlockRenderer (ForLink)

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("//evil.com")]
    [InlineData("//evil.com/x.js")]
    [InlineData("//cdn.evil.com/page")]
    public async Task LinkPreviewBlockRenderer_DangerousUrl_ReturnsNull(string url)
    {
        var json = $"{{\"link_preview\":{{\"url\":\"{url}\"}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new LinkPreviewBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.Null(html);
    }

    [Theory]
    [InlineData("https://example.com/resource")]
    [InlineData("http://example.com/resource")]
    [InlineData("/internal/page")]
    [InlineData("mailto:user@example.com")]
    [InlineData("tel:+1234567890")]
    public async Task LinkPreviewBlockRenderer_SafeUrl_RendersHtml(string url)
    {
        var json = $"{{\"link_preview\":{{\"url\":\"{url}\"}}}}";
        using var doc = JsonDocument.Parse(json);

        var html = await new LinkPreviewBlockRenderer().RenderAsync(doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(html);
        Assert.Contains(url, html);
    }

    #endregion

    #region NotionRichTextRenderer dangerous URL extension

    [Fact]
    public void Render_ProtocolRelativeUrl_NoAnchor()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Evil link",
            "href": "//evil.com"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.DoesNotContain("<a", html);
        Assert.Contains("Evil link", html);
    }

    [Fact]
    public void Render_FileUrl_NoAnchor()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Local file",
            "href": "file:///etc/passwd"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.DoesNotContain("<a", html);
        Assert.Contains("Local file", html);
    }

    [Fact]
    public void Render_VbscriptUrl_NoAnchor()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "VB script",
            "href": "vbscript:msgbox(1)"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.DoesNotContain("<a", html);
        Assert.Contains("VB script", html);
    }

    [Fact]
    public void Render_TelLink_PassesThrough()
    {
        using var doc = JsonDocument.Parse("""
        [
          {
            "type": "text",
            "plain_text": "Call us",
            "href": "tel:+1234567890"
          }
        ]
        """);

        var html = NotionRichTextRenderer.Render(doc.RootElement);

        Assert.Contains("tel:+1234567890", html);
        Assert.Contains("Call us</a>", html);
    }

    #endregion
}
