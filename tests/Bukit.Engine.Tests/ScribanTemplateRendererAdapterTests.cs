using Bukit.Engine;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ScribanTemplateRendererAdapterTests
{
    private static SiteModel CreateSite() => new()
    {
        Name = "test",
        Title = "Test Site",
        BaseUrl = "/",
        Language = "en"
    };

    private static PageInfo CreatePage(string title, string url, string content = "") => new()
    {
        Title = title,
        Url = url,
        Content = content
    };

    [Fact]
    public void Constructor_WithValidLayoutsDir_CreatesInstance()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_layouts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var adapter = new ScribanTemplateRendererAdapter(tempDir);
            Assert.NotNull(adapter);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Constructor_WithNonExistentDir_DoesNotThrow()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), "bukit_nonexistent_" + Guid.NewGuid().ToString("N"));

        var adapter = new ScribanTemplateRendererAdapter(nonExistent);
        Assert.NotNull(adapter);
    }

    [Fact]
    public void RenderPage_WithValidTemplate_RendersOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_layouts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "post.scriban"), "Title: {{ page.title }}");

            var adapter = new ScribanTemplateRendererAdapter(tempDir);
            var pageModel = new PageModel
            {
                Site = CreateSite(),
                Page = CreatePage("Hello World", "/posts/hello", "<p>Content</p>")
            };

            var output = adapter.RenderPage("post.scriban", pageModel);

            Assert.NotNull(output);
            Assert.Contains("Hello World", output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void RenderPage_WithNonExistentTemplate_Throws()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_layouts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var adapter = new ScribanTemplateRendererAdapter(tempDir);
            var pageModel = new PageModel
            {
                Site = CreateSite(),
                Page = CreatePage("Test", "/test", "<p>Test</p>")
            };

            Assert.ThrowsAny<Exception>(() => adapter.RenderPage("nonexistent.scriban", pageModel));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void RenderList_WithValidTemplate_RendersOutput()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_layouts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "list.scriban"), "Title: {{ page.title }} | Count: {{ pages | array.size }}");

            var adapter = new ScribanTemplateRendererAdapter(tempDir);
            var listModel = new ListPageModel
            {
                Site = CreateSite(),
                Page = CreatePage("Posts", "/posts"),
                Pages = new List<PageInfo>
                {
                    CreatePage("Post 1", "/posts/1"),
                    CreatePage("Post 2", "/posts/2")
                }
            };

            var output = adapter.RenderList("list.scriban", listModel);

            Assert.NotNull(output);
            Assert.Contains("Posts", output, StringComparison.Ordinal);
            Assert.Contains("2", output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void RenderList_WithEmptyList_RendersWithoutError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_layouts_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "list.scriban"), "Title: {{ page.title }} ({{ pages | array.size }} items)");

            var adapter = new ScribanTemplateRendererAdapter(tempDir);
            var listModel = new ListPageModel
            {
                Site = CreateSite(),
                Page = CreatePage("Empty List", "/list"),
                Pages = Array.Empty<PageInfo>()
            };

            var output = adapter.RenderList("list.scriban", listModel);

            Assert.NotNull(output);
            Assert.Contains("Empty List", output, StringComparison.Ordinal);
            Assert.Contains("0 items", output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
