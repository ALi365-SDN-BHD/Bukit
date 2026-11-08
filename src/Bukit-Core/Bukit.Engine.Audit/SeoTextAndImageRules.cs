using Bukit.Config;
using Bukit.Engine.Primitives;
using Bukit.Rendering;

namespace Bukit.Engine.Audit;

/// <summary>
/// Per-route SEO text and social image checks. Lengths are measured as display width,
/// so CJK text is not undercounted.
/// </summary>
internal static class SeoTextAndImageRules
{
    internal static void AnalyzeModel(AppConfig config, string route, SeoModel model, string outputDir, List<SeoAuditIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            issues.Add(new("error", "seo.title_missing", route, "SEO title is missing."));
        }
        else if (SeoTextWidth.Measure(model.Title) is var titleWidth && titleWidth > SeoTextWidth.TitleMax)
        {
            issues.Add(new("warning", "seo.title_too_long", route, $"SEO title display width is {titleWidth}, recommended maximum is {SeoTextWidth.TitleMax}."));
        }

        if (string.IsNullOrWhiteSpace(model.Description))
        {
            issues.Add(new("warning", "seo.description_missing", route, "SEO description is missing."));
        }
        else if (SeoTextWidth.Measure(model.Description) is var descriptionWidth && descriptionWidth > SeoTextWidth.DescriptionMax)
        {
            issues.Add(new("warning", "seo.description_too_long", route, $"SEO description display width is {descriptionWidth}, recommended maximum is {SeoTextWidth.DescriptionMax}."));
        }

        ImageMetadataReader.AnalyzeImage(config, "seo.og_image", route, model.Og.Image, outputDir, issues);
        ImageMetadataReader.AnalyzeImage(config, "seo.twitter_image", route, model.Twitter.Image, outputDir, issues);
    }

    internal static void AnalyzeDocumentTitleWidth(string route, string actual, List<SeoAuditIssue> issues)
    {
        var width = SeoTextWidth.Measure(actual);
        if (width > SeoTextWidth.TitleMax)
        {
            issues.Add(new("warning", "seo.document_title_too_long", route, $"Final HTML document title display width is {width}, recommended maximum is {SeoTextWidth.TitleMax}."));
        }
    }
}
