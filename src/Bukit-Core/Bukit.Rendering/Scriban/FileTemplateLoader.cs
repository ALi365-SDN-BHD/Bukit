using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using System.Collections.Concurrent;

namespace Bukit.Rendering.Scriban;

public sealed class FileTemplateLoader : ITemplateLoader
{
    private readonly string? _overrideDir;
    private readonly string _rootDir;
    private readonly string? _fallbackDir;
    private readonly ConcurrentDictionary<string, CachedText> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FileTemplateLoader(string rootDir, string? fallbackDir = null, string? overrideDir = null)
    {
        _overrideDir = overrideDir;
        _rootDir = rootDir;
        _fallbackDir = fallbackDir;
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

            if (_overrideDir is not null)
            {
                var overridePath = Path.GetFullPath(Path.Combine(_overrideDir, normalized));
                if (File.Exists(overridePath))
                {
                    resolved = overridePath;
                    goto safetyCheck;
                }
            }

            var primary = Path.GetFullPath(Path.Combine(_rootDir, normalized));
            if (File.Exists(primary))
            {
                resolved = primary;
                goto safetyCheck;
            }

            if (_fallbackDir is not null)
            {
                var fallback = Path.GetFullPath(Path.Combine(_fallbackDir, normalized));
                if (File.Exists(fallback))
                {
                    resolved = fallback;
                    goto safetyCheck;
                }
            }

            resolved = primary;
        }

    safetyCheck:
        if (_overrideDir is not null)
        {
            var safeOverride = EnsureSafeRoot(_overrideDir);
            if (resolved.StartsWith(safeOverride, StringComparison.OrdinalIgnoreCase))
            {
                return resolved;
            }
        }

        var safeRootPrimary = EnsureSafeRoot(_rootDir);
        if (!resolved.StartsWith(safeRootPrimary, StringComparison.OrdinalIgnoreCase) &&
            (_fallbackDir is null || !resolved.StartsWith(EnsureSafeRoot(_fallbackDir), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Template include '{templateName}' resolves outside the layouts directory.");
        }

        return resolved;
    }

    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        return LoadCached(EnsurePathInsideAnyRoot(templatePath));
    }

    public async ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        templatePath = EnsurePathInsideAnyRoot(templatePath);
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

    private string EnsurePathInsideAnyRoot(string templatePath)
    {
        var fullPath = Path.GetFullPath(templatePath);

        if (_overrideDir is not null)
        {
            var safeOverride = EnsureSafeRoot(_overrideDir);
            if (fullPath.StartsWith(safeOverride, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }
        }

        var safeRoot = EnsureSafeRoot(_rootDir);
        if (fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        if (_fallbackDir is not null)
        {
            var safeFallback = EnsureSafeRoot(_fallbackDir);
            if (fullPath.StartsWith(safeFallback, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }
        }

        throw new InvalidOperationException(
            $"Template path '{templatePath}' resolves outside the layouts directory.");
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

    private static string EnsureSafeRoot(string dir)
        => Path.GetFullPath(dir) + Path.DirectorySeparatorChar;

    private readonly record struct FileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedText(FileSignature Signature, string Text);
}
