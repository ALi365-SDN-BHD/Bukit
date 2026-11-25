using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ThemeImagesConfigTests : IDisposable
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
    public void Load_ReadsTrimmedSizesAttributeAndDefaultsToUnset()
    {
        Assert.Equal("(max-width: 768px) 100vw, 768px", Load("sizesAttribute: \" (max-width: 768px) 100vw, 768px \"").Theme.Images!.SizesAttribute);
        Assert.Null(Load("quality: 80").Theme.Images!.SizesAttribute);
    }

    [Theory]
    [InlineData("sizesAttribute: \"  \"", "must not be blank")]
    [InlineData("sizesAttribute: \"auto, 100vw\"", "without auto")]
    [InlineData("sizesAttribute: [100vw]", "theme.images.sizesAttribute")]
    public void Load_RejectsInvalidValues(string fragment, string message)
    {
        var ex = Assert.Throws<ConfigException>(() => Load(fragment));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("quality: 80", "all")]
    [InlineData("fallbackVariants: none", "none")]
    [InlineData("fallbackVariants: all", "all")]
    public void Load_ReadsFallbackVariants(string fragment, string expected)
        => Assert.Equal(expected, Load(fragment).Theme.Images!.FallbackVariants);

    [Fact]
    public void Load_RejectsUnknownFallbackVariants()
    {
        var ex = Assert.Throws<ConfigException>(() => Load("fallbackVariants: some"));

        Assert.Contains("theme.images.fallbackVariants must be all|none", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WebpOnlyVariants_RequiresNoneAndWebp()
    {
        Assert.True(new ImageOptimizationConfig { FallbackVariants = "none" }.WebpOnlyVariants);
        Assert.False(new ImageOptimizationConfig { FallbackVariants = "none", Formats = [] }.WebpOnlyVariants);
        Assert.False(new ImageOptimizationConfig().WebpOnlyVariants);
    }

    private AppConfig Load(string imagesFragment)
    {
        var yaml = $$"""
            site:
              name: images-test
              title: Images Test
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            theme:
              images:
                enabled: true
                {{imagesFragment}}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-images-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return ConfigLoader.Load(path);
    }
}
