using System.Globalization;
using Bukit.Config;

namespace Bukit.Engine.Incremental;

internal sealed class SiteIdentityAndModesContributor : IRenderDependencyContributor
{
    public string Name => "site-identity-and-modes";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var site = context.Config.Site;
        writer.AppendUtf8(site.Title);
        writer.AppendNewline();
        writer.AppendUtf8(site.Description);
        writer.AppendNewline();
        writer.AppendUtf8(site.BaseUrl);
        writer.AppendNewline();
        writer.AppendUtf8(site.Language);
        writer.AppendNewline();
        writer.AppendUtf8(site.Url);
        writer.AppendNewline();
        writer.AppendUtf8(context.SiteModel.BuildYear.ToString(CultureInfo.InvariantCulture));
        writer.AppendNewline();

        if (site.Languages is { Count: > 0 })
        {
            foreach (var language in site.Languages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                writer.AppendNewline();
                writer.AppendUtf8(language);
            }
        }

        writer.AppendUtf8(site.DefaultLanguage);
        writer.AppendNewline();
        writer.AppendUtf8(site.SitemapMode);
        writer.AppendNewline();
        writer.AppendUtf8(SiteModeResolver.ResolveFeedMode(site));
        writer.AppendNewline();
        writer.AppendUtf8(SiteModeResolver.ResolveSearchMode(site));
        writer.AppendNewline();
    }
}
