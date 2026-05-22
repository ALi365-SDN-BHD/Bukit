using Bukit.Config;
using Bukit.Theme;
using Scriban;
using Scriban.Runtime;

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
