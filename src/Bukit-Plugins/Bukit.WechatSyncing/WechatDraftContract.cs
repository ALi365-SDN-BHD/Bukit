using System.Globalization;
using System.Text;

namespace Bukit.WechatSyncing;

/// <summary>
/// Local enforcement of the WeChat draft and inline-image limits. Official "字"
/// limits are interpreted as Unicode text elements so combined and ZWJ emoji
/// sequences are counted as one element and never split.
/// </summary>
internal static class WechatDraftContract
{
    internal const int TitleMaxTextElements = 32;
    internal const int AuthorMaxTextElements = 16;
    internal const int DigestMaxTextElements = 120;
    internal const int ContentMaxTextElementsExclusive = 20_000;
    internal const int ContentMaxUtf8BytesExclusive = 1 * 1024 * 1024;
    internal const int ContentSourceUrlMaxUtf8Bytes = 1 * 1024;
    internal const int InlineImageMaxBytesExclusive = 1 * 1024 * 1024;

    internal static void ValidateDraft(WechatDraftRequest request)
    {
        ValidateTextElements(request.Title, TitleMaxTextElements, "title");
        ValidateTextElements(request.Author, AuthorMaxTextElements, "author");
        ValidateTextElements(request.Digest, DigestMaxTextElements, "digest");
        ValidateContent(request.ContentHtml);

        if (Encoding.UTF8.GetByteCount(request.ContentSourceUrl ?? string.Empty) > ContentSourceUrlMaxUtf8Bytes)
        {
            throw Violation("contentSourceUrl.utf8Bytes", $"content_source_url exceeds {ContentSourceUrlMaxUtf8Bytes} UTF-8 bytes.");
        }
    }

    internal static void ValidateInlineImage(byte[] bytes, string? contentType)
    {
        if (bytes is null || bytes.Length == 0)
        {
            throw Violation("inlineImage.empty", "inline uploadimg bytes are empty.");
        }

        if (bytes.Length >= InlineImageMaxBytesExclusive)
        {
            throw Violation("inlineImage.bytes", $"inline uploadimg must be smaller than {InlineImageMaxBytesExclusive} bytes.");
        }

        var detected = WechatSyncHelpers.DetectImageContentType(bytes);
        if (detected is not "image/jpeg" and not "image/png")
        {
            throw Violation("inlineImage.format", "inline uploadimg must be JPEG or PNG.");
        }

    }

    internal static int CountTextElements(string? value)
        => new StringInfo(value ?? string.Empty).LengthInTextElements;

    internal static string TruncateTextElements(string? value, int maxTextElements)
    {
        var text = value ?? string.Empty;
        if (maxTextElements <= 0 || CountTextElements(text) <= maxTextElements)
        {
            return maxTextElements <= 0 ? string.Empty : text;
        }

        return new StringInfo(text).SubstringByTextElements(0, maxTextElements);
    }

    private static void ValidateTextElements(string? value, int maximum, string field)
    {
        if (CountTextElements(value) > maximum)
        {
            throw Violation($"{field}.textElements", $"{field} exceeds {maximum} Unicode text elements.");
        }
    }

    private static void ValidateContent(string? value)
    {
        if (CountTextElements(value) >= ContentMaxTextElementsExclusive)
        {
            throw Violation("content.textElements", $"content must contain fewer than {ContentMaxTextElementsExclusive} Unicode text elements.");
        }

        if (Encoding.UTF8.GetByteCount(value ?? string.Empty) >= ContentMaxUtf8BytesExclusive)
        {
            throw Violation("content.utf8Bytes", $"content must be smaller than {ContentMaxUtf8BytesExclusive} UTF-8 bytes.");
        }
    }

    private static WechatDraftContractViolationException Violation(string field, string message)
        => new($"plugin.wechat-sync.contract.{field}", message);
}

internal sealed class WechatDraftContractViolationException : InvalidOperationException
{
    internal WechatDraftContractViolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    internal string Code { get; }
}
