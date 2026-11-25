using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class SiteLanguageIdentityTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ReadsEachLanguageIdentity()
    {
        var config = Load("""
            languages: [en, zh-CN]
            i18n:
              zh-CN:
                title: 中文站
                description: 中文描述
                organization:
                  name: 中文机构
                  sameAs: [https://weibo.example/org]
            """);
        ConfigValidator.Validate(config);

        var chinese = Assert.Single(config.Site.I18n!).Value;
        Assert.Equal("中文站", chinese.Title);
        Assert.Equal("中文描述", chinese.Description);
        Assert.Equal("中文机构", chinese.Organization!.Name);
        Assert.Equal(["https://weibo.example/org"], chinese.Organization.SameAs);
        Assert.Null(chinese.Organization.Url);
    }

    [Theory]
    [InlineData("i18n:\n  zh-CN:\n    tagline: x", "site.i18n.zh-CN")]
    [InlineData("i18n:\n  zh-CN:\n    organization:\n      type: NewsMediaOrganization", "site.i18n.zh-CN.organization")]
    public void Load_RejectsUnknownKeys(string fragment, string path)
    {
        var ex = Assert.Throws<ConfigException>(() => Load("languages: [en, zh-CN]\n" + fragment));

        Assert.Contains(path, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("languages: [en]\ni18n:\n  en:\n    title: English", "two or more site.languages")]
    [InlineData("languages: [en, zh-CN]\ni18n:\n  fr:\n    title: Français", "site.i18n.fr is not a site language")]
    [InlineData("languages: [en, zh-CN]\ni18n:\n  zh-cn:\n    title: 中文", "site.i18n.zh-cn is not a site language")]
    [InlineData("languages: [en, zh-CN]\ni18n:\n  zh-CN:\n    title: \"  \"", "site.i18n.zh-CN.title must not be blank")]
    public void Validate_RejectsInvalidIdentities(string fragment, string message)
    {
        var config = Load(fragment);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_ReplacesOnlyTheSetFields_AndMergesTheOrganizationFieldByField()
    {
        var site = new SiteConfig
        {
            Name = "s",
            Title = "English",
            Description = "English description",
            Seo = new SeoConfig
            {
                Organization = new SeoOrganizationConfig { Type = "NewsMediaOrganization", Name = "Org", Url = "https://org.example", Logo = "/logo.png", SameAs = ["https://x.example/org"] }
            },
            I18n = new Dictionary<string, SiteLanguageIdentityConfig>
            {
                ["zh-CN"] = new() { Title = "中文", Organization = new SiteLanguageOrganizationConfig { Name = "中文机构" } }
            }
        };

        var chinese = SiteLanguageIdentity.Apply(site, "zh-CN");
        var english = SiteLanguageIdentity.Apply(site, "en");

        Assert.Equal("中文", chinese.Title);
        Assert.Equal("English description", chinese.Description);
        Assert.Equal(
            new SeoOrganizationConfig { Type = "NewsMediaOrganization", Name = "中文机构", Url = "https://org.example", Logo = "/logo.png", SameAs = site.Seo.Organization!.SameAs },
            chinese.Seo.Organization);
        Assert.Same(site, english);
    }

    [Fact]
    public void Apply_CreatesTheOrganizationWhenOnlyTheLanguageDeclaresOne()
    {
        var site = new SiteConfig
        {
            Name = "s",
            Title = "English",
            I18n = new Dictionary<string, SiteLanguageIdentityConfig>
            {
                ["zh-CN"] = new() { Organization = new SiteLanguageOrganizationConfig { Name = "中文机构" } }
            }
        };

        var organization = SiteLanguageIdentity.Apply(site, "zh-CN").Seo.Organization;

        Assert.NotNull(organization);
        Assert.Equal("Organization", organization.Type);
        Assert.Equal("中文机构", organization.Name);
        Assert.Null(site.Seo.Organization);
    }

    private AppConfig Load(string siteFragment)
    {
        var indented = string.Join(
            Environment.NewLine,
            siteFragment.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => $"  {line}"));
        var yaml = $$"""
            site:
              name: identity-test
              title: Identity Test
            {{indented}}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-site-identity-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return ConfigLoader.Load(path);
    }
}
