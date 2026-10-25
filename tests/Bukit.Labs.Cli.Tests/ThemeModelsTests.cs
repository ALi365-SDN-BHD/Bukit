using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class ThemeModelsTests : IDisposable
{
    private readonly string _tempDir;

    public ThemeModelsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-labs-theme-models-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Theory]
    [InlineData(typeof(ThemeManifest))]
    [InlineData(typeof(ThemeParam))]
    [InlineData(typeof(RegistryIndex))]
    [InlineData(typeof(RegistryMeta))]
    [InlineData(typeof(RegistryThemeEntry))]
    [InlineData(typeof(RegistryDownload))]
    public void ThemeYamlStaticContext_CreatesRegisteredModels(Type type)
    {
        var context = new ThemeYamlStaticContext();

        Assert.IsType(type, context.GetFactory().Create(type));
        Assert.True(context.IsKnownType(type));
    }

    [Fact]
    public void ThemeManifest_Load_ParsesManifestAndCountsDeclaredParams()
    {
        File.WriteAllText(Path.Combine(_tempDir, "theme.yaml"), """
name: artisan
version: 1.2.3
description: Minimal theme
author: Bukit
license: MIT
homepage: https://example.com/artisan
thumbnail: preview.png
requires_bukit: 0.1.0
future_setting:
  enabled: true
tags:
  - clean
  - docs
params:
  - key: brand
    label: Brand
    type: text
    default: Bukit
    future_setting: ignored
  - key: accent
    label: Accent
    type: color
""");

        var manifest = ThemeManifest.Load(_tempDir);

        Assert.NotNull(manifest);
        Assert.Equal("artisan", manifest!.Name);
        Assert.Equal("1.2.3", manifest.Version);
        Assert.Equal("Minimal theme", manifest.Description);
        Assert.Equal("Bukit", manifest.Author);
        Assert.Equal("MIT", manifest.License);
        Assert.Equal("https://example.com/artisan", manifest.Homepage);
        Assert.Equal("preview.png", manifest.Thumbnail);
        Assert.Equal("0.1.0", manifest.RequiresBukit);
        Assert.Equal(2, manifest.DeclaredParamCount);
        Assert.Equal(["clean", "docs"], manifest.Tags);
        Assert.Equal(new ThemeParam { Key = "brand", Label = "Brand", Type = "text", Default = "Bukit" }, manifest.Params[0]);
        Assert.Equal(new ThemeParam { Key = "accent", Label = "Accent", Type = "color" }, manifest.Params[1]);
    }

    [Fact]
    public void ThemeManifest_Load_MissingManifest_ReturnsNull()
    {
        Assert.Null(ThemeManifest.Load(_tempDir));
    }

    [Fact]
    public void ThemeManifest_Load_InvalidYaml_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_tempDir, "theme.yaml"), "name: [broken");

        Assert.Null(ThemeManifest.Load(_tempDir));
    }

    [Fact]
    public void RegistryIndex_Parse_ParsesValidYaml_AndRejectsInvalidYaml()
    {
        const string validYaml = """
registry:
  updated: 2026-06-14
  version: 1
  bukit_min_version: 0.1.0
  future_setting: ignored
themes:
  - name: starter
    version: 0.1.0
    description: Starter theme
    author: Bukit
    license: MIT
    homepage: https://example.com/starter
    thumbnail: preview.png
    requires_bukit: 0.1.0
    tags: [clean, docs]
    params:
      - key: brand
        label: Brand
        type: text
        default: Bukit
        future_setting: ignored
    download:
      url: https://example.com/starter.zip
      sha256: abc123
      future_setting: ignored
    future_setting:
      enabled: true
future_setting: ignored
""";

        var parsed = RegistryIndex.Parse(validYaml);

        Assert.NotNull(parsed);
        Assert.Equal("2026-06-14", parsed!.Registry!.Updated);
        Assert.Equal("1", parsed.Registry.Version);
        Assert.Equal("0.1.0", parsed.Registry.BukitMinVersion);
        var theme = Assert.Single(parsed.Themes);
        Assert.Equal("starter", theme.Name);
        Assert.Equal("0.1.0", theme.Version);
        Assert.Equal("Starter theme", theme.Description);
        Assert.Equal("Bukit", theme.Author);
        Assert.Equal("MIT", theme.License);
        Assert.Equal("https://example.com/starter", theme.Homepage);
        Assert.Equal("preview.png", theme.Thumbnail);
        Assert.Equal("0.1.0", theme.RequiresBukit);
        Assert.Equal(["clean", "docs"], theme.Tags);
        Assert.Equal(new ThemeParam { Key = "brand", Label = "Brand", Type = "text", Default = "Bukit" }, Assert.Single(theme.Params));
        Assert.Equal("https://example.com/starter.zip", theme.Download!.Url);
        Assert.Equal("abc123", theme.Download.Sha256);

        Assert.Null(RegistryIndex.Parse("themes: [broken"));
    }
}
