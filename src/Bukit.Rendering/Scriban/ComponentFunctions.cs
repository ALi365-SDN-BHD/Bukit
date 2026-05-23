using Bukit.Config;
using Bukit.Theme;
using Scriban;
using Scriban.Runtime;
using System.Text;

namespace Bukit.Rendering.Scriban;

internal sealed class ComponentFunctions
{
    internal static IReadOnlyDictionary<string, ComponentDefinition>? Components;
    internal static FileTemplateLoader? TemplateLoader;
    internal static ScriptObject? ParentGlobals;

    internal static IReadOnlyDictionary<string, ThemeComponentDefinition>? ThemeComponents;
    internal static FileTemplateLoader? ThemeTemplateLoader;
    internal static ScriptObject? ThemeParentGlobals;
    internal static string? ThemeRegistryRoot;

    public static string Render(string name, string arg1 = "", string arg2 = "", string arg3 = "")
    {
        if (Components is null || !Components.TryGetValue(name, out var compDef))
        {
            return $"<!-- component not found: {name} -->";
        }

        try
        {
            var resolveCtx = new TemplateContext { TemplateLoader = TemplateLoader };
            var resolvedPath = TemplateLoader!.GetPath(resolveCtx, default, compDef.Template);

            var compContext = new TemplateContext
            {
                TemplateLoader = TemplateLoader,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedTargetAccess = true,
                EnableNullIndexer = true
            };

            var componentGlobals = new ScriptObject();
            if (compDef.Props is { Count: > 0 })
            {
                var props = new List<string>();
                if (!string.IsNullOrEmpty(arg1)) props.Add(arg1);
                if (!string.IsNullOrEmpty(arg2)) props.Add(arg2);
                if (!string.IsNullOrEmpty(arg3)) props.Add(arg3);

                var propIndex = 0;
                foreach (var (propName, _) in compDef.Props)
                {
                    var val = propIndex < props.Count ? props[propIndex] : compDef.Props[propName];
                    componentGlobals.SetValue(propName, val, readOnly: true);
                    propIndex++;
                }
            }

            var templateText = TemplateLoader!.Load(compContext, default, resolvedPath);
            if (string.IsNullOrEmpty(templateText))
            {
                return $"<!-- component template not found: {compDef.Template} -->";
            }

            var compTemplate = Template.Parse(templateText);
            if (compTemplate.HasErrors)
            {
                return $"<!-- component error: {compTemplate.Messages} -->";
            }

            compContext.PushGlobal(componentGlobals);
            if (ParentGlobals is not null)
            {
                compContext.PushGlobal(ParentGlobals);
            }
            return compTemplate.Render(compContext);
        }
        catch (Exception ex)
        {
            return $"<!-- component error: {ex.Message} -->";
        }
    }

    public static string RenderComponent(string name, object data)
    {
        if (ThemeComponents is null || !ThemeComponents.TryGetValue(name, out var compDef))
        {
            return $"<!-- component not found: {name} -->";
        }

        try
        {
            var templatePath = !string.IsNullOrEmpty(ThemeRegistryRoot)
                ? Path.Combine(ThemeRegistryRoot, compDef.Template)
                : compDef.Template;

            if (!File.Exists(templatePath))
            {
                return $"<!-- component template not found: {compDef.Template} -->";
            }

            var compContext = new TemplateContext
            {
                TemplateLoader = ThemeTemplateLoader,
                EnableRelaxedMemberAccess = true,
                EnableRelaxedTargetAccess = true,
                EnableNullIndexer = true
            };

            var templateText = File.ReadAllText(templatePath);
            var compTemplate = Template.Parse(templateText);
            if (compTemplate.HasErrors)
            {
                return $"<!-- component error: {compTemplate.Messages} -->";
            }

            if (data is ScriptObject so)
            {
                compContext.PushGlobal(so);
            }

            if (ThemeParentGlobals is not null)
            {
                compContext.PushGlobal(ThemeParentGlobals);
            }

            return compTemplate.Render(compContext);
        }
        catch (Exception ex)
        {
            return $"<!-- component error: {ex.Message} -->";
        }
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
