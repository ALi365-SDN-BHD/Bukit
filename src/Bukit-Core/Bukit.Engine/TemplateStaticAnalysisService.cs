using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class TemplateStaticAnalysisService
{
    private static readonly ConcurrentDictionary<string, TemplateStaticAnalysisResult> StaticCache = new(StringComparer.OrdinalIgnoreCase);

    internal static TemplateStaticAnalysisResult AnalyzeNeedsPageContent(string layoutsDir, string templateRelativePath)
    {
        var key = CacheKey(layoutsDir, templateRelativePath);
        if (StaticCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var analyzer = new Analyzer(layoutsDir);
        var result = analyzer.Analyze(templateRelativePath);
        StaticCache[key] = result;
        return result;
    }

    private static string CacheKey(string layoutsDir, string templateRelativePath)
        => $"{layoutsDir}\u0000{templateRelativePath.Replace('\\', '/')}";

    [GeneratedRegex(@"\binclude\s+[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IncludeRegex();

    [GeneratedRegex(@"\binclude\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IncludeTokenRegex();

    [GeneratedRegex(@"\.\s*content\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ContentMemberRegex();

    [GeneratedRegex(@"\{\{\*.*?\*\}\}", RegexOptions.Singleline)]
    private static partial Regex ScribanCommentRegex();

    private static string StripScribanComments(string text)
        => ScribanCommentRegex().Replace(text, string.Empty);

    private sealed class Analyzer
    {
        private readonly string _layoutsDir;
        private readonly Dictionary<string, TemplateStaticAnalysisResult> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _visiting = new(StringComparer.OrdinalIgnoreCase);

        public Analyzer(string layoutsDir)
        {
            _layoutsDir = layoutsDir;
        }

        public TemplateStaticAnalysisResult Analyze(string templateRelativePath)
        {
            var normalized = ScribanLayoutDirectiveParser.NormalizePath(templateRelativePath);
            if (_cache.TryGetValue(normalized, out var cached))
            {
                return cached;
            }

            if (!_visiting.Add(normalized))
            {
                return new TemplateStaticAnalysisResult(null, "cycle");
            }

            try
            {
                var fullPath = ResolveTemplatePath(normalized);
                if (!File.Exists(fullPath))
                {
                    return Cache(normalized, new TemplateStaticAnalysisResult(null, "missing_template"));
                }

                var text = File.ReadAllText(fullPath);
                text = StripScribanComments(text);
                if (ScribanLayoutDirectiveParser.TryExtractLayoutDirective(text, out var layoutPath, out var bodyText))
                {
                    var normalizedLayout = ScribanLayoutDirectiveParser.NormalizePath(layoutPath);
                    var layoutResult = Analyze(normalizedLayout);
                    if (layoutResult.NeedsPageContent.HasValue)
                    {
                        return Cache(normalized, layoutResult.NeedsPageContent.Value
                            ? new TemplateStaticAnalysisResult(true, "analysis")
                            : AnalyzeText(normalized, bodyText));
                    }

                    return Cache(normalized, layoutResult with { Source = layoutResult.Source });
                }

                return Cache(normalized, AnalyzeText(normalized, text));
            }
            catch
            {
                return Cache(normalized, new TemplateStaticAnalysisResult(null, "analysis_error"));
            }
            finally
            {
                _visiting.Remove(normalized);
            }
        }

        private TemplateStaticAnalysisResult AnalyzeText(string templateRelativePath, string text)
        {
            if (ContentMemberRegex().IsMatch(text))
            {
                return new TemplateStaticAnalysisResult(true, "analysis");
            }

            var includeMatches = IncludeRegex().Matches(text).Cast<Match>().ToList();
            var includeTokenCount = IncludeTokenRegex().Matches(text).Count;
            if (includeTokenCount > includeMatches.Count)
            {
                return new TemplateStaticAnalysisResult(null, "dynamic_include");
            }

            foreach (var match in includeMatches)
            {
                var includePath = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(includePath))
                {
                    continue;
                }

                var result = Analyze(includePath);
                if (!result.NeedsPageContent.HasValue)
                {
                    return result;
                }

                if (result.NeedsPageContent.Value)
                {
                    return new TemplateStaticAnalysisResult(true, "analysis");
                }
            }

            return new TemplateStaticAnalysisResult(false, "analysis");
        }

        private string ResolveTemplatePath(string templateRelativePath)
        {
            var resolved = Path.GetFullPath(Path.Combine(_layoutsDir, templateRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            var safeRoot = Path.GetFullPath(_layoutsDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Template resolves outside layouts.");
            }

            return resolved;
        }

        private TemplateStaticAnalysisResult Cache(string templateRelativePath, TemplateStaticAnalysisResult result)
        {
            _cache[templateRelativePath] = result;
            return result;
        }
    }

    internal sealed record TemplateStaticAnalysisResult(bool? NeedsPageContent, string Source);
}
