using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering.Scriban;
using Bukit.Shared;
using Bukit.Theme;
using Scriban.Runtime;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class SharedModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bukit-shared-" + Guid.NewGuid().ToString("N"));

    public SharedModelTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => TestCleanup.DeleteDirectory(_dir, recursive: true);

    private static PageModel Model() => new()
    {
        Site = new SiteModel
        {
            Name = "test", Title = "Site", BaseUrl = "/", Language = "en",
            Data = new Dictionary<string, object>
            {
                ["nested"] = new Dictionary<string, object> { ["original"] = "safe" },
                ["values"] = new[] { "safe" }
            }
        },
        Page = new PageInfo { Title = "Page", Url = "/page/", Content = "body" },
        Pages = new[] { new PageInfo { Title = "Original", Url = "/original/", Content = "" } }
    };

    [Theory]
    [InlineData("site.injected = 1")]
    [InlineData("pages[0].injected = 1")]
    [InlineData("site.data.nested.injected = 1")]
    [InlineData("pages[0] = null")]
    [InlineData("site.data.values[0] = 'changed'")]
    public void SharedWrites_RejectNewKeysAndArrayAssignments_WithoutLeaking(string assignment)
    {
        File.WriteAllText(Path.Combine(_dir, "write.html"), "{{ " + assignment + " }}");
        File.WriteAllText(Path.Combine(_dir, "read.html"),
            "{{ site.injected }}|{{ pages[0].injected }}|{{ site.data.nested.injected }}|{{ pages[0].title }}|{{ site.data.values[0] }}");
        var renderer = new ScribanTemplateRenderer(_dir);
        var model = Model();
        var error = Assert.Throws<RenderException>(() => renderer.RenderPage("write.html", model));
        Assert.Contains("readonly", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("|||Original|safe", renderer.RenderPage("read.html", model));
    }

    // Components and sections render in their own template context. Assigning an element of a
    // shared array there must fail too, or one page could change the array for every later page.
    [Theory]
    [InlineData("theme-component")]
    [InlineData("config-component")]
    [InlineData("section")]
    public void SharedArrayAssignment_InComponentOrSection_FailsWithoutLeaking(string kind)
    {
        var layouts = Path.Combine(_dir, "layouts");
        Directory.CreateDirectory(Path.Combine(layouts, "parts"));
        File.WriteAllText(Path.Combine(layouts, "parts", "write.html"), "{{ site.data.values[0] = 'leaked' }}");
        File.WriteAllText(Path.Combine(layouts, "write.html"), kind == "section"
            ? "{{ render_section '[{\"type\":\"writer\"}]' }}"
            : "{{ comp.render 'writer' }}");
        File.WriteAllText(Path.Combine(layouts, "read.html"), "{{ site.data.values[0] }}");
        var manifest = new ThemeManifestV2
        {
            Name = "test",
            Version = "1.0.0",
            Components = new() { ["writer"] = new() { Template = "parts/write.html" } },
            Sections = new() { ["writer"] = new() { Template = "parts/write.html" } }
        };
        var renderer = new ScribanTemplateRenderer(
            layouts,
            parentLayoutsDir: null,
            shortcodes: null,
            components: kind == "config-component"
                ? new Dictionary<string, Bukit.Config.ComponentDefinition> { ["writer"] = new() { Template = "parts/write.html" } }
                : null,
            userLayoutsDir: null,
            themeRegistry: kind == "config-component" ? null : new ThemeComponentRegistry(_dir, manifest, null),
            schemaValidator: null,
            dataResolver: null,
            componentValidation: "off");
        var model = Model();

        string? written = null;
        try
        {
            written = renderer.RenderPage("write.html", model);
        }
        catch (RenderException error)
        {
            Assert.True(error.Message.Contains("readonly", StringComparison.OrdinalIgnoreCase), error.Message);
        }

        if (written is not null)
        {
            Assert.Contains("readonly", written, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal("safe", renderer.RenderPage("read.html", model));
    }

    [Fact]
    public void ExistingMemberAssignment_StillFails()
    {
        File.WriteAllText(Path.Combine(_dir, "write.html"), "{{ pages[0].title = 'changed' }}");
        File.WriteAllText(Path.Combine(_dir, "read.html"), "{{ pages[0].title }}");
        var renderer = new ScribanTemplateRenderer(_dir);
        var model = Model();
        Assert.Throws<RenderException>(() => renderer.RenderPage("write.html", model));
        Assert.Equal("Original", renderer.RenderPage("read.html", model));
    }

    [Fact]
    public void SharedCache_ReusesConvertedValuesForTheSameInstances()
    {
        var cache = new SharedModelCache();
        var first = Model();
        var second = first with { Page = new PageInfo { Title = "Other", Url = "/other/", Content = "" } };

        var a = ScribanRootModelMapper.ToScriptObject(first, cache);
        var b = ScribanRootModelMapper.ToScriptObject(second, cache);

        Assert.Same(a["pages"], b["pages"]);
        Assert.Same(a["site"], b["site"]);
        Assert.NotSame(a["page"], b["page"]);
        Assert.True(Assert.IsType<ScriptArray>(a["pages"]).IsReadOnly);
        Assert.True(Assert.IsType<ScriptObject>(a["site"]).IsReadOnly);
    }

    [Fact]
    public void SharedCache_ConvertsAgainForDifferentInstances_EvenWhenValuesAreEqual()
    {
        var cache = new SharedModelCache();
        var first = Model();
        var copiedSite = first.Site with { };
        var copiedPages = first.Pages.ToArray();
        Assert.Equal(first.Site, copiedSite);

        var a = ScribanRootModelMapper.ToScriptObject(first, cache);
        var b = ScribanRootModelMapper.ToScriptObject(first with { Site = copiedSite, Pages = copiedPages }, cache);

        Assert.NotSame(a["site"], b["site"]);
        Assert.NotSame(a["pages"], b["pages"]);
    }

    [Fact]
    public void CompleteRenderPass_ReleasesSharedValues()
    {
        File.WriteAllText(Path.Combine(_dir, "read.html"), "{{ site.title }}");
        var renderer = new ScribanTemplateRenderer(_dir);
        var model = Model();
        renderer.RenderPage("read.html", model);
        Assert.False(renderer.SharedModels.IsEmpty);

        Assert.True(renderer.CompleteRenderPass() > 0);
        Assert.True(renderer.SharedModels.IsEmpty);
        Assert.Equal(0, renderer.CompleteRenderPass());
    }

    [Fact]
    public void SharedValues_AreNestedReadOnly()
    {
        var cache = new SharedModelCache();
        var root = ScribanRootModelMapper.ToScriptObject(Model(), cache);
        var site = Assert.IsType<ScriptObject>(root["site"]);
        var data = Assert.IsType<ScriptObject>(site["data"]);
        Assert.True(Assert.IsType<ScriptObject>(data["nested"]).IsReadOnly);
        Assert.True(Assert.IsType<ScriptArray>(data["values"]).IsReadOnly);
        Assert.True(Assert.IsType<ScriptObject>(Assert.IsType<ScriptArray>(root["pages"])[0]).IsReadOnly);
    }

    [Fact]
    public void ParallelRenders_MatchSequentialRenders()
    {
        File.WriteAllText(Path.Combine(_dir, "read.html"),
            "{{ page.title }}|{{ pages.size }}|{{ pages.unknown }}|{{ pages[0].title }}|{{ site.data.nested.original }}|{{ for p in pages }}{{ p.url }}{{ end }}");
        var shared = Model();
        var models = Enumerable.Range(0, 200)
            .Select(i => shared with { Page = new PageInfo { Title = "Page " + i, Url = $"/p{i}/", Content = "" } })
            .ToArray();

        var sequential = models.Select(m => new ScribanTemplateRenderer(_dir).RenderPage("read.html", m)).ToArray();
        var renderer = new ScribanTemplateRenderer(_dir);
        var parallel = new string[models.Length];
        Parallel.For(0, models.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            i => parallel[i] = renderer.RenderPage("read.html", models[i]));

        Assert.Equal(sequential, parallel);
    }

    [Fact]
    public void ListPage_SameInstanceForPagesAndItems_SharesOneReadOnlyArray()
    {
        var list = new[] { new PageInfo { Title = "A", Url = "/a/", Content = "" } };
        var site = Model().Site;

        var sameInstance = ScribanRootModelMapper.ToScriptObject(new ListPageModel { Site = site, Pages = list, Items = list });
        Assert.Same(sameInstance["pages"], sameInstance["items"]);
        Assert.True(Assert.IsType<ScriptArray>(sameInstance["items"]).IsReadOnly);

        var itemsAbsent = ScribanRootModelMapper.ToScriptObject(new ListPageModel { Site = site, Pages = list });
        Assert.Same(itemsAbsent["pages"], itemsAbsent["items"]);

        var different = ScribanRootModelMapper.ToScriptObject(new ListPageModel { Site = site, Pages = list, Items = list.ToArray() });
        Assert.NotSame(different["pages"], different["items"]);
    }

    [Fact]
    public void ArrayFunctions_OnSharedArrays_ReturnCopiesAndLeaveSharedValuesUnchanged()
    {
        File.WriteAllText(Path.Combine(_dir, "funcs.html"),
            "{{ a = pages | array.add 'n' }}{{ b = site.data.values | array.insert_at 0 'x' }}{{ c = site.data.values | array.remove_at 0 }}" +
            "{{ d = pages | array.reverse }}{{ e = site.data.values | array.sort }}{{ f = site.data.values | array.uniq }}" +
            "{{ a.size }}|{{ b.size }}|{{ c.size }}|{{ pages.size }}|{{ site.data.values.size }}");
        var renderer = new ScribanTemplateRenderer(_dir);
        var model = Model();

        Assert.Equal("2|2|0|1|1", renderer.RenderPage("funcs.html", model));
        Assert.Equal("2|2|0|1|1", renderer.RenderPage("funcs.html", model));
    }
}
