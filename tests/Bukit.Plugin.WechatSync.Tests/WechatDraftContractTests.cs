using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatDraftContractTests
{
    [Theory]
    [InlineData("中", 32, true)]
    [InlineData("中", 33, false)]
    [InlineData("👩🏽‍💻", 32, true)]
    [InlineData("👩🏽‍💻", 33, false)]
    [InlineData("é", 32, true)]
    [InlineData("é", 33, false)]
    public void ValidateDraft_EnforcesTitleByUnicodeTextElements(string element, int count, bool valid)
    {
        var request = Request() with { Title = string.Concat(Enumerable.Repeat(element, count)) };

        if (valid)
        {
            WechatDraftContract.ValidateDraft(request);
            return;
        }

        var ex = Assert.Throws<WechatDraftContractViolationException>(() => WechatDraftContract.ValidateDraft(request));
        Assert.Equal("plugin.wechat-sync.contract.title.textElements", ex.Code);
    }

    [Fact]
    public void ValidateDraft_EnforcesAuthorAndContentSourceUrlBoundaries()
    {
        WechatDraftContract.ValidateDraft(Request() with
        {
            Author = new string('作', 16),
            ContentSourceUrl = "https://example.com/" + new string('a', 1004)
        });

        var author = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateDraft(Request() with { Author = new string('作', 17) }));
        Assert.Equal("plugin.wechat-sync.contract.author.textElements", author.Code);

        var sourceUrl = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateDraft(Request() with { ContentSourceUrl = "https://example.com/" + new string('a', 1005) }));
        Assert.Equal("plugin.wechat-sync.contract.contentSourceUrl.utf8Bytes", sourceUrl.Code);

        WechatDraftContract.ValidateDraft(Request() with { Digest = new string('摘', 120) });
        var digest = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateDraft(Request() with { Digest = new string('摘', 121) }));
        Assert.Equal("plugin.wechat-sync.contract.digest.textElements", digest.Code);
    }

    [Fact]
    public void ValidateDraft_RequiresContentToBeStrictlyBelowTextAndUtf8Limits()
    {
        WechatDraftContract.ValidateDraft(Request() with { ContentHtml = new string('a', 19_999) });

        var textElements = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateDraft(Request() with { ContentHtml = new string('a', 20_000) }));
        Assert.Equal("plugin.wechat-sync.contract.content.textElements", textElements.Code);

        var oversizedUtf8Content = string.Concat(Enumerable.Repeat("a" + new string('\u0301', 30), 19_999));
        var utf8Bytes = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateDraft(Request() with { ContentHtml = oversizedUtf8Content }));
        Assert.Equal("plugin.wechat-sync.contract.content.utf8Bytes", utf8Bytes.Code);

        var belowUtf8Boundary = "a" + new string('\u0301', 524_287);
        WechatDraftContract.ValidateDraft(Request() with { ContentHtml = belowUtf8Boundary });

        var exactUtf8Boundary = "a" + new string('\u0301', 524_286) + '\u20dd';
        var exactUtf8 = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateDraft(Request() with { ContentHtml = exactUtf8Boundary }));
        Assert.Equal("plugin.wechat-sync.contract.content.utf8Bytes", exactUtf8.Code);
    }

    [Fact]
    public void ValidateInlineImage_RequiresJpegOrPngAndStrictOneMiBBoundary()
    {
        var legal = PngBytes(WechatDraftContract.InlineImageMaxBytesExclusive - 1);
        WechatDraftContract.ValidateInlineImage(legal, "image/png");

        var boundary = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateInlineImage(PngBytes(WechatDraftContract.InlineImageMaxBytesExclusive), "image/png"));
        Assert.Equal("plugin.wechat-sync.contract.inlineImage.bytes", boundary.Code);

        var afterBoundary = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateInlineImage(PngBytes(WechatDraftContract.InlineImageMaxBytesExclusive + 1), "image/png"));
        Assert.Equal("plugin.wechat-sync.contract.inlineImage.bytes", afterBoundary.Code);

        var format = Assert.Throws<WechatDraftContractViolationException>(() =>
            WechatDraftContract.ValidateInlineImage("GIF89a"u8.ToArray(), "image/gif"));
        Assert.Equal("plugin.wechat-sync.contract.inlineImage.format", format.Code);
    }

    [Fact]
    public async Task UploadContentImageAsync_RejectsInvalidBytesBeforeTokenOrHttpActivity()
    {
        using var gateway = new WechatDraftGateway(new SilentLogger(), "app", "secret");

        var ex = await Assert.ThrowsAsync<WechatDraftContractViolationException>(() =>
            gateway.UploadContentImageAsync("GIF89a"u8.ToArray(), "image.gif", "image/gif", CancellationToken.None));

        Assert.Equal("plugin.wechat-sync.contract.inlineImage.format", ex.Code);

        var size = await Assert.ThrowsAsync<WechatDraftContractViolationException>(() =>
            gateway.UploadContentImageAsync(
                PngBytes(WechatDraftContract.InlineImageMaxBytesExclusive),
                "image.png",
                "image/png",
                CancellationToken.None));
        Assert.Equal("plugin.wechat-sync.contract.inlineImage.bytes", size.Code);
    }

    private static WechatDraftRequest Request()
        => new("Title", "Author", "Digest", "<p>content</p>", "https://example.com/", "thumb", false, false);

    private static byte[] PngBytes(int length)
    {
        var bytes = new byte[length];
        new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }.CopyTo(bytes, 0);
        return bytes;
    }

    private sealed class SilentLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
