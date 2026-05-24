using System.Text.Json;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigJsonSchemaGeneratorTests
{
    [Fact]
    public void Generate_ReturnsObjectSchemaForAppConfigWithRequiredRoots()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.Contains("site", root.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("content", root.GetProperty("required").EnumerateArray().Select(x => x.GetString()));

        var properties = root.GetProperty("properties");
        Assert.True(properties.TryGetProperty("site", out var site));
        Assert.True(site.GetProperty("properties").TryGetProperty("title", out var title));
        Assert.Equal("string", title.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("build", out var build));
        Assert.Equal("boolean", build.GetProperty("properties").GetProperty("clean").GetProperty("type").GetString());
    }
}
