namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class RssPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "rss";
    public string Version => "2.0.0";

    public void AfterBuild(BuildContext context)
    {
        if (context.Config.Site.Languages is { Count: > 0 } && context.Config.Site.RssMode.Equals("merged", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var siteUrl = context.Config.Site.Url;
        if (string.IsNullOrWhiteSpace(siteUrl))
        {
            return;
        }

        RssGenerator.Generate(
            outputDir: context.OutputDir,
            siteUrl: siteUrl,
            baseUrl: context.BaseUrl,
            siteTitle: context.Config.Site.Title,
            collections: context.Config.Site.Collections,
            routed: context.Routed,
            bodyStore: context.BodyStore);
    }
}
