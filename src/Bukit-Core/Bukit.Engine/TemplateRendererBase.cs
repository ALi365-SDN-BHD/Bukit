using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Bukit.Rendering;
using Bukit.Rendering.Scriban;
using Bukit.Shared;
using Bukit.Shared.IO;

namespace Bukit.Engine;

/// <summary>
/// Abstract base for template renderers that handles template resolution,
/// file caching, layout nesting (up to <see cref="MaxLayoutDepth"/>),
/// and shortcode post-processing. Concrete subclasses implement
/// <see cref="ParseTemplateText"/> and <see cref="RenderTemplateCore"/>
/// for a specific template engine.
/// This base is Scriban-free — it only depends on <see cref="ITemplateRenderer"/>
/// and rendering models, making it straightforward to swap in a different
/// template engine.
/// </summary>
public abstract class TemplateRendererBase : ITemplateRenderer
{
    private const int MaxLayoutDepth = 10;

    protected string LayoutsDir { get; }
    protected string? ParentLayoutsDir { get; }
    protected string? UserLayoutsDir { get; }
    protected IReadOnlyDictionary<string, string>? Shortcodes { get; }
    internal IReadOnlyList<ITemplateContextContributor> ContextContributors { get; } = Array.Empty<ITemplateContextContributor>();

    private readonly ConcurrentDictionary<string, CachedTemplateInfo> _templateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISafeSourceFileOpener _templateOpener = new PlatformSafeSourceFileOpener();

    protected TemplateRendererBase(
        string layoutsDir,
        string? parentLayoutsDir = null,
        string? userLayoutsDir = null,
        IReadOnlyDictionary<string, string>? shortcodes = null)
    {
        LayoutsDir = layoutsDir;
        ParentLayoutsDir = parentLayoutsDir;
        UserLayoutsDir = userLayoutsDir;
        Shortcodes = shortcodes;
    }

    public abstract string RenderPage(string templateRelativePath, PageModel model);
    public abstract string RenderList(string templateRelativePath, ListPageModel model);

    protected abstract object ParseTemplateText(string templateText, string templatePath, string templateRelativePath);
    protected abstract string RenderTemplateCore(object parsedTemplate, string templateRelativePath, object modelData);
    protected abstract string ResolveTemplatePath(string templateRelativePath);
    protected abstract void SetContent(object modelData, string content);

    protected string RenderWithLayout(string templateRelativePath, object modelData, int depth = 0)
    {
        if (depth >= MaxLayoutDepth)
        {
            throw new RenderException(
                $"Layout nesting depth exceeded maximum of {MaxLayoutDepth}.",
                DiagnosticCode.RenderLayoutNestingExceeded);
        }

        var cached = GetOrCacheTemplate(templateRelativePath);
        if (cached.LayoutTemplateRelativePath is not null)
        {
            var body = RenderTemplateCore(cached.ParsedTemplate, templateRelativePath, modelData);
            SetContent(modelData, body);
            return RenderWithLayout(cached.LayoutTemplateRelativePath, modelData, depth + 1);
        }

        var result = RenderTemplateCore(cached.ParsedTemplate, templateRelativePath, modelData);
        if (Shortcodes is { Count: > 0 })
            result = ShortcodeProcessor.RenderShortcodes(result, Shortcodes);
        return result;
    }

    private CachedTemplateInfo GetOrCacheTemplate(string templateRelativePath)
    {
        var templatePath = ResolveTemplatePath(templateRelativePath);
        var fileInfo = new FileInfo(templatePath);
        if (!fileInfo.Exists)
            throw new RenderException($"Template not found: {templateRelativePath}", DiagnosticCode.RenderTemplateNotFound);

        var templateRoot = ResolveContainingLayoutsRoot(templatePath);
        using var verified = _templateOpener.Open(templatePath, templateRoot);
        string templateText;
        using (var reader = new StreamReader(
                   verified.Stream,
                   Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: true,
                   bufferSize: 4096,
                   leaveOpen: true))
        {
            templateText = reader.ReadToEnd();
        }

        var contentHash = ComputeContentHash(templateText);
        var lastWrite = verified.LastWriteTimeUtc;
        var length = verified.Length;
        if (_templateCache.TryGetValue(templatePath, out var existing) &&
            existing.LastWriteUtc == lastWrite && existing.Length == length && existing.ContentHash == contentHash)
            return existing;

        var (bodyText, layoutPath) = ExtractLayoutDirective(templateText);
        var parsed = ParseTemplateText(bodyText, templatePath, templateRelativePath);
        var cached = new CachedTemplateInfo(lastWrite, length, contentHash, parsed, layoutPath);
        _templateCache[templatePath] = cached;
        return cached;
    }

    private string ResolveContainingLayoutsRoot(string templatePath)
    {
        var fullPath = Path.GetFullPath(templatePath);
        foreach (var candidate in new[] { UserLayoutsDir, LayoutsDir, ParentLayoutsDir })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var root = Path.GetFullPath(candidate);
            var relative = Path.GetRelativePath(root, fullPath);
            if (relative == "." ||
                (!Path.IsPathRooted(relative) &&
                 !relative.Equals("..", StringComparison.Ordinal) &&
                 !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                 !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)))
            {
                return root;
            }
        }

        throw new RenderException(
            $"Template path is outside the configured layouts directories: {templatePath}",
            DiagnosticCode.RenderTemplateNotFound);
    }

    protected virtual (string BodyTemplateText, string? LayoutTemplateRelativePath) ExtractLayoutDirective(string templateText)
        => (templateText, null);

    private static long ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return BitConverter.ToInt64(hash, 0);
    }

    private sealed record CachedTemplateInfo(DateTime LastWriteUtc, long Length, long ContentHash, object ParsedTemplate, string? LayoutTemplateRelativePath);
}
