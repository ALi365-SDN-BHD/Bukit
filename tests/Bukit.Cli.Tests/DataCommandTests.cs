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
        var item = new ContentItem(
            Id: "module-1",
            Title: "Hero Block",
            Slug: "hero-block",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = "data",
                ["sourceKey"] = "homepage"
            },
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "hero"),
                ["language"] = new("text", "ms-MY")
            });

        DataCommand.PrintModuleSummary(new[] { item });

        var output = _stdout.ToString();
        Assert.Contains("hero", output, StringComparison.Ordinal);
        Assert.Contains("homepage", output, StringComparison.Ordinal);
        Assert.Contains("ms-MY", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DumpModulesJson_UsesStructuredTypeAsModuleKey()
    {
        var item = new ContentItem(
            Id: "module-1",
            Title: "Hero Block",
            Slug: "hero-block",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceMode"] = "data"
            },
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "hero"),
                ["headline"] = new("text", "Welcome")
            });

        var json = DataCommand.DumpModulesJson(new[] { item });
        using var doc = JsonDocument.Parse(json);

        var hero = doc.RootElement.GetProperty("modules").GetProperty("hero");
        Assert.Equal("module-1", hero[0].GetProperty("id").GetString());
        Assert.Equal("Welcome", hero[0].GetProperty("fields").GetProperty("headline").GetString());
    }
}
