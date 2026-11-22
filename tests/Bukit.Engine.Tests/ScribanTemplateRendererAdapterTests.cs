using Bukit.Engine;
using Bukit.Rendering;
using Bukit.Theme;
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

    // Both constructors hand the contributors to the renderer; the registry one used to drop them.
    // The contributor interface is internal to Bukit.Rendering, so the test builds one with a
    // DispatchProxy and reaches the constructors through reflection.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderPage_RunsTemplateContextContributors(bool withRegistry)
    {
        var themeDir = Path.Combine(Path.GetTempPath(), "bukit_test_contributors_" + Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        try
        {
            File.WriteAllText(Path.Combine(layoutsDir, "post.html"), "{{ contributed }}");
            var contributorType = typeof(PageModel).Assembly.GetType("Bukit.Rendering.Scriban.ITemplateContextContributor", throwOnError: true)!;
            var contributors = Array.CreateInstance(contributorType, 1);
            contributors.SetValue(System.Reflection.DispatchProxy.Create(contributorType, typeof(ValueContributor)), 0);
            var constructors = typeof(ScribanTemplateRendererAdapter).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var constructor = constructors.Single(candidate => candidate.GetParameters().Any(parameter => parameter.ParameterType == typeof(ThemeComponentRegistry)) == withRegistry);
            var arguments = constructor.GetParameters().Select(parameter => parameter.Name switch
            {
                "layoutsDir" => layoutsDir,
                "registry" => new ThemeComponentRegistry(themeDir, new ThemeManifestV2 { Name = "t", Version = "1.0.0" }, null),
                "componentValidation" => "off",
                "contributors" => contributors,
                _ => parameter.HasDefaultValue ? parameter.DefaultValue : null
            }).ToArray();
            var adapter = (ScribanTemplateRendererAdapter)constructor.Invoke(arguments);

            var output = adapter.RenderPage("post.html", new PageModel { Site = CreateSite(), Page = CreatePage("T", "/t/") });

            Assert.Equal("from-contributor", output.Trim());
        }
        finally
        {
            Directory.Delete(themeDir, true);
        }
    }

    public class ValueContributor : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            ((Scriban.Runtime.ScriptObject)args![1]!).SetValue("contributed", "from-contributor", readOnly: false);
            return null;
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
            File.WriteAllText(Path.Combine(tempDir, "post.scriban"), "title: {{ page.title }}");

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
            File.WriteAllText(Path.Combine(tempDir, "list.scriban"), "title: {{ page.title }} | Count: {{ pages | array.size }}");

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
            File.WriteAllText(Path.Combine(tempDir, "list.scriban"), "title: {{ page.title }} ({{ pages | array.size }} items)");

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
