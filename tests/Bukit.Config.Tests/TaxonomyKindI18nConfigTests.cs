using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class TaxonomyKindI18nConfigTests : IDisposable
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
    public void Load_ReadsEachLanguagesLabels_AndLabelsForFallsBackToTheKindsOwn()
    {
        var config = Load("[en, zh-CN]", "i18n:\n  en:\n    title: Insight tags\n    singularTitlePrefix: Tag");
        ConfigValidator.Validate(config);

        var kind = Assert.Single(config.Taxonomy.Kinds!);
        var english = kind.LabelsFor("en");
        Assert.Equal("Insight tags", english.Title);
        Assert.Equal("按标签浏览。", english.Description);
        Assert.Equal("Tag", english.SingularTitlePrefix);
        Assert.Equal(new TaxonomyKindLabelsConfig { Title = "资讯标签", Description = "按标签浏览。", SingularTitlePrefix = "标签" }, kind.LabelsFor("zh-CN"));
    }

    [Theory]
    [InlineData("[en]", "i18n:\n  en:\n    title: Tags", "requires two or more site.languages")]
    [InlineData("[en, zh-CN]", "i18n:\n  fr:\n    title: Étiquettes", "taxonomy.kinds[0].i18n.fr is not a site language")]
    [InlineData("[en, zh-CN]", "i18n:\n  en:\n    singularTitlePrefix: \"  \"", "taxonomy.kinds[0].i18n.en.singularTitlePrefix must be a non-empty string")]
    public void Validate_RejectsInvalidLabels(string languages, string fragment, string message)
    {
        var config = Load(languages, fragment);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsUnknownLabelKeys()
    {
        var ex = Assert.Throws<ConfigException>(() => Load("[en, zh-CN]", "i18n:\n  en:\n    template: x.html"));

        Assert.Contains("taxonomy.kinds[0].i18n.en", ex.Message, StringComparison.Ordinal);
    }

    private AppConfig Load(string languages, string kindFragment)
    {
        var indented = string.Join(
            Environment.NewLine,
            kindFragment.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => $"      {line}"));
        var yaml = $$"""
            site:
              name: kind-labels-test
              title: Kind Labels Test
              languages: {{languages}}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            taxonomy:
              kinds:
                - key: tags
                  title: 资讯标签
                  description: 按标签浏览。
                  singularTitlePrefix: 标签
            {{indented}}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-kind-labels-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return ConfigLoader.Load(path);
    }
}
