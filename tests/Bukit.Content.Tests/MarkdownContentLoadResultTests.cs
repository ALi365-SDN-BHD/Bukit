using Bukit.Content.Markdown;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class MarkdownContentLoadResultTests
{
    [Fact]
    public async Task LoadAsync_ReturnsMetadataFirstItemsAndHydratesBodyOnDemand()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "hello.md");

        await File.WriteAllTextAsync(file, """
                                        ---
                                        title: Hello
                                        slug: hello
                                        type: post
                                        ---
                                        # Hi

                                        Body text
                                        """);

        var provider = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(root));

        var result = await provider.LoadAsync();

        var item = Assert.Single(result.Items);
        Assert.Null(item.ContentHtml);
        Assert.False(string.IsNullOrWhiteSpace(item.BodyKey));

        var body = await result.BodyStore.GetAsync(item);
        Assert.Contains("<h1 id=\"hi\">Hi</h1>", body.Html);
        Assert.Contains("Body text", body.Html);
    }
}
