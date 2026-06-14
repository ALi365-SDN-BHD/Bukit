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

    [Fact]
    public void ThemeManifest_Load_ParsesManifestAndCountsDeclaredParams()
    {
        File.WriteAllText(Path.Combine(_tempDir, "theme.yaml"), """
name: artisan
version: 1.2.3
description: Minimal theme
tags:
  - clean
  - docs
params:
  - key: brand
    label: Brand
    type: text
    default: Bukit
  - key: accent
    label: Accent
    type: color
""");

        var manifest = ThemeManifest.Load(_tempDir);

        Assert.NotNull(manifest);
        Assert.Equal("artisan", manifest!.Name);
        Assert.Equal("1.2.3", manifest.Version);
        Assert.Equal(2, manifest.DeclaredParamCount);
        Assert.Equal(["clean", "docs"], manifest.Tags);
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
themes:
  - name: starter
    version: 0.1.0
    description: Starter theme
    download:
      url: https://example.com/starter.zip
      sha256: abc123
""";

        var parsed = RegistryIndex.Parse(validYaml);

        Assert.NotNull(parsed);
        Assert.Equal("2026-06-14", parsed!.Registry!.Updated);
        var theme = Assert.Single(parsed.Themes);
        Assert.Equal("starter", theme.Name);
        Assert.Equal("https://example.com/starter.zip", theme.Download!.Url);

        Assert.Null(RegistryIndex.Parse("themes: [broken"));
    }
}
