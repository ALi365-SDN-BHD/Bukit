using System.Text.Json;
using Bukit.Cli.Commands;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DataCommandTests : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly StringWriter _stdout;

    public DataCommandTests()
    {
        _originalOut = Console.Out;
        _stdout = new StringWriter();
        Console.SetOut(_stdout);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        _stdout.Dispose();
    }

    [Fact]
    public void PrintModuleSummary_UsesStructuredTypeAndLanguage()
    {
        var document = ContentDocument.Create(
            "module-1",
            "Hero Block",
            "hero-block",
            DateTimeOffset.UtcNow,
            null,
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = new("text", "data"),
                ["sourceKey"] = new("text", "homepage"),
                ["type"] = new("text", "hero"),
                ["language"] = new("text", "ms-MY")
            });

        DataCommand.PrintModuleSummary(new[] { document });

        var output = _stdout.ToString();
        Assert.Contains("hero", output, StringComparison.Ordinal);
        Assert.Contains("homepage", output, StringComparison.Ordinal);
        Assert.Contains("ms-MY", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DumpModulesJson_UsesStructuredTypeAsModuleKey()
    {
        var document = ContentDocument.Create(
            "module-1",
            "Hero Block",
            "hero-block",
            DateTimeOffset.UtcNow,
            null,
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = new("text", "data"),
                ["type"] = new("text", "hero"),
                ["headline"] = new("text", "Welcome")
            });

        var json = DataCommand.DumpModulesJson(new[] { document });
        using var doc = JsonDocument.Parse(json);

        var hero = doc.RootElement.GetProperty("modules").GetProperty("hero");
        Assert.Equal("module-1", hero[0].GetProperty("id").GetString());
        Assert.Equal("Welcome", hero[0].GetProperty("fields").GetProperty("headline").GetString());
    }
}
