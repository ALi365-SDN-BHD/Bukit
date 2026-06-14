using Bukit.Rendering.Scriban;
using Bukit.Theme;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class RenderComponentFunctionTests : IDisposable
{
    private readonly string _themeDir;

    public RenderComponentFunctionTests()
    {
        _themeDir = Path.Combine(Path.GetTempPath(), "bukit-render-component-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_themeDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_themeDir, recursive: true);
    }

    [Fact]
    public void Invoke_NonScriptObject_ReturnsDiagnosticComment()
    {
        var function = CreateFunction(new Dictionary<string, ThemeComponentDefinition>());
        var arguments = new ScriptArray
        {
            "badge",
            "plain-text"
        };

        var result = function.Invoke(new TemplateContext(), callerContext: null, arguments, blockStatement: null);

        var text = Assert.IsType<string>(result);
        Assert.Contains("not ScriptObject", text);
        Assert.Contains(typeof(string).FullName!, text);
    }

    [Fact]
    public async Task InvokeAsync_ScriptObject_RendersComponentTemplate()
    {
        var componentsDir = Path.Combine(_themeDir, "components");
        Directory.CreateDirectory(componentsDir);
        File.WriteAllText(Path.Combine(componentsDir, "badge.html"), "<span>{{ data.text }}</span>");

        var function = CreateFunction(new Dictionary<string, ThemeComponentDefinition>
        {
            ["badge"] = new() { Template = "components/badge.html" }
        });

        var data = new ScriptObject();
        data.Add("text", "Hello");
        var globals = new ScriptObject();
        globals.Add("data", data);

        var arguments = new ScriptArray
        {
            "badge",
            globals
        };

        var result = await function.InvokeAsync(new TemplateContext(), callerContext: null, arguments, blockStatement: null);

        Assert.Equal("<span>Hello</span>", Assert.IsType<string>(result));
    }

    [Fact]
    public void Metadata_ReportsExpectedSignature()
    {
        var function = CreateFunction(new Dictionary<string, ThemeComponentDefinition>());

        Assert.Equal(1, function.RequiredParameterCount);
        Assert.Equal(2, function.ParameterCount);
        Assert.Equal(ScriptVarParamKind.None, function.VarParamKind);
        Assert.Equal(typeof(string), function.ReturnType);
        Assert.Equal("name", function.GetParameterInfo(0).Name);
        Assert.Equal("data", function.GetParameterInfo(1).Name);
    }

    private RenderComponentFunction CreateFunction(IReadOnlyDictionary<string, ThemeComponentDefinition> components)
    {
        return new RenderComponentFunction(
            components,
            new FileTemplateLoader(_themeDir),
            new ScriptObject(),
            _themeDir,
            componentValidation: "off");
    }
}
