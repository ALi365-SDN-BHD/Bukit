using System.Reflection;
using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DevCommandTests
{
    private static readonly MethodInfo s_extractOptions = typeof(DevCommand)
        .GetMethod("ExtractOptions", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_createBuildOverrides = typeof(DevCommand)
        .GetMethod("CreateBuildOverrides", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void ExtractOptions_AllDefaults_ReturnsDefaults()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        var result = s_extractOptions.Invoke(null, new object[] { command })!;
        var type = result.GetType();

        Assert.Null(type.GetField("Item1")!.GetValue(result));
        Assert.Null(type.GetField("Item2")!.GetValue(result));
        Assert.Equal("localhost", type.GetField("Item3")!.GetValue(result));
        Assert.Equal(35729, type.GetField("Item4")!.GetValue(result));
        Assert.False((bool)type.GetField("Item5")!.GetValue(result)!);
        Assert.Null(type.GetField("Item6")!.GetValue(result));
    }

    [Fact]
    public void ExtractOptions_WithValues_ReturnsProvided()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = "my-site.yaml",
                ["--site"] = "blog",
                ["--host"] = "0.0.0.0",
                ["--port"] = "8080",
                ["--no-watch"] = "true",
                ["--output"] = "public"
            },
            Array.Empty<string>());

        var result = s_extractOptions.Invoke(null, new object[] { command })!;
        var type = result.GetType();

        Assert.Equal("my-site.yaml", type.GetField("Item1")!.GetValue(result));
        Assert.Equal("blog", type.GetField("Item2")!.GetValue(result));
        Assert.Equal("0.0.0.0", type.GetField("Item3")!.GetValue(result));
        Assert.Equal(8080, type.GetField("Item4")!.GetValue(result));
        Assert.True((bool)type.GetField("Item5")!.GetValue(result)!);
        Assert.Equal("public", type.GetField("Item6")!.GetValue(result));
    }

    [Fact]
    public void CreateBuildOverrides_CleanTrue_ReturnsCleanOverride()
    {
        var result = s_createBuildOverrides.Invoke(null, new object[] { true, null!, "/cache" })!;
        var type = result.GetType();

        Assert.True((bool)type.GetProperty("Clean")!.GetValue(result)!);
        Assert.True((bool)type.GetProperty("Incremental")!.GetValue(result)!);
        Assert.Equal("/cache", type.GetProperty("CacheDir")!.GetValue(result));
        Assert.Null(type.GetProperty("Output")!.GetValue(result));
    }

    [Fact]
    public void CreateBuildOverrides_WithOutputOverride_ReturnsOutput()
    {
        var result = s_createBuildOverrides.Invoke(null, new object[] { false, "custom-output", "/cache" })!;
        var type = result.GetType();

        Assert.Equal("custom-output", type.GetProperty("Output")!.GetValue(result));
        Assert.False((bool)type.GetProperty("Clean")!.GetValue(result)!);
    }
}
