using Scriban;
using Scriban.Runtime;
using Bukit.Config;
using Bukit.Shared;
using System.Collections.Concurrent;
using System.Text;

namespace Bukit.Rendering.Scriban;

public sealed class ScribanTemplateRenderer
{
    private const int MaxLayoutDepth = 10;
    private readonly string _layoutsDir;
    private readonly FileTemplateLoader _templateLoader;
    private readonly IReadOnlyDictionary<string, string>? _shortcodes;
    private readonly IReadOnlyDictionary<string, ComponentDefinition>? _components;
    private readonly ConcurrentDictionary<string, CachedTemplate> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ScribanTemplateRenderer(string layoutsDir, string? parentLayoutsDir = null, IReadOnlyDictionary<string, string>? shortcodes = null, IReadOnlyDictionary<string, ComponentDefinition>? components = null)
    {
        _layoutsDir = layoutsDir;
        _templateLoader = new FileTemplateLoader(_layoutsDir, parentLayoutsDir);
        _shortcodes = shortcodes;
        _components = components;
    }

    public string RenderPage(string templateRelativePath, PageModel model)
    {
        var globals = ScribanModelBinder.ToScriptObject(model);
        return Render(templateRelativePath, globals, 0);
    }

    public string RenderList(string templateRelativePath, ListPageModel model)
    {
        var globals = ScribanModelBinder.ToScriptObject(model);
        return Render(templateRelativePath, globals, 0);
    }

    private string Render(string templateRelativePath, ScriptObject globals, int depth)
    {
        if (depth >= MaxLayoutDepth)
        {
            throw new RenderException($"Layout nesting depth exceeded maximum of {MaxLayoutDepth}. Possible circular layout reference in '{templateRelativePath}'.");
        }

        var cached = GetCachedTemplate(templateRelativePath);
        if (cached.LayoutTemplateRelativePath is not null)
        {
            var body = RenderTemplate(cached.Template, templateRelativePath, globals);
            globals.SetValue("content", body, readOnly: true);
            return Render(cached.LayoutTemplateRelativePath, globals, depth + 1);
        }

        return RenderTemplate(cached.Template, templateRelativePath, globals);
    }

    private string RenderTemplate(Template template, string templateRelativePath, ScriptObject globals)
    {
        var context = new TemplateContext
        {
            TemplateLoader = _templateLoader,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true,
            EnableNullIndexer = true
        };

        if (_shortcodes is { Count: > 0 })
        {
            var shortcodeObj = new ScriptObject();
            var capturedShortcodes = _shortcodes;
            shortcodeObj.SetValue("shortcode", new Func<string, string, string, string, string, string>((name, a1, a2, a3, a4) =>
            {
                var args = new List<string>();
                if (!string.IsNullOrEmpty(a1)) args.Add(a1);
                if (!string.IsNullOrEmpty(a2)) args.Add(a2);
                if (!string.IsNullOrEmpty(a3)) args.Add(a3);
                if (!string.IsNullOrEmpty(a4)) args.Add(a4);
                return ShortcodeProcessor.RenderShortcode(name, capturedShortcodes, args.ToArray());
            }), readOnly: true);
            context.PushGlobal(shortcodeObj);
        }

        if (_components is { Count: > 0 })
        {
            ComponentFunctions.Components = _components;
            ComponentFunctions.TemplateLoader = _templateLoader;
            ComponentFunctions.ParentGlobals = globals;
            var componentObj = new ScriptObject();
            componentObj.SetValue("render", new Func<string, string, string, string, string>(ComponentFunctions.Render), readOnly: true);
            context.PushGlobal(new ScriptObject { ["comp"] = componentObj });
        }

        var imageObj = new ScriptObject();
        imageObj.SetValue("srcset", new Func<string, string, string>(ImageHelper.BuildSrcset), readOnly: true);
        imageObj.SetValue("img", new Func<string, string, string, string, string>(ImageHelper.BuildImgTag), readOnly: true);
        context.PushGlobal(new ScriptObject { ["image"] = imageObj });

        context.PushGlobal(globals);

        try
        {
            var result = template.Render(context);
            if (_shortcodes is { Count: > 0 })
            {
                result = ShortcodeProcessor.RenderShortcodes(result, _shortcodes);
            }
            return result;
        }
        catch (Exception ex)
        {
            throw new RenderException($"Render failed: {templateRelativePath}", ex);
        }
    }

    private CachedTemplate GetCachedTemplate(string templateRelativePath)
    {
        var templatePath = ResolveTemplatePath(templateRelativePath);
        var fileInfo = new FileInfo(templatePath);
        if (!fileInfo.Exists)
        {
            throw new RenderException($"Template not found: {templateRelativePath}");
        }

        var signature = new FileSignature(fileInfo.LastWriteTimeUtc, fileInfo.Length);
        if (_cache.TryGetValue(templatePath, out var existing) && existing.Signature.Equals(signature))
        {
            return existing;
        }

        var templateText = File.ReadAllText(templatePath);
        CachedTemplate parsed;
        if (ScribanLayoutDirectiveParser.TryExtractLayoutDirective(templateText, out var layoutTemplateRelativePath, out var bodyTemplateText))
        {
            var bodyTemplate = ParseTemplateOrThrow(bodyTemplateText, templatePath, templateRelativePath);
            parsed = new CachedTemplate(signature, bodyTemplate, layoutTemplateRelativePath);
        }
        else
        {
            var template = ParseTemplateOrThrow(templateText, templatePath, templateRelativePath);
            parsed = new CachedTemplate(signature, template, null);
        }

        _cache[templatePath] = parsed;
        return parsed;
    }

    private string ResolveTemplatePath(string templateRelativePath)
    {
        try
        {
            return _templateLoader.GetPath(new TemplateContext(), default, templateRelativePath);
        }
        catch (Exception ex)
        {
            throw new RenderException($"Template path is invalid: {templateRelativePath}", ex);
        }
    }

    private static Template ParseTemplateOrThrow(string text, string templatePath, string templateRelativePath)
    {
        var template = Template.Parse(text, templatePath);
        if (template.HasErrors)
        {
            throw new RenderException($"Template parse error: {templateRelativePath}\n{template.Messages}");
        }

        return template;
    }

    private readonly record struct FileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedTemplate(FileSignature Signature, Template Template, string? LayoutTemplateRelativePath);
}

internal static class ImageHelper
{
    internal static string BuildSrcset(string imagePath, string sizes = "480,768,1200")
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var sizeList = sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var size in sizeList)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"{imagePath}?w={size} {size}w");
        }
        return sb.ToString();
    }

    internal static string BuildImgTag(string src, string alt = "", string sizes = "480,768,1200", string className = "")
    {
        if (string.IsNullOrWhiteSpace(src))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var classAttr = string.IsNullOrWhiteSpace(className) ? "" : $" class=\"{className}\"";
        var sizesAttr = $"(max-width: 480px) 480px, (max-width: 768px) 768px, 1200px";

        sb.Append($"<img src=\"{src}\"");
        sb.Append($" srcset=\"{BuildSrcset(src, sizes)}\"");
        sb.Append($" sizes=\"{sizesAttr}\"");
        if (!string.IsNullOrWhiteSpace(alt))
        {
            sb.Append($" alt=\"{alt}\"");
        }
        if (!string.IsNullOrWhiteSpace(className))
        {
            sb.Append($" class=\"{className}\"");
        }
        sb.Append(" loading=\"lazy\" decoding=\"async\" />");
        return sb.ToString();
    }
}
