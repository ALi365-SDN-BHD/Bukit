using Bukit.Rendering;

namespace Bukit.Engine.PublishAuditRules;

internal static class ArticleImageAuditRules
{
    internal static void Analyze(PublishDocument document, List<PublishAuditIssue> issues)
    {
        if (!PublishDocumentAuditScope.IsContentBacked(document))
        {
            return;
        }

        if (document.SeoModel is not { ImageSource: SeoImageSource.SiteDefault })
        {
            return;
        }

        if (!document.SchemaTypes.Any(type => type is "Article" or "BlogPosting" or "NewsArticle"))
        {
            return;
        }

        issues.Add(new PublishAuditIssue(
            "warning",
            "seo.article_image_uses_site_default",
            document.RouteUrl,
            "Article SEO image falls back to the configured site default image; the image is generic and requires editorial review."));
    }
}
