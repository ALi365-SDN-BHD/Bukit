using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record ResolvedSeoImage(string? Url, SeoImageSource Source);

internal static class SeoImageResolver
{
    internal static ResolvedSeoImage ResolveForContent(
        AppConfig config,
        string baseUrl,
        ContentDocument document)
    {
        var fields = document.CustomFields;
        var record = document.Record;

        var explicitImage = SeoModelBuilder.FirstTextField(fields, "seo_image")
            ?? SeoModelBuilder.FirstTextField(fields, "og_image")
            ?? SeoModelBuilder.FirstTextField(fields, "cover")
            ?? SeoModelBuilder.FirstTextField(fields, "image");
        if (explicitImage is not null)
        {
            return new ResolvedSeoImage(
                SeoModelBuilder.BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, explicitImage),
                SeoImageSource.ExplicitField);
        }

        var mediaImage = record.Media
            .FirstOrDefault(media => string.Equals(media.Kind, "image", StringComparison.OrdinalIgnoreCase))
            ?.Url;
        if (mediaImage is not null)
        {
            return new ResolvedSeoImage(
                SeoModelBuilder.BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, mediaImage),
                SeoImageSource.ContentMedia);
        }

        if (!string.IsNullOrWhiteSpace(config.Site.Seo.DefaultImage))
        {
            return new ResolvedSeoImage(
                SeoModelBuilder.BuildMaybeAbsoluteUrl(config.Site.Url, baseUrl, config.Site.Seo.DefaultImage),
                SeoImageSource.SiteDefault);
        }

        return new ResolvedSeoImage(null, SeoImageSource.None);
    }
}
