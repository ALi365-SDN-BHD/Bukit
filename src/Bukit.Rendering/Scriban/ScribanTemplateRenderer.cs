using Scriban;
using Scriban.Runtime;
using Bukit.Shared;
using System.Collections.Concurrent;

namespace Bukit.Rendering.Scriban;

public sealed class ScribanTemplateRenderer
{
    private const int MaxLayoutDepth = 10;
    private readonly string _layoutsDir;
    private readonly FileTemplateLoader _templateLoader;
    private readonly ConcurrentDictionary<string, CachedTemplate> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ScribanTemplateRenderer(string layoutsDir)
    {
        _layoutsDir = layoutsDir;
        _templateLoader = new FileTemplateLoader(_layoutsDir);
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

        context.PushGlobal(globals);

        try
        {
            return template.Render(context);
        }
        catch (Exception ex)
        {
            throw new RenderException($"Render failed: {templateRelativePath}", ex);
        }
    }

    private CachedTemplate GetCachedTemplate(string templateRelativePath)
    {
        var templatePath = Path.Combine(_layoutsDir, templateRelativePath.Replace('/', Path.DirectorySeparatorChar));
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
        if (TryExtractLayoutDirective(templateText, out var layoutTemplateRelativePath, out var bodyTemplateText))
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

    private static Template ParseTemplateOrThrow(string text, string templatePath, string templateRelativePath)
    {
        var template = Template.Parse(text, templatePath);
        if (template.HasErrors)
        {
            throw new RenderException($"Template parse error: {templateRelativePath}\n{template.Messages}");
        }

        return template;
    }

    private static bool TryExtractLayoutDirective(string templateText, out string layoutTemplateRelativePath, out string bodyTemplateText)
    {
        layoutTemplateRelativePath = string.Empty;
        bodyTemplateText = templateText;

        var lines = templateText.ReplaceLineEndings("\n").Split('\n').ToList();

        var firstContentLineIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                firstContentLineIndex = i;
                break;
            }
        }

        if (firstContentLineIndex < 0)
        {
            return false;
        }

        var firstLine = lines[firstContentLineIndex].Trim();
        if (!TryParseLayoutLine(firstLine, out layoutTemplateRelativePath))
        {
            return false;
        }

        lines.RemoveAt(firstContentLineIndex);
        bodyTemplateText = string.Join('\n', lines);
        return true;
    }

    private static bool TryParseLayoutLine(string line, out string layoutTemplateRelativePath)
    {
        layoutTemplateRelativePath = string.Empty;

        if (TryParseDirective("{%", "%}", line, out var inner) || TryParseDirective("{{", "}}", line, out inner))
        {
            if (!inner.StartsWith("layout", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rest = inner["layout".Length..].Trim();
            if (TryExtractQuotedString(rest, out var path))
            {
                layoutTemplateRelativePath = path;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseDirective(string open, string close, string line, out string inner)
    {
        inner = string.Empty;
        if (!line.StartsWith(open, StringComparison.Ordinal) || !line.EndsWith(close, StringComparison.Ordinal))
        {
            return false;
        }

        inner = line.Substring(open.Length, line.Length - open.Length - close.Length).Trim();
        return true;
    }

    private static bool TryExtractQuotedString(string text, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var q1 = text.IndexOf('"');
        var q2 = q1 < 0 ? text.IndexOf('\'') : -1;
        var quote = q1 >= 0 ? '"' : q2 >= 0 ? '\'' : '\0';
        var start = q1 >= 0 ? q1 : q2;
        if (start < 0)
        {
            return false;
        }

        var end = text.IndexOf(quote, start + 1);
        if (end <= start)
        {
            return false;
        }

        value = text.Substring(start + 1, end - start - 1);
        return !string.IsNullOrWhiteSpace(value);
    }

    private readonly record struct FileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedTemplate(FileSignature Signature, Template Template, string? LayoutTemplateRelativePath);
}
