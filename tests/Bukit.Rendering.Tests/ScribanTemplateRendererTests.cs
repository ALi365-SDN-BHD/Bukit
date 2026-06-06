using Bukit.Config;
using Bukit.Shared;
using Bukit.Theme;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class ScribanTemplateRendererTests : IDisposable
{
    private readonly string _layoutsDir;

    public ScribanTemplateRendererTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-scriban-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_layoutsDir, recursive: true);
    }

    private static SiteModel CreateSite()
    {
        return new SiteModel
        {
            Name = "test",
            Title = "Test Site",
            Url = "https://example.com",
            BaseUrl = "/",
            Language = "en"
        };
    }

    [Fact]
    public void Constructor_CreatesRenderer()
    {
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        Assert.NotNull(renderer);
    }

    [Fact]
    public void RenderPage_Throws_WhenTemplateNotFound()
    {
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo
            {
                Title = "Test",
                Content = "<p>hi</p>",
                Url = "/test/"
            }
        };

        Assert.Throws<Bukit.Shared.RenderException>(() =>
            renderer.RenderPage("missing.html", model));
    }

    [Fact]
    public void RenderPage_RendersSimpleTemplate()
    {
        var templatePath = Path.Combine(_layoutsDir, "page.html");
        File.WriteAllText(templatePath, "<html><head><title>{{ page.title }}</title></head><body>{{ page.content }}</body></html>");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var model = new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo
            {
                Title = "Hello",
                Content = "<p>World</p>",
                Url = "/hello/"
            }
        };

        var result = renderer.RenderPage("page.html", model);
        Assert.Contains("<title>Hello</title>", result);
        Assert.Contains("<p>World</p>", result);
    }

    [Fact]
    public void RenderList_RendersListTemplate()
    {
        var templatePath = Path.Combine(_layoutsDir, "list.html");
        File.WriteAllText(templatePath, "{{ for page in pages }}{{ page.title }}|{{ end }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var model = new ListPageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "List", Url = "/list/", Content = "" },
            Pages = new[]
            {
                new PageInfo { Title = "A", Url = "/a/", Content = "" },
                new PageInfo { Title = "B", Url = "/b/", Content = "" }
            }
        };

        var result = renderer.RenderList("list.html", model);
        Assert.Contains("A|", result);
        Assert.Contains("B|", result);
    }

    [Fact]
    public async Task RenderPage_WithComponents_UsesRendererLocalStateUnderParallelRendering()
    {
        var leftLayouts = Path.Combine(_layoutsDir, "left");
        var rightLayouts = Path.Combine(_layoutsDir, "right");
        Directory.CreateDirectory(leftLayouts);
        Directory.CreateDirectory(rightLayouts);

        File.WriteAllText(Path.Combine(leftLayouts, "page.html"), """
            {{ for i in 1..20 }}{{ comp.render 'badge' '' '' '' }};{{ end }}
            """);
        File.WriteAllText(Path.Combine(leftLayouts, "badge.html"), "left:{{ page.title }}");

        File.WriteAllText(Path.Combine(rightLayouts, "page.html"), """
            {{ for i in 1..20 }}{{ comp.render 'badge' '' '' '' }};{{ end }}
            """);
        File.WriteAllText(Path.Combine(rightLayouts, "badge.html"), "right:{{ page.title }}");

        var components = new Dictionary<string, ComponentDefinition>
        {
            ["badge"] = new() { Template = "badge.html" }
        };

        var leftRenderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(leftLayouts, null, null, components, null);
        var rightRenderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(rightLayouts, null, null, components, null);
        var site = CreateSite();

        var tasks = Enumerable.Range(0, 250).Select(i => Task.Run(() =>
        {
            var left = leftRenderer.RenderPage("page.html", new PageModel
            {
                Site = site,
                Page = new PageInfo { Title = $"L{i}", Content = "", Url = $"/l{i}/" }
            });
            var right = rightRenderer.RenderPage("page.html", new PageModel
            {
                Site = site,
                Page = new PageInfo { Title = $"R{i}", Content = "", Url = $"/r{i}/" }
            });
            return (i, left, right);
        }));

        var results = await Task.WhenAll(tasks);

        foreach (var (i, left, right) in results)
        {
            Assert.DoesNotContain("right:", left);
            Assert.DoesNotContain("R" + i, left);
            Assert.DoesNotContain("left:", right);
            Assert.DoesNotContain("L" + i, right);
            Assert.Equal(20, CountOccurrences(left, $"left:L{i}"));
            Assert.Equal(20, CountOccurrences(right, $"right:R{i}"));
        }
    }

    [Fact]
    public void RenderPage_ImageHelper_RejectsUnsafeImageSource()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"),
            "{{ image.img 'javascript:alert(1)' 'bad' '480' 'hero' }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Image", Content = "", Url = "/image/" }
        });

        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderPage_ImageHelper_EscapesAttributeValues()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"),
            "{{ image.img '/img/photo.jpg' 'A \"quoted\" alt' '480' 'hero \"bad\"' }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Image", Content = "", Url = "/image/" }
        });

        Assert.Contains("src=\"/img/photo.jpg\"", result);
        Assert.Contains("alt=\"A &quot;quoted&quot; alt\"", result);
        Assert.Contains("class=\"hero &quot;bad&quot;\"", result);
        Assert.DoesNotContain("alt=\"A \"quoted\" alt\"", result);
    }

    [Fact]
    public void RenderPage_ThemeComponentMissing_StrictThrows()
    {
        var themeDir = Path.Combine(_layoutsDir, "theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'missing' {} }}");

        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Components = new()
            {
                ["existing"] = new() { Template = "existing.html" }
            }
        };
        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "strict");

        var ex = Assert.Throws<RenderException>(() => renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Component", Content = "", Url = "/component/" }
        }));

        Assert.Contains("theme.component.not_found", ex.Message);
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void RenderPage_WithSection_RendersCorrectly()
    {
        var themeDir = Path.Combine(_layoutsDir, "section-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        var sectionDir = Path.Combine(layoutsDir, "sections", "hero");
        var pagesDir = Path.Combine(layoutsDir, "pages");
        Directory.CreateDirectory(sectionDir);
        Directory.CreateDirectory(pagesDir);

        File.WriteAllText(Path.Combine(sectionDir, "hero.html"),
            "<div class=\"hero\">{{ section.props.title }}</div>");

        var manifest = new ThemeManifestV2
        {
            Name = "section-theme",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero/hero.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(pagesDir, "page.html"),
            "{{ render_section '[{\"type\":\"hero\",\"props\":{\"title\":\"Hello World\"}}]' }}");

        var result = renderer.RenderPage("pages/page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("Hello World", result);
        Assert.Contains("hero", result);
    }

    [Fact]
    public void RenderPage_WithSection_TemplateModifiedBetweenRenders_SeesUpdatedContent()
    {
        var themeDir = Path.Combine(_layoutsDir, "inval-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        var sectionDir = Path.Combine(layoutsDir, "sections", "hero");
        var pagesDir = Path.Combine(layoutsDir, "pages");
        Directory.CreateDirectory(sectionDir);
        Directory.CreateDirectory(pagesDir);

        var templatePath = Path.Combine(sectionDir, "hero.html");
        File.WriteAllText(templatePath, "<h1>{{ section.props.title }}</h1>");

        var manifest = new ThemeManifestV2
        {
            Name = "inval-theme",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero/hero.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(pagesDir, "page1.html"),
            "{{ render_section '[{\"type\":\"hero\",\"props\":{\"title\":\"V1\"}}]' }}");
        File.WriteAllText(Path.Combine(pagesDir, "page2.html"),
            "{{ render_section '[{\"type\":\"hero\",\"props\":{\"title\":\"V2\"}}]' }}");

        var first = renderer.RenderPage("pages/page1.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "First", Content = "", Url = "/first/" }
        });

        Assert.Contains("<h1>V1</h1>", first);

        File.WriteAllText(templatePath, "<h2>{{ section.props.title }}</h2>");

        var second = renderer.RenderPage("pages/page2.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Second", Content = "", Url = "/second/" }
        });

        Assert.Contains("<h2>V2</h2>", second);
        Assert.DoesNotContain("<h1>V2</h1>", second);
    }

    [Fact]
    public async Task RenderPage_WithSection_MultiplePagesInParallel_NoCrossContamination()
    {
        var themeDir = Path.Combine(_layoutsDir, "parallel-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        var sectionDir = Path.Combine(layoutsDir, "sections", "hero");
        var pagesDir = Path.Combine(layoutsDir, "pages");
        Directory.CreateDirectory(sectionDir);
        Directory.CreateDirectory(pagesDir);

        File.WriteAllText(Path.Combine(sectionDir, "hero.html"),
            "<div class=\"hero\">{{ section.props.title }}</div>");

        var manifest = new ThemeManifestV2
        {
            Name = "parallel-theme",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero/hero.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(pagesDir, "page.html"),
            "{{ render_section '[{\"type\":\"hero\",\"props\":{\"title\":\"Shared\"}}]' }}");

        var site = CreateSite();

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            var result = renderer.RenderPage("pages/page.html", new PageModel
            {
                Site = site,
                Page = new PageInfo { Title = "P", Content = "", Url = "/p/" }
            });

            Assert.Contains("Shared", result);
            Assert.DoesNotContain("error", result, StringComparison.OrdinalIgnoreCase);
            return result;
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void RenderPage_WithThemeComponent_RendersCorrectly()
    {
        var themeDir = Path.Combine(_layoutsDir, "comp-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        var componentsDir = Path.Combine(layoutsDir, "components");
        Directory.CreateDirectory(componentsDir);

        File.WriteAllText(Path.Combine(componentsDir, "badge.html"),
            "<span class=\"badge\">{{ data.text }}</span>");

        var manifest = new ThemeManifestV2
        {
            Name = "comp-theme",
            Version = "1.0.0",
            Components = new()
            {
                ["badge"] = new() { Template = "components/badge.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'badge' {} }}");

        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("badge", result);
    }

    [Fact]
    public void RenderPage_WithThemeComponent_TemplateModifiedBetweenRenders_SeesUpdatedContent()
    {
        var themeDir = Path.Combine(_layoutsDir, "comp-inval-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        var componentsDir = Path.Combine(layoutsDir, "components");
        Directory.CreateDirectory(componentsDir);

        var templatePath = Path.Combine(componentsDir, "alert.html");
        File.WriteAllText(templatePath, "<strong>{{ data.msg }}</strong>");

        var manifest = new ThemeManifestV2
        {
            Name = "comp-inval-theme",
            Version = "1.0.0",
            Components = new()
            {
                ["alert"] = new() { Template = "components/alert.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page1.html"),
            "{{ comp.render 'alert' {} }}");
        File.WriteAllText(Path.Combine(layoutsDir, "page2.html"),
            "{{ comp.render 'alert' {} }}");

        var first = renderer.RenderPage("page1.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "First", Content = "", Url = "/first/" }
        });

        Assert.Contains("<strong>", first);

        File.WriteAllText(templatePath, "<em>{{ data.msg }}</em>");

        var second = renderer.RenderPage("page2.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Second", Content = "", Url = "/second/" }
        });

        Assert.Contains("<em>", second);
        Assert.DoesNotContain("<strong>", second);
    }

    [Fact]
    public async Task RenderPage_WithThemeComponent_MultiplePagesInParallel_NoCrossContamination()
    {
        var themeDir = Path.Combine(_layoutsDir, "comp-parallel-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        var componentsDir = Path.Combine(layoutsDir, "components");
        Directory.CreateDirectory(componentsDir);

        File.WriteAllText(Path.Combine(componentsDir, "card.html"),
            "<div class=\"card\">{{ data.title }}</div>");

        var manifest = new ThemeManifestV2
        {
            Name = "comp-parallel-theme",
            Version = "1.0.0",
            Components = new()
            {
                ["card"] = new() { Template = "components/card.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'card' {} }}");

        var site = CreateSite();

        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            var result = renderer.RenderPage("page.html", new PageModel
            {
                Site = site,
                Page = new PageInfo { Title = "P", Content = "", Url = "/p/" }
            });

            Assert.Contains("card", result);
            Assert.DoesNotContain("error", result, StringComparison.OrdinalIgnoreCase);
            return result;
        }));

        await Task.WhenAll(tasks);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    [Fact]
    public void RenderPage_ImageSrcset_RendersSrcset()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"),
            "{{ image.srcset '/img/photo.jpg' '480,768' }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Srcset", Content = "", Url = "/srcset/" }
        });

        Assert.Contains("?w=480 480w", result);
        Assert.Contains("?w=768 768w", result);
    }

    [Fact]
    public void RenderPage_ImageSrcset_DefaultSizes()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "page.html"),
            "{{ image.srcset '/img/photo.jpg' }}");

        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(_layoutsDir);
        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Srcset", Content = "", Url = "/srcset/" }
        });

        Assert.Contains("1200w", result);
    }

    [Fact]
    public void RenderPage_ThemeComponentTemplateInvalidPath_RendersError()
    {
        var themeDir = Path.Combine(_layoutsDir, "comp-invalid-path-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);

        var manifest = new ThemeManifestV2
        {
            Name = "comp-invalid-path-theme",
            Version = "1.0.0",
            Components = new()
            {
                ["bad"] = new() { Template = "" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'bad' {} }}");

        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("code=theme.component.template_invalid", result);
    }

    [Fact]
    public void RenderPage_ThemeComponentTemplateNotFound_RendersError()
    {
        var themeDir = Path.Combine(_layoutsDir, "comp-notfound-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);

        var manifest = new ThemeManifestV2
        {
            Name = "comp-notfound-theme",
            Version = "1.0.0",
            Components = new()
            {
                ["missing"] = new() { Template = "components/missing.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'missing' {} }}");

        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("code=theme.component.template_not_found", result);
    }

    [Fact]
    public void RenderPage_ThemeComponentEscapesRoot_RendersError()
    {
        var themeDir = Path.Combine(_layoutsDir, "comp-escape-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);

        var manifest = new ThemeManifestV2
        {
            Name = "comp-escape-theme",
            Version = "1.0.0",
            Components = new()
            {
                ["escape"] = new() { Template = "/etc/passwd" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'escape' {} }}");

        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("code=theme.component.template_invalid", result);
    }

    [Fact]
    public void RenderPage_ThemeComponentParseError_RendersError()
    {
        var themeDir = Path.Combine(_layoutsDir, "comp-parse-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        var componentsDir = Path.Combine(layoutsDir, "components");
        Directory.CreateDirectory(componentsDir);

        File.WriteAllText(Path.Combine(componentsDir, "broken.html"),
            "Hello {{ broken }");

        var manifest = new ThemeManifestV2
        {
            Name = "comp-parse-theme",
            Version = "1.0.0",
            Components = new()
            {
                ["broken"] = new() { Template = "components/broken.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'broken' {} }}");

        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("code=theme.component.template_parse_failed", result);
    }

    [Fact]
    public void RenderPage_ThemeComponentStrictMode_Throws()
    {
        var themeDir = Path.Combine(_layoutsDir, "comp-strict-theme");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);

        var manifest = new ThemeManifestV2
        {
            Name = "comp-strict-theme",
            Version = "1.0.0",
            Components = new()
            {
                ["alert"] = new() { Template = "components/missing.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "strict");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ comp.render 'alert' {} }}");

        var ex = Assert.Throws<RenderException>(() => renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        }));

        Assert.Contains("theme.component.template_not_found", ex.Message);
    }

    [Fact]
    public void RenderPage_SectionInvalidJson_RendersError()
    {
        var themeDir = Path.Combine(_layoutsDir, "section-invalid-json");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);

        var manifest = new ThemeManifestV2
        {
            Name = "section-invalid-json",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ render_section 'not-json' }}");

        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("code=theme.render_section.invalid_json", result);
    }

    [Fact]
    public void RenderPage_SectionMissingTemplate_RendersError()
    {
        var themeDir = Path.Combine(_layoutsDir, "section-missing-tmpl");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        var pagesDir = Path.Combine(layoutsDir, "pages");
        Directory.CreateDirectory(pagesDir);

        var manifest = new ThemeManifestV2
        {
            Name = "section-missing-tmpl",
            Version = "1.0.0",
            Sections = new()
            {
                ["nonexistent"] = new() { Template = "sections/nonexistent.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(pagesDir, "page.html"),
            "{{ render_section '[{\"type\":\"nonexistent\",\"props\":{\"title\":\"X\"}}]' }}");

        var result = renderer.RenderPage("pages/page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("template not found", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderPage_SectionWithoutType_RendersError()
    {
        var themeDir = Path.Combine(_layoutsDir, "section-no-type");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);

        var manifest = new ThemeManifestV2
        {
            Name = "section-no-type",
            Version = "1.0.0"
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ render_section '[{\"props\":{\"title\":\"X\"}}]' }}");

        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("code=theme.render_section.empty", result);
    }

    [Fact]
    public void RenderPage_SectionUndefinedInManifest_RendersError()
    {
        var themeDir = Path.Combine(_layoutsDir, "section-undefined");
        var layoutsDir = Path.Combine(themeDir, "layouts");
        Directory.CreateDirectory(layoutsDir);

        var manifest = new ThemeManifestV2
        {
            Name = "section-undefined",
            Version = "1.0.0",
            Sections = new()
            {
                ["hero"] = new() { Template = "sections/hero.html" }
            }
        };

        var registry = new ThemeComponentRegistry(themeDir, manifest, null);
        var renderer = new Bukit.Rendering.Scriban.ScribanTemplateRenderer(
            layoutsDir, null, null, null, null, registry, null, null, "off");

        File.WriteAllText(Path.Combine(layoutsDir, "page.html"),
            "{{ render_section '[{\"type\":\"unknown\",\"props\":{\"title\":\"X\"}}]' }}");

        var result = renderer.RenderPage("page.html", new PageModel
        {
            Site = CreateSite(),
            Page = new PageInfo { Title = "Test", Content = "", Url = "/test/" }
        });

        Assert.Contains("code=theme.section.not_found", result);
    }
}
