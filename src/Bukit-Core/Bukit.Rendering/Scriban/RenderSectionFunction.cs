using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Bukit.Rendering.Scriban;

internal sealed class RenderSectionFunction : IScriptCustomFunction
{
    private readonly SectionRenderHelper _helper;

    public RenderSectionFunction(SectionRenderHelper helper)
    {
        _helper = helper;
    }

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var firstArg = arguments.Count > 0 ? arguments[0] : null;

        if (firstArg is ScriptObject so)
        {
            return _helper.RenderScriptObjectSection(so, _helper.ParentGlobals);
        }

        var json = firstArg?.ToString() ?? "";
        return _helper.render_section(json);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount => 1;
    public int ParameterCount => 1;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(string), "json");
}
