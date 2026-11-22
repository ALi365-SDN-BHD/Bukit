using Bukit.Config;
using Bukit.Shared;
using Bukit.Theme;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using System.Text;

namespace Bukit.Rendering.Scriban;

internal sealed class ComponentRenderFunction : IScriptCustomFunction
{
    private readonly IReadOnlyDictionary<string, ComponentDefinition> _components;
    private readonly FileTemplateLoader _templateLoader;
    private readonly ScriptObject _parentGlobals;
    private readonly string _componentValidation;

    public ComponentRenderFunction(
        IReadOnlyDictionary<string, ComponentDefinition> components,
        FileTemplateLoader templateLoader,
        ScriptObject parentGlobals,
        string componentValidation)
    {
        _components = components;
        _templateLoader = templateLoader;
        _parentGlobals = parentGlobals;
        _componentValidation = componentValidation;
    }

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var name = arguments.Count > 0 ? arguments[0]?.ToString() ?? string.Empty : string.Empty;
        if (!_components.TryGetValue(name, out var compDef))
        {
            return Diagnostic("theme.component.not_found", $"component not found: {name}");
        }

        try
        {
            var resolveCtx = new TemplateContext { TemplateLoader = _templateLoader };
            var resolvedPath = _templateLoader.GetPath(resolveCtx, default, compDef.Template);

            var compContext = new BukitTemplateContext
            {
                TemplateLoader = _templateLoader,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedTargetAccess = true,
                EnableNullIndexer = true,
                LimitToString = 0
            };

            var componentGlobals = new ScriptObject();
            if (compDef.Props is { Count: > 0 })
            {
                var props = new List<string>();
                for (var i = 1; i < arguments.Count; i++)
                {
                    var value = arguments[i]?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        props.Add(value);
                    }
                }

                var propIndex = 0;
                foreach (var (propName, _) in compDef.Props)
                {
                    var val = propIndex < props.Count ? props[propIndex] : compDef.Props[propName];
                    componentGlobals.SetValue(propName, val, readOnly: true);
                    RenderReadRecorder.Current?.Declare(propName);
                    propIndex++;
                }
            }

            var templateText = _templateLoader.Load(compContext, default, resolvedPath);
            if (string.IsNullOrEmpty(templateText))
            {
                return Diagnostic("theme.component.template_not_found", $"component template not found: {compDef.Template}");
            }

            var compTemplate = Template.Parse(templateText);
            if (compTemplate.HasErrors)
            {
                return Diagnostic("theme.component.template_parse_failed", $"component error: {compTemplate.Messages}");
            }

            compContext.PushGlobal(componentGlobals);
            compContext.PushGlobal(_parentGlobals);
            return compTemplate.Render(compContext);
        }
        catch (Exception ex)
        {
            if (ex is RenderException)
            {
                throw;
            }

            return Diagnostic("theme.component.render_failed", $"component error: {ex.Message}");
        }
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount => 1;
    public int ParameterCount => 4;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.Direct;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(string), index == 0 ? "name" : $"arg{index}");

    private string Diagnostic(string code, string message)
    {
        var diagnostic = $"code={code} {message}";
        if (string.Equals(_componentValidation, "strict", StringComparison.OrdinalIgnoreCase))
        {
            throw new RenderException(diagnostic, DiagnosticCode.RenderComponentFailed);
        }

        return $"<!-- {diagnostic} -->";
    }
}

internal sealed class ThemeComponentRenderFunction : IScriptCustomFunction
{
    private readonly IReadOnlyDictionary<string, ThemeComponentDefinition> _components;
    private readonly FileTemplateLoader _templateLoader;
    private readonly ScriptObject _parentGlobals;
    private readonly string _registryRoot;
    private readonly string _componentValidation;
    private readonly SectionRenderHelper.GetCachedSectionTemplate _getCachedTemplate;

    public ThemeComponentRenderFunction(
        IReadOnlyDictionary<string, ThemeComponentDefinition> components,
        FileTemplateLoader templateLoader,
        ScriptObject parentGlobals,
        string registryRoot,
        string componentValidation,
        SectionRenderHelper.GetCachedSectionTemplate? getCachedTemplate = null)
    {
        _components = components;
        _templateLoader = templateLoader;
        _parentGlobals = parentGlobals;
        _registryRoot = registryRoot;
        _componentValidation = componentValidation;
        _getCachedTemplate = getCachedTemplate ?? DefaultGetCachedTemplate;
    }

    private bool DefaultGetCachedTemplate(string templatePath, out Template template)
    {
        if (!File.Exists(templatePath))
        {
            template = null!;
            return false;
        }

        var source = _templateLoader.LoadVerifiedSource(templatePath);
        template = Template.Parse(source.Text, templatePath);
        return true;
    }

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var name = arguments.Count > 0 ? arguments[0]?.ToString() ?? string.Empty : string.Empty;
        var data = arguments.Count > 1 ? arguments[1] : null;
        return Render(name, data);
    }

    public string Render(string name, object? data)
    {
        if (!_components.TryGetValue(name, out var compDef))
        {
            return Diagnostic("theme.component.not_found", $"component not found: {name}");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(compDef.Template) || Path.IsPathRooted(compDef.Template))
            {
                return Diagnostic("theme.component.template_invalid", $"component template path is invalid: {compDef.Template}");
            }

            var registryRootFull = Path.GetFullPath(_registryRoot);
            var templatePath = Path.GetFullPath(Path.Combine(registryRootFull, compDef.Template));
            var safeRoot = registryRootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!templatePath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Diagnostic("theme.component.template_invalid", $"component template path escapes theme root: {compDef.Template}");
            }

            if (!File.Exists(templatePath))
            {
                return Diagnostic("theme.component.template_not_found", $"component template not found: {compDef.Template}");
            }

            var compContext = new BukitTemplateContext
            {
                TemplateLoader = _templateLoader,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedTargetAccess = true,
                EnableNullIndexer = true,
                LimitToString = 0
            };

            if (!_getCachedTemplate(templatePath, out var compTemplate))
            {
                return Diagnostic("theme.component.template_not_found", $"component template not found: {compDef.Template}");
            }
            if (compTemplate.HasErrors)
            {
                return Diagnostic("theme.component.template_parse_failed", $"component error: {compTemplate.Messages}");
            }

            ScribanTemplateReadAnalyzer.Record(compTemplate);
            if (data is ScriptObject so)
            {
                compContext.PushGlobal(so);
                if (RenderReadRecorder.Current is { } recorder)
                {
                    foreach (var propName in so.Keys)
                    {
                        recorder.Declare(propName);
                    }
                }
            }

            compContext.PushGlobal(_parentGlobals);

            return compTemplate.Render(compContext);
        }
        catch (Exception ex)
        {
            if (ex is RenderException)
            {
                throw;
            }

            return Diagnostic("theme.component.render_failed", $"component error: {ex.Message}");
        }
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount => 1;
    public int ParameterCount => 2;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(object), index == 0 ? "name" : "data");

    private string Diagnostic(string code, string message)
    {
        var diagnostic = $"code={code} {message}";
        if (string.Equals(_componentValidation, "strict", StringComparison.OrdinalIgnoreCase))
        {
            throw new RenderException(diagnostic, DiagnosticCode.RenderComponentFailed);
        }

        return $"<!-- {diagnostic} -->";
    }
}

internal static class ComponentUtilityFunctions
{
    /// <summary>
    /// Formats a DateTimeOffset to a readable string.
    /// Usage: {{ date | util.format_date '%Y-%m-%d' }}
    /// </summary>
    public static string FormatDate(object? input, string format = "yyyy-MM-dd")
    {
        if (input is DateTimeOffset dto) return dto.ToString(format);
        if (input is DateTime dt) return dt.ToString(format);
        if (input is string s && DateTimeOffset.TryParse(s, out var parsedDto)) return parsedDto.ToString(format);
        if (input is string s2 && DateTime.TryParse(s2, out var parsedDt)) return parsedDt.ToString(format);
        return input?.ToString() ?? "";
    }

    /// <summary>
    /// Truncates text to a maximum length, appending ellipsis if truncated.
    /// Usage: {{ text | util.truncate 100 }}
    /// </summary>
    public static string Truncate(object? input, int maxLength = 100)
    {
        if (input is null) return "";
        var text = input.ToString() ?? "";
        if (text.Length <= maxLength) return text;
        return text[..maxLength].TrimEnd() + "…";
    }

    /// <summary>
    /// Converts camelCase or snake_case to Title Case.
    /// Usage: {{ 'my_section_name' | util.titleize }}
    /// </summary>
    public static string Titleize(object? input)
    {
        if (input is null) return "";
        var text = input.ToString() ?? "";
        text = text.Replace('_', ' ').Replace('-', ' ');
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length > 0 ? char.ToUpper(word[0]) + word[1..].ToLower() : ""));
    }

    /// <summary>
    /// Converts a string to a URL-friendly slug.
    /// Usage: {{ 'My Page Title' | util.slugify }}
    /// </summary>
    public static string Slugify(object? input)
    {
        if (input is null) return "";
        var text = input.ToString() ?? "";
        text = text.ToLowerInvariant().Trim();
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '-') sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '_') sb.Append('-');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Slug of a taxonomy term, identical to the taxonomy page URL segment
    /// (percent-encoded values decoded, whitespace collapsed, Latin accents folded).
    /// Usage: {{ tag | util.term_slug }}
    /// </summary>
    public static string TermSlug(object? input)
    {
        return TermValueNormalizer.ToSlug(input?.ToString());
    }
}

/// <summary>
/// The shared <c>util</c> template object. Its functions are stateless, so one frozen instance
/// serves every page, component and section context; templates cannot add or change members.
/// </summary>
internal static class UtilTemplateFunctions
{
    internal static readonly ScriptObject Util = Create();

    private static ScriptObject Create()
    {
        var utilObj = new ScriptObject();
        // IScriptCustomFunction instead of Func delegates: delegates are bound by reflection and fail under Native AOT.
        utilObj.SetValue("format_date", UtilStringFunction.WithOptionalArg((input, format) =>
        {
            var fmt = format?.ToString();
            return string.IsNullOrEmpty(fmt)
                ? ComponentUtilityFunctions.FormatDate(input)
                : ComponentUtilityFunctions.FormatDate(input, fmt);
        }, "format"), readOnly: true);
        utilObj.SetValue("truncate", UtilStringFunction.WithOptionalArg((input, maxLen) =>
            int.TryParse(maxLen?.ToString(), out var n) ? ComponentUtilityFunctions.Truncate(input, n) : ComponentUtilityFunctions.Truncate(input), "max_length"), readOnly: true);
        utilObj.SetValue("titleize", UtilStringFunction.Unary(ComponentUtilityFunctions.Titleize), readOnly: true);
        utilObj.SetValue("slugify", UtilStringFunction.Unary(ComponentUtilityFunctions.Slugify), readOnly: true);
        utilObj.SetValue("term_slug", UtilStringFunction.Unary(ComponentUtilityFunctions.TermSlug), readOnly: true);
        utilObj.SetValue("json_string", new JsonStringFunction(), readOnly: true);
        utilObj.SetValue("to_json", ToJsonFunction.Instance, readOnly: true);
        utilObj.IsReadOnly = true;
        return utilObj;
    }

    /// <summary>
    /// A per-context global holding <c>util</c> as a writable variable, so a template may still
    /// assign its own <c>util</c> (as before) without touching the shared object.
    /// </summary>
    internal static ScriptObject CreateGlobal() => new() { ["util"] = Util };
}

/// <summary>
/// AOT-safe wrapper for util.* string helpers. Scriban binds a plain <c>Func</c>
/// through reflection, which Native AOT trims away ("not a valid function").
/// </summary>
internal sealed class UtilStringFunction : IScriptCustomFunction
{
    private readonly Func<object?, object?, string> _invoke;
    private readonly string[] _parameterNames;

    private UtilStringFunction(Func<object?, object?, string> invoke, int requiredParameterCount, params string[] parameterNames)
    {
        _invoke = invoke;
        RequiredParameterCount = requiredParameterCount;
        _parameterNames = parameterNames;
    }

    internal static UtilStringFunction Unary(Func<object?, string> invoke)
        => new((value, _) => invoke(value), 1, "value");

    internal static UtilStringFunction WithOptionalArg(Func<object?, object?, string> invoke, string argName)
        => new(invoke, 1, "value", argName);

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var value = arguments.Count > 0 ? arguments[0] : null;
        var arg = arguments.Count > 1 ? arguments[1] : null;
        return _invoke(value, arg);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount { get; }
    public int ParameterCount => _parameterNames.Length;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(object), _parameterNames[Math.Min(index, _parameterNames.Length - 1)]);
}

/// <summary>
/// <c>util.json_string</c>: the value's text as a JSON string literal, safe inside an HTML script element.
/// </summary>
internal sealed class JsonStringFunction : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var value = arguments.Count > 0 ? arguments[0] : null;
        return TemplateJsonWriter.SerializeString(value?.ToString() ?? string.Empty);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount => 1;
    public int ParameterCount => 1;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(object), "value");
}

/// <summary>
/// <c>util.to_json</c> and the <c>object.to_json</c> override: AOT-safe JSON for any template value.
/// Scriban's built-in <c>object.to_json</c> uses reflection-based System.Text.Json serialization,
/// which Native AOT disables.
/// </summary>
internal sealed class ToJsonFunction : IScriptCustomFunction
{
    internal static readonly ToJsonFunction Instance = new();

    private ToJsonFunction()
    {
    }

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var value = arguments.Count > 0 ? arguments[0] : null;
        return TemplateJsonWriter.Serialize(value);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return new ValueTask<object?>(Invoke(context, callerContext, arguments, blockStatement));
    }

    public int RequiredParameterCount => 1;
    public int ParameterCount => 1;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(object), "value");
}
