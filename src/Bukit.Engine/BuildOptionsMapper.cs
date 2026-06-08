using Bukit.Config;

namespace Bukit.Engine;

internal static class BuildOptionsMapper
{
    internal static AppConfig ToAppConfig(BuildOptions options, string outputDirName)
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = options.SiteTitle,
                Title = options.SiteTitle,
                Language = "en",
                BaseUrl = options.BaseUrl,
                Url = options.SiteUrl,
                OutputPathEncoding = options.OutputPathEncoding,
                Seo = new SeoConfig { Enabled = false }
            },
            Build = new BuildConfig
            {
                Output = outputDirName,
                Clean = options.Clean
            },
            Content = new ContentConfig
            {
                Provider = "sources",
                Sources = new[]
                {
                    new ContentSourceConfig
                    {
                        Type = "markdown",
                        Name = "page",
                        Collection = "page",
                        Markdown = new MarkdownConfig { Dir = "content" }
                    }
                }
            }
        };
    }
}
