using Bukit.Config;
using Bukit.Rendering.Scriban;
using Bukit.Shared;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class RenderingCoverageTests
{
    // ── JsonStringFunction ────────────────────────────────────────────

    [Fact]
    public void JsonString_EmptyString_ReturnsQuotedEmpty()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { "" }, null!);
        Assert.Equal("\"\"", result);
    }

    [Fact]
    public void JsonString_PlainText_ReturnsQuoted()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { "hello world" }, null!);
        Assert.Equal("\"hello world\"", result);
    }

    [Fact]
    public void JsonString_DoubleQuotes_Escaped()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { """He said "hi" """ }, null!);
        Assert.Contains("\\\"hi\\\"", result!.ToString());
    }

    [Fact]
    public void JsonString_Backslash_Escaped()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { "path\\to\\file" }, null!);
        Assert.Equal("\"path\\\\to\\\\file\"", result);
    }

    [Fact]
    public void JsonString_Newlines_Escaped()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { "line1\nline2\rline3" }, null!);
        Assert.Equal("\"line1\\nline2\\rline3\"", result);
    }

    [Fact]
    public void JsonString_Tab_Escaped()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { "col1\tcol2" }, null!);
        Assert.Equal("\"col1\\tcol2\"", result);
    }

    [Fact]
    public void JsonString_BackslashF_Escaped()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { "a\fb" }, null!);
        Assert.Equal("\"a\\fb\"", result);
    }

    [Fact]
    public void JsonString_BackslashB_Escaped()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { "a\bb" }, null!);
        Assert.Equal("\"a\\bb\"", result);
    }

    [Fact]
    public void JsonString_ControlChar_UnicodeEscaped()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { "a\u0001b" }, null!);
        Assert.Equal("\"a\\u0001b\"", result);
    }

    [Fact]
    public void JsonString_NoArgs_ReturnsQuotedEmpty()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray(), null!);
        Assert.Equal("\"\"", result);
    }

    [Fact]
    public void JsonString_NullArg_ReturnsQuotedEmpty()
    {
        var fn = new JsonStringFunction();
        var result = fn.Invoke(null!, null!, new ScriptArray { null! }, null!);
        Assert.Equal("\"\"", result);
    }

    [Fact]
    public async Task JsonString_Async_Works()
    {
        var fn = new JsonStringFunction();
        var result = await fn.InvokeAsync(null!, null!, new ScriptArray { "test" }, null!);
        Assert.Equal("\"test\"", result);
    }

    [Fact]
    public void JsonString_Properties()
    {
        var fn = new JsonStringFunction();
        Assert.Equal(1, fn.RequiredParameterCount);
        Assert.Equal(1, fn.ParameterCount);
        Assert.Equal(ScriptVarParamKind.None, fn.VarParamKind);
        Assert.Equal(typeof(string), fn.ReturnType);
        Assert.Equal("value", fn.GetParameterInfo(0).Name);
    }

    // ── ComponentRenderFunction properties ────────────────────────────

    [Fact]
    public void ComponentRenderFunction_Properties()
    {
        var fn = new ComponentRenderFunction(
            new Dictionary<string, ComponentDefinition>(),
            null!,
            new ScriptObject(),
            "lenient");
        Assert.Equal(1, fn.RequiredParameterCount);
        Assert.Equal(4, fn.ParameterCount);
        Assert.Equal(ScriptVarParamKind.Direct, fn.VarParamKind);
        Assert.Equal(typeof(string), fn.ReturnType);
        Assert.Equal("name", fn.GetParameterInfo(0).Name);
        Assert.Equal("arg1", fn.GetParameterInfo(1).Name);
    }

    [Fact]
    public void ComponentRenderFunction_NotFound_Lenient_ReturnsComment()
    {
        var fn = new ComponentRenderFunction(
            new Dictionary<string, ComponentDefinition>(),
            null!,
            new ScriptObject(),
            "lenient");
        var result = fn.Invoke(new TemplateContext(), null, new ScriptArray { "missing" }, null);
        Assert.Contains("theme.component.not_found", result!.ToString());
        Assert.Contains("<!--", result.ToString());
    }

    [Fact]
    public void ComponentRenderFunction_NotFound_Strict_Throws()
    {
        var fn = new ComponentRenderFunction(
            new Dictionary<string, ComponentDefinition>(),
            null!,
            new ScriptObject(),
            "strict");
        Assert.Throws<RenderException>(() =>
            fn.Invoke(new TemplateContext(), null, new ScriptArray { "missing" }, null));
    }

    [Fact]
    public async Task ComponentRenderFunction_Async_Works()
    {
        var fn = new ComponentRenderFunction(
            new Dictionary<string, ComponentDefinition>(),
            null!,
            new ScriptObject(),
            "lenient");
        var result = await fn.InvokeAsync(new TemplateContext(), null, new ScriptArray { "missing" }, null);
        Assert.Contains("not_found", result!.ToString());
    }

    // ── ThemeComponentRenderFunction ──────────────────────────────────

    [Fact]
    public void ThemeComponentRenderFunction_Properties()
    {
        var fn = new ThemeComponentRenderFunction(
            new Dictionary<string, Theme.ThemeComponentDefinition>(),
            null!,
            new ScriptObject(),
            "/tmp",
            "lenient");
        Assert.Equal(1, fn.RequiredParameterCount);
        Assert.Equal(2, fn.ParameterCount);
        Assert.Equal(ScriptVarParamKind.None, fn.VarParamKind);
        Assert.Equal(typeof(string), fn.ReturnType);
    }

    [Fact]
    public void ThemeComponentRenderFunction_NotFound_Lenient()
    {
        var fn = new ThemeComponentRenderFunction(
            new Dictionary<string, Theme.ThemeComponentDefinition>(),
            null!,
            new ScriptObject(),
            "/tmp",
            "lenient");
        var result = fn.Render("missing", null);
        Assert.Contains("not_found", result);
    }

    [Fact]
    public void ThemeComponentRenderFunction_NotFound_Strict()
    {
        var fn = new ThemeComponentRenderFunction(
            new Dictionary<string, Theme.ThemeComponentDefinition>(),
            null!,
            new ScriptObject(),
            "/tmp",
            "strict");
        Assert.Throws<RenderException>(() => fn.Render("missing", null));
    }

    [Fact]
    public void ThemeComponentRenderFunction_InvalidTemplatePath()
    {
        var fn = new ThemeComponentRenderFunction(
            new Dictionary<string, Theme.ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["test"] = new() { Template = "" }
            },
            null!,
            new ScriptObject(),
            "/tmp",
            "lenient");
        var result = fn.Render("test", null);
        Assert.Contains("template_invalid", result);
    }

    [Fact]
    public void ThemeComponentRenderFunction_AbsoluteTemplatePath()
    {
        var fn = new ThemeComponentRenderFunction(
            new Dictionary<string, Theme.ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["test"] = new() { Template = "/etc/passwd" }
            },
            null!,
            new ScriptObject(),
            "/tmp",
            "lenient");
        var result = fn.Render("test", null);
        Assert.Contains("template_invalid", result);
    }

    [Fact]
    public void ThemeComponentRenderFunction_PathTraversal()
    {
        var fn = new ThemeComponentRenderFunction(
            new Dictionary<string, Theme.ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["evil"] = new() { Template = "../../etc/passwd" }
            },
            null!,
            new ScriptObject(),
            "/tmp/theme",
            "lenient");
        var result = fn.Render("evil", null);
        Assert.Contains("template_invalid", result);
    }

    [Fact]
    public void ThemeComponentRenderFunction_TemplateNotFound()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var fn = new ThemeComponentRenderFunction(
                new Dictionary<string, Theme.ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["test"] = new() { Template = "missing.html" }
                },
                null!,
                new ScriptObject(),
                dir,
                "lenient");
            var result = fn.Render("test", null);
            Assert.Contains("template_not_found", result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ThemeComponentRenderFunction_Async_Works()
    {
        var fn = new ThemeComponentRenderFunction(
            new Dictionary<string, Theme.ThemeComponentDefinition>(),
            null!,
            new ScriptObject(),
            "/tmp",
            "lenient");
        var result = await fn.InvokeAsync(new TemplateContext(), null, new ScriptArray { "missing" }, null);
        Assert.Contains("not_found", result!.ToString());
    }
}
