using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content.Media;

/// <summary>
/// The page whose images are being localized, for warnings. Set inside the async method that
/// rewrites one document, so it flows to that document's localizer calls and is restored when
/// the method returns.
/// </summary>
internal static class MediaPageContext
{
    private static readonly AsyncLocal<string?> CurrentPage = new();

    internal static string? Current => CurrentPage.Value;

    internal static void Enter(ContentDocument document)
        => CurrentPage.Value = Describe(document);

    internal static string Describe(ContentDocument document)
    {
        var slug = (document.Slug ?? string.Empty).Trim();
        return slug.Length > 0 ? slug : (document.Id ?? string.Empty).Trim();
    }
}
