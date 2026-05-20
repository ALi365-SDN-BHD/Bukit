using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using System.Collections.Concurrent;

namespace Bukit.Rendering.Scriban;

public sealed class FileTemplateLoader : ITemplateLoader
{
    private readonly string _rootDir;
    private readonly ConcurrentDictionary<string, CachedText> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileTemplateLoader(string rootDir)
    {
        _rootDir = rootDir;
    }

    public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        string resolved;
        if (Path.IsPathRooted(templateName))
        {
            resolved = Path.GetFullPath(templateName);
        }
        else
        {
            var normalized = templateName.Replace('/', Path.DirectorySeparatorChar);
            resolved = Path.GetFullPath(Path.Combine(_rootDir, normalized));
        }

        var safeRoot = Path.GetFullPath(_rootDir) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Template include '{templateName}' resolves outside the layouts directory.");
        }

        return resolved;
    }

    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        return LoadCached(EnsurePathInsideRoot(templatePath));
    }

    public async ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        templatePath = EnsurePathInsideRoot(templatePath);
        var fileInfo = new FileInfo(templatePath);
        if (!fileInfo.Exists)
        {
            return string.Empty;
        }

        var signature = new FileSignature(fileInfo.LastWriteTimeUtc, fileInfo.Length);
        if (_cache.TryGetValue(templatePath, out var existing) && existing.Signature.Equals(signature))
        {
            return existing.Text;
        }

        var text = await File.ReadAllTextAsync(templatePath);
        _cache[templatePath] = new CachedText(signature, text);
        return text;
    }

    private string EnsurePathInsideRoot(string templatePath)
    {
        var fullPath = Path.GetFullPath(templatePath);
        var safeRoot = Path.GetFullPath(_rootDir) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Template path '{templatePath}' resolves outside the layouts directory.");
        }

        return fullPath;
    }

    private string LoadCached(string templatePath)
    {
        var fileInfo = new FileInfo(templatePath);
        if (!fileInfo.Exists)
        {
            return string.Empty;
        }

        var signature = new FileSignature(fileInfo.LastWriteTimeUtc, fileInfo.Length);
        if (_cache.TryGetValue(templatePath, out var existing) && existing.Signature.Equals(signature))
        {
            return existing.Text;
        }

        var text = File.ReadAllText(templatePath);
        _cache[templatePath] = new CachedText(signature, text);
        return text;
    }

    private readonly record struct FileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedText(FileSignature Signature, string Text);
}
