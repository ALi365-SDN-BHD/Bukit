using Bukit.Config;
using Bukit.Engine.Primitives;
using Bukit.Engine.Plugins.BuiltIn.ImageProcessing;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// The effective shared output set: configured prefixes minus the media prefix.
/// Created once per site build and passed through the variant contexts.
/// </summary>
internal sealed class SharedAssetScope
{
    private readonly IReadOnlyList<string> _includes;
    private readonly IReadOnlyList<string> _excludes;
    private readonly StringComparison _comparison;

    private SharedAssetScope(
        IReadOnlyList<string> includes,
        IReadOnlyList<string> excludes,
        IReadOnlyList<string> canonicalPrefixes,
        string outputRoot,
        string rootBaseUrl,
        StringComparer comparer)
    {
        _includes = includes;
        _excludes = excludes;
        CanonicalPrefixes = canonicalPrefixes;
        OutputRoot = outputRoot;
        RootBaseUrl = rootBaseUrl;
        Comparer = comparer;
        _comparison = comparer.Equals(StringComparer.OrdinalIgnoreCase)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    internal IReadOnlyList<string> CanonicalPrefixes { get; }
    internal string OutputRoot { get; }
    internal string RootBaseUrl { get; }
    internal StringComparer Comparer { get; }

    internal static SharedAssetScope? TryCreate(
        AppConfig config,
        IReadOnlyList<string> languages,
        string outputRoot,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(languages);

        if (!config.Site.SharedAssets.Enabled ||
            languages.Count < 2 ||
            config.Site.DefaultLanguageInSubdir)
        {
            return null;
        }

        var output = Path.GetFullPath(outputRoot);
        var comparer = OutputDestinationIdentityComparer.ForOutputRoot(output);
        var includes = ReadIncludes(config, logger, comparer);
        var excludes = ReadExcludes(config, comparer);
        var canonical = includes
            .Concat(excludes)
            .Distinct(comparer)
            .OrderBy(prefix => prefix, StringComparer.Ordinal)
            .ToArray();

        return new SharedAssetScope(
            includes,
            excludes,
            canonical,
            output,
            BuildPathUtils.NormalizeBaseUrl(config.Site.BaseUrl),
            comparer);
    }

    internal bool Contains(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalized = SharedAssetsPath.Normalize(relativePath);
        return normalized.Length > 0 &&
               _includes.Any(prefix => normalized.StartsWith(prefix, _comparison)) &&
               !_excludes.Any(prefix => normalized.StartsWith(prefix, _comparison));
    }

    internal bool ContainsSitePath(string sitePath)
    {
        if (string.IsNullOrWhiteSpace(sitePath))
        {
            return false;
        }

        var path = sitePath.Trim();
        var suffixAt = path.IndexOfAny(['?', '#']);
        if (suffixAt >= 0)
        {
            path = path[..suffixAt];
        }

        return Contains(path);
    }

    private static List<string> ReadIncludes(
        AppConfig config,
        ILogger? logger,
        StringComparer comparer)
    {
        var includes = new List<string>();
        if (config.Site.SharedAssets.Paths is { Count: > 0 } paths)
        {
            foreach (var path in paths)
            {
                AddPrefix(includes, path, comparer);
            }
        }
        else
        {
            AddPrefix(includes, "assets/", comparer);
            if (config.Theme.Scss is { Enabled: true } scss)
            {
                var normalized = SharedAssetsPath.Normalize(scss.OutputDir);
                if (SharedAssetsPath.IsRoot(normalized))
                {
                    logger?.Warn(
                        $"event=i18n.config.shared_assets_path_ignored path={scss.OutputDir} reason=root");
                }
                else
                {
                    AddPrefix(includes, normalized, comparer);
                }
            }
        }

        return includes;
    }

    private static List<string> ReadExcludes(AppConfig config, StringComparer comparer)
    {
        var excludes = new List<string>();
        var media = SharedAssetsPath.Normalize(
            ResponsiveImageHtmlTransform.NormalizeMediaUrlBase(config.Content.Media.UrlBase));
        if (SharedAssetsPath.IsRoot(media))
        {
            excludes.Add(string.Empty);
        }
        else
        {
            AddPrefix(excludes, media, comparer);
        }

        return excludes;
    }

    private static void AddPrefix(List<string> prefixes, string? value, StringComparer comparer)
    {
        var normalized = SharedAssetsPath.Normalize(value);
        if (normalized.Length > 0 && !prefixes.Contains(normalized, comparer))
        {
            prefixes.Add(normalized);
        }
    }
}
