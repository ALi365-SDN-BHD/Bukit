using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Theme;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Bukit.Rendering.Scriban;

public sealed class ScribanTemplateRenderer
{
    private const int MaxLayoutDepth = 10;
    private readonly string _layoutsDir;
    private readonly FileTemplateLoader _templateLoader;
    private readonly IReadOnlyDictionary<string, string>? _shortcodes;
    private readonly IReadOnlyDictionary<string, ComponentDefinition>? _components;
    private readonly ConcurrentDictionary<string, CachedTemplate> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ThemeComponentRegistry? _themeRegistry;
    private readonly SectionSchemaValidator? _schemaValidator;
    private readonly SectionDataResolverAccessor? _dataResolver;
    private readonly string _componentValidation;
    private readonly IReadOnlyList<(ContentItem Item, RouteInfo? Route)>? _allPages;
    private readonly IReadOnlyDictionary<string, ISectionPlugin>? _sectionPlugins;
    private readonly ConcurrentDictionary<string, CachedSectionTemplate> _sectionTemplateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TemplateContextBuilder _contextBuilder;

    public ScribanTemplateRenderer(string layoutsDir, string? parentLayoutsDir = null, IReadOnlyDictionary<string, string>? shortcodes = null, IReadOnlyDictionary<string, ComponentDefinition>? components = null, string? userLayoutsDir = null)
        : this(layoutsDir, parentLayoutsDir, shortcodes, components, userLayoutsDir, null, null, null, "off", null, null)
    {
    }

    public ScribanTemplateRenderer(string layoutsDir, string? parentLayoutsDir, IReadOnlyDictionary<string, string>? shortcodes, IReadOnlyDictionary<string, ComponentDefinition>? components, string? userLayoutsDir, ThemeComponentRegistry? themeRegistry, SectionSchemaValidator? schemaValidator, SectionDataResolverAccessor? dataResolver, string componentValidation, IReadOnlyList<(ContentItem, RouteInfo?)>? allPages = null, IReadOnlyDictionary<string, ISectionPlugin>? sectionPlugins = null)
    {
        _layoutsDir = layoutsDir;
        _templateLoader = new FileTemplateLoader(_layoutsDir, parentLayoutsDir, userLayoutsDir);
        _shortcodes = shortcodes;
        _components = components;
        _themeRegistry = themeRegistry;
        _schemaValidator = schemaValidator;
        _dataResolver = dataResolver;
        _componentValidation = componentValidation;
        _allPages = allPages;
        _sectionPlugins = sectionPlugins;
        _contextBuilder = new TemplateContextBuilder(
            _templateLoader, _shortcodes, _components,
            _themeRegistry, _schemaValidator, _componentValidation,
            _allPages, _sectionPlugins, TryGetCachedSectionTemplate);
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
            throw new RenderException($"Layout nesting depth exceeded maximum of {MaxLayoutDepth}. Possible circular layout reference in '{templateRelativePath}'.", DiagnosticCode.RenderLayoutNestingExceeded);
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
        var context = _contextBuilder.BuildContext(globals);

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
            if (TryFindRenderException(ex, out var renderException))
            {
                throw renderException;
            }

            throw new RenderException($"Render failed: {templateRelativePath}", ex);
        }
    }

    private static bool TryFindRenderException(Exception exception, out RenderException renderException)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is RenderException found)
            {
                renderException = found;
                return true;
            }
        }

        renderException = null!;
        return false;
    }

    private CachedTemplate GetCachedTemplate(string templateRelativePath)
    {
        var templatePath = ResolveTemplatePath(templateRelativePath);
        var fileInfo = new FileInfo(templatePath);
        if (!fileInfo.Exists)
        {
            throw new RenderException($"Template not found: {templateRelativePath}", DiagnosticCode.RenderTemplateNotFound);
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
            throw new RenderException($"Template path is invalid: {templateRelativePath}", ex, DiagnosticCode.RenderTemplateNotFound);
        }
    }

    private static Template ParseTemplateOrThrow(string text, string templatePath, string templateRelativePath)
    {
        var template = Template.Parse(text, templatePath);
        if (template.HasErrors)
        {
            throw new RenderException($"Template parse error: {templateRelativePath}\n{template.Messages}", DiagnosticCode.RenderTemplateParseError);
        }

        return template;
    }

    private readonly record struct FileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedTemplate(FileSignature Signature, Template Template, string? LayoutTemplateRelativePath);

    private readonly record struct SectionFileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedSectionTemplate(SectionFileSignature Signature, Template Template);

    private bool TryGetCachedSectionTemplate(string templatePath, out Template template)
    {
        var fileInfo = new FileInfo(templatePath);
        if (!fileInfo.Exists)
        {
            template = null!;
            return false;
        }

        var signature = new SectionFileSignature(fileInfo.LastWriteTimeUtc, fileInfo.Length);
        if (_sectionTemplateCache.TryGetValue(templatePath, out var cached) && cached.Signature.Equals(signature))
        {
            template = cached.Template;
            return true;
        }

        var templateText = File.ReadAllText(templatePath);
        template = Template.Parse(templateText, templatePath);
        if (!template.HasErrors)
        {
            _sectionTemplateCache[templatePath] = new CachedSectionTemplate(signature, template);
        }

        return true;
    }
}

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

public sealed class SectionDataResolverAccessor
{
    internal IReadOnlyList<ContentItem>? AllItems { get; set; }
    internal ThemeComponentRegistry? Registry { get; set; }

    public IReadOnlyList<ContentItem>? ResolveData(PageSectionDefinition sectionDef)
    {
        return null;
    }
}

internal static class ImageHelper
{
    internal static string BuildSrcset(string imagePath, string sizes = "480,768,1200")
    {
        if (!IsSafeImageSource(imagePath))
        {
            return string.Empty;
        }

        var encodedImagePath = WebUtility.HtmlEncode(imagePath.Trim());
        var sb = new StringBuilder();
        var sizeList = sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var size in sizeList)
        {
            if (!int.TryParse(size, out var width) || width <= 0)
            {
                continue;
            }

            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"{encodedImagePath}?w={width} {width}w");
        }
        return sb.ToString();
    }

    internal static string BuildImgTag(string src, string alt = "", string sizes = "480,768,1200", string className = "")
    {
        if (!IsSafeImageSource(src))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var safeSrc = WebUtility.HtmlEncode(src.Trim());
        var safeAlt = WebUtility.HtmlEncode(alt ?? string.Empty);
        var safeClass = WebUtility.HtmlEncode(className ?? string.Empty);
        var sizesAttr = $"(max-width: 480px) 480px, (max-width: 768px) 768px, 1200px";

        sb.Append($"<img src=\"{safeSrc}\"");
        sb.Append($" srcset=\"{BuildSrcset(src, sizes)}\"");
        sb.Append($" sizes=\"{sizesAttr}\"");
        if (!string.IsNullOrWhiteSpace(alt))
        {
            sb.Append($" alt=\"{safeAlt}\"");
        }
        if (!string.IsNullOrWhiteSpace(className))
        {
            sb.Append($" class=\"{safeClass}\"");
        }
        sb.Append(" loading=\"lazy\" decoding=\"async\" />");
        return sb.ToString();
    }

    private static bool IsSafeImageSource(string? src)
    {
        if (string.IsNullOrWhiteSpace(src))
        {
            return false;
        }

        var value = src.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

internal sealed class ImageSrcsetFunction : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var src = arguments.Count > 0 ? arguments[0]?.ToString() ?? string.Empty : string.Empty;
        var sizes = arguments.Count > 1 ? arguments[1]?.ToString() ?? "480,768,1200" : "480,768,1200";
        return ImageHelper.BuildSrcset(src, sizes);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
        => new(Invoke(context, callerContext, arguments, blockStatement));

    public int RequiredParameterCount => 1;
    public int ParameterCount => 2;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(string), index == 0 ? "src" : "sizes");
}

internal sealed class ImageImgFunction : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var src = arguments.Count > 0 ? arguments[0]?.ToString() ?? string.Empty : string.Empty;
        var alt = arguments.Count > 1 ? arguments[1]?.ToString() ?? string.Empty : string.Empty;
        var sizes = arguments.Count > 2 ? arguments[2]?.ToString() ?? "480,768,1200" : "480,768,1200";
        var className = arguments.Count > 3 ? arguments[3]?.ToString() ?? string.Empty : string.Empty;
        return ImageHelper.BuildImgTag(src, alt, sizes, className);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
        => new(Invoke(context, callerContext, arguments, blockStatement));

    public int RequiredParameterCount => 1;
    public int ParameterCount => 4;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index)
        => new(typeof(string), index switch
        {
            0 => "src",
            1 => "alt",
            2 => "sizes",
            _ => "className"
        });
}
