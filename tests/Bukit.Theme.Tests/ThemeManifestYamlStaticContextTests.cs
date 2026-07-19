using Bukit.Theme;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bukit.Theme.Tests;

public sealed class ThemeManifestYamlStaticContextTests
{
    [Fact]
    public void StaticContext_DeserializesRegisteredThemeManifestGraph()
    {
        var deserializer = new StaticDeserializerBuilder(new ThemeManifestYamlStaticContext())
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var manifest = deserializer.Deserialize<ThemeManifestV2>("""
            name: deterministic-theme
            display_name: Deterministic Theme
            capabilities:
              seo: true
              i18n: true
            layouts:
              default: layouts/default.html
            assets:
              css:
                - assets/site.css
              js:
                - assets/site.js
            """);

        Assert.Equal("deterministic-theme", manifest.Name);
        Assert.Equal("Deterministic Theme", manifest.DisplayName);
        Assert.True(manifest.Capabilities.Seo);
        Assert.True(manifest.Capabilities.I18n);
        Assert.Equal("layouts/default.html", manifest.Layouts?["default"]);
        Assert.Equal(["assets/site.css"], manifest.Assets.Css);
        Assert.Equal(["assets/site.js"], manifest.Assets.Js);
    }

    [Fact]
    public void StaticContext_SerializesRegisteredThemeManifestGraph()
    {
        var serializer = new StaticSerializerBuilder(new ThemeManifestYamlStaticContext())
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var manifest = new ThemeManifestV2
        {
            Name = "deterministic-theme",
            DisplayName = "Deterministic Theme",
            Capabilities = new ThemeCapabilities { Seo = true },
            Assets = new ThemeAssetsConfig { Css = ["assets/site.css"] }
        };

        var yaml = serializer.Serialize(manifest);

        Assert.Contains("name: deterministic-theme", yaml, StringComparison.Ordinal);
        Assert.Contains("display_name: Deterministic Theme", yaml, StringComparison.Ordinal);
        Assert.Contains("seo: true", yaml, StringComparison.Ordinal);
        Assert.Contains("- assets/site.css", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticContext_PreservesPublicSurfaceAndKnownTypes()
    {
        var contextType = typeof(ThemeManifestYamlStaticContext);
        var context = new ThemeManifestYamlStaticContext();
        Type[] registeredTypes =
        [
            typeof(ThemeManifestV2),
            typeof(ThemeCapabilities),
            typeof(ThemeAssetsConfig),
            typeof(ThemePageTemplateDefinition),
            typeof(ThemePageTemplateAccept),
            typeof(ThemeTemplateDefinition),
            typeof(ThemeTemplateAccept),
            typeof(ThemeSectionDefinition),
            typeof(ThemeComponentDefinition),
            typeof(ThemeVariantDefinition),
            typeof(ThemeDataBindingDefinition)
        ];

        Assert.NotNull(contextType.GetConstructor(Type.EmptyTypes));
        Assert.NotNull(context.GetFactory());
        Assert.NotNull(context.GetTypeInspector());
        Assert.NotNull(context.GetTypeResolver());
        Assert.All(registeredTypes, type => Assert.True(context.IsKnownType(type), type.FullName));
        Assert.False(context.IsKnownType(typeof(ThemeManifestYamlStaticContextTests)));

        Assert.True(typeof(StaticTypeInspector).IsPublic);
        Assert.NotNull(typeof(StaticTypeInspector).GetConstructor([typeof(ITypeResolver)]));
    }
}
