using System.Text.Json;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class SeoOrganizationConfigContractTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    public void Load_NewsMediaOrganization_BindsTypeSameAsAndStrictFields()
    {
        var config = Load("""
            site:
              name: silk-road-news
              title: 丝路商讯
              url: https://silushangxun.com/
              seo:
                organization:
                  type: NewsMediaOrganization
                  name: 丝路商讯
                  url: /about/
                  logo: /assets/images/social-default.png
                  sameAs:
                    - https://www.linkedin.com/company/silushangxun/
                    - https://www.youtube.com/@silushangxun
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

        var organization = Assert.IsType<SeoOrganizationConfig>(config.Site.Seo.Organization);
        Assert.Equal("NewsMediaOrganization", organization.Type);
        Assert.Equal(
            [
                "https://www.linkedin.com/company/silushangxun/",
                "https://www.youtube.com/@silushangxun"
            ],
            organization.SameAs);
    }

    [Fact]
    public void Schema_OrganizationTypeAndSameAsExposePublicContract()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var organization = schema.RootElement
            .GetProperty("properties")
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("seo")
            .GetProperty("properties")
            .GetProperty("organization")
            .GetProperty("properties");

        var type = organization.GetProperty("type");
        Assert.Equal("string", type.GetProperty("type").GetString());
        Assert.Equal(
            ["Organization", "NewsMediaOrganization"],
            type.GetProperty("enum").EnumerateArray().Select(static value => value.GetString()));

        var sameAs = organization.GetProperty("sameAs");
        Assert.Equal("array", sameAs.GetProperty("type").GetString());
        Assert.Equal("string", sameAs.GetProperty("items").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("Person")]
    [InlineData("")]
    public void Validate_UnsupportedOrEmptyOrganizationType_ThrowsConfigException(string type)
    {
        var config = Load($$"""
            site:
              name: silk-road-news
              title: 丝路商讯
              seo:
                organization:
                  type: '{{type}}'
                  name: 丝路商讯
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

        var exception = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("site.seo.organization.type", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnknownOrganizationField_RemainsRejected()
    {
        var exception = Assert.Throws<ConfigException>(() => Load("""
            site:
              name: silk-road-news
              title: 丝路商讯
              seo:
                organization:
                  type: Organization
                  guessedUrl: https://example.com/
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("site.seo.organization.guessedUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_LegacyOrganization_DefaultsTypeAndSameAs()
    {
        var config = Load("""
            site:
              name: legacy-site
              title: Legacy Site
              seo:
                organization:
                  name: Legacy Publisher
                  url: https://example.com/about/
                  logo: https://example.com/logo.png
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

        var organization = Assert.IsType<SeoOrganizationConfig>(config.Site.Seo.Organization);
        Assert.Equal("Organization", organization.Type);
        Assert.Empty(organization.SameAs);
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private AppConfig Load(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-seo-organization-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return ConfigLoader.Load(path);
    }
}
