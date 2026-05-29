using Scriban;
using Scriban.Runtime;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Theme;
using System.Collections.Concurrent;

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
