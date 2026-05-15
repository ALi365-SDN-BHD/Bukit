using System.Text.Json;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionBlockRendererRegistryTests
{
    [Fact]
    public void CreateDefault_RegistersAllBlockTypes()
    {
        var registry = NotionBlockRendererRegistry.CreateDefault();

        Assert.NotNull(registry);
    }

    [Fact]
    public async Task RenderBlockAsync_UnknownBlockType_ReturnsNull()
    {
        var registry = new NotionBlockRendererRegistry();

        using var doc = JsonDocument.Parse("""
        { "type": "unknown_block_type" }
        """);

        var result = await registry.RenderBlockAsync(
            "unknown_block_type",
            doc.RootElement,
            null!,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterCustomTransformer_OverridesBuiltIn()
    {
        var registry = NotionBlockRendererRegistry.CreateDefault();
        registry.SetCustomTransformer("paragraph", (_, _, _) =>
            Task.FromResult<string?>("<custom-paragraph>overridden</custom-paragraph>"));

        using var doc = JsonDocument.Parse("""
        {
          "type": "paragraph",
          "paragraph": { "rich_text": [{ "plain_text": "hello" }] }
        }
        """);

        var result = await registry.RenderBlockAsync("paragraph", doc.RootElement, null!, CancellationToken.None);

        Assert.Equal("<custom-paragraph>overridden</custom-paragraph>", result);
    }

    [Fact]
    public async Task SetCustomTransformer_ReturnsNull_FallsBackToBuiltIn()
    {
        var registry = NotionBlockRendererRegistry.CreateDefault();
        registry.SetCustomTransformer("paragraph", (_, _, _) =>
            Task.FromResult<string?>(null));

        using var doc = JsonDocument.Parse("""
        {
          "type": "paragraph",
          "paragraph": { "rich_text": [{ "plain_text": "hello" }] }
        }
        """);

        var result = await registry.RenderBlockAsync("paragraph", doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("<p>", result);
        Assert.Contains("hello", result);
    }

    [Fact]
    public async Task RemoveCustomTransformer_RestoresBuiltIn()
    {
        var registry = NotionBlockRendererRegistry.CreateDefault();
        registry.SetCustomTransformer("paragraph", (_, _, _) =>
            Task.FromResult<string?>("<custom>overridden</custom>"));
        registry.RemoveCustomTransformer("paragraph");

        using var doc = JsonDocument.Parse("""
        {
          "type": "paragraph",
          "paragraph": { "rich_text": [{ "plain_text": "original" }] }
        }
        """);

        var result = await registry.RenderBlockAsync("paragraph", doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("<p>", result);
        Assert.Contains("original", result);
        Assert.DoesNotContain("<custom>", result);
    }

    [Fact]
    public async Task DuplicateRegistration_Overwrites()
    {
        var registry = new NotionBlockRendererRegistry();
        var first = new TestRenderer("first");
        var second = new TestRenderer("second");

        registry.Register("paragraph", first);
        registry.Register("paragraph", second);

        var result = await registry.RenderBlockAsync(
            "paragraph",
            default,
            null!,
            CancellationToken.None);

        Assert.Equal("second", result);
    }

    [Fact]
    public async Task SetCustomTransformer_NewTransformer_ReplacesPrevious()
    {
        var registry = NotionBlockRendererRegistry.CreateDefault();
        registry.SetCustomTransformer("paragraph", (_, _, _) =>
            Task.FromResult<string?>("<v1>first</v1>"));
        registry.SetCustomTransformer("paragraph", (_, _, _) =>
            Task.FromResult<string?>("<v2>second</v2>"));

        using var doc = JsonDocument.Parse("""
        {
          "type": "paragraph",
          "paragraph": { "rich_text": [{ "plain_text": "test" }] }
        }
        """);

        var result = await registry.RenderBlockAsync("paragraph", doc.RootElement, null!, CancellationToken.None);

        Assert.Equal("<v2>second</v2>", result);
    }

    [Fact]
    public async Task RemoveCustomTransformer_WhenNoneRegistered_NoEffect()
    {
        var registry = NotionBlockRendererRegistry.CreateDefault();
        registry.RemoveCustomTransformer("nonexistent");

        using var doc = JsonDocument.Parse("""
        {
          "type": "paragraph",
          "paragraph": { "rich_text": [{ "plain_text": "test" }] }
        }
        """);

        var result = await registry.RenderBlockAsync("paragraph", doc.RootElement, null!, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("<p>", result);
    }

    private sealed class TestRenderer : INotionBlockRenderer
    {
        private readonly string _value;

        public TestRenderer(string value)
        {
            _value = value;
        }

        public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(_value);
        }
    }
}
