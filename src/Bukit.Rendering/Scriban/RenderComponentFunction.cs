using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using Bukit.Theme;

namespace Bukit.Rendering.Scriban;

internal sealed class RenderComponentFunction : IScriptCustomFunction
{
    private readonly ThemeComponentRenderFunction _renderer;

    public RenderComponentFunction(
        IReadOnlyDictionary<string, ThemeComponentDefinition> components,
        FileTemplateLoader templateLoader,
        ScriptObject parentGlobals,
        string registryRoot,
        string componentValidation,
        SectionRenderHelper.GetCachedSectionTemplate? getCachedTemplate = null)
    {
        _renderer = new ThemeComponentRenderFunction(components, templateLoader, parentGlobals, registryRoot, componentValidation, getCachedTemplate);
    }

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var name = arguments.Count > 0 ? arguments[0]?.ToString() ?? "" : "";
        var data = arguments.Count > 1 ? arguments[1] : null;

        if (data is ScriptObject so)
        {
            return _renderer.Render(name, so);
        }

        return $"<!-- component: data is {(data?.GetType().FullName ?? "null")} not ScriptObject -->";
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount => 1;
    public int ParameterCount => 2;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(string), index == 0 ? "name" : "data");
}
