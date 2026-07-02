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

            var compContext = new TemplateContext
            {
                TemplateLoader = _templateLoader,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedTargetAccess = true,
                EnableNullIndexer = true
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

    private static bool DefaultGetCachedTemplate(string templatePath, out Template template)
    {
        if (!File.Exists(templatePath))
        {
            template = null!;
            return false;
        }

        var templateText = File.ReadAllText(templatePath);
        template = Template.Parse(templateText, templatePath);
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

            var compContext = new TemplateContext
            {
                TemplateLoader = _templateLoader,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedTargetAccess = true,
                EnableNullIndexer = true
            };

            if (!_getCachedTemplate(templatePath, out var compTemplate))
            {
                return Diagnostic("theme.component.template_not_found", $"component template not found: {compDef.Template}");
            }
            if (compTemplate.HasErrors)
            {
                return Diagnostic("theme.component.template_parse_failed", $"component error: {compTemplate.Messages}");
            }

            if (data is ScriptObject so)
            {
                compContext.PushGlobal(so);
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
}
