namespace Bukit.Importing;

internal static class NotionPropertyNaming
{
    internal static string Canonicalize(string name)
        => name.Trim().ToLowerInvariant() switch
        {
            "link" => "Link",
            "url" => "Url",
            "href" => "Href",
            "order" or "sort_order" => "Order",
            "enabled" => "Enabled",
            _ => string.Concat(name.Trim().Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]))
        };

    internal static bool IsCore(string name)
        => name is "Title" or "Slug" or "Type" or "Summary" or "Content" or "Language" or
           "Published" or "SeoTitle" or "SeoDescription";
}
