using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Bukit.Shared.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Content.Markdown;

public sealed record MarkdownFolderProviderOptions(
    string ContentDir,
    string DefaultType = "",
    int? MaxItems = null,
    IReadOnlyList<string>? IncludePaths = null,
    IReadOnlyList<string>? IncludeGlobs = null,
    bool AutoSummary = false,
    int AutoSummaryMaxLength = 200
)
{
    internal IReadOnlyList<string>? Languages { get; init; }
}

public sealed class MarkdownFolderProvider : IContentProvider
{
    private readonly MarkdownFolderProviderOptions _options;
    private readonly ISafeSourceFileOpener _opener;
    private readonly ILogger _logger;

    public MarkdownFolderProvider(MarkdownFolderProviderOptions options)
        : this(options, new ConsoleLogger(LogLevel.Warn))
    {
    }

    internal MarkdownFolderProvider(MarkdownFolderProviderOptions options, ILogger logger)
        : this(options, new PlatformSafeSourceFileOpener(), logger)
    {
    }

    internal MarkdownFolderProvider(
        MarkdownFolderProviderOptions options,
        ISafeSourceFileOpener opener,
        ILogger logger)
    {
        _options = options;
        _opener = opener;
        _logger = logger;
    }

    /// <summary>
    /// Read-only disk identity used by build source/write overlap checks.
    /// </summary>
    internal string ContentDirectory => Path.GetFullPath(_options.ContentDir);

    public async Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ContentDir))
        {
            throw new ContentException("ContentDir is required.");
        }

        if (!Directory.Exists(_options.ContentDir))
        {
            throw new ContentException($"ContentDir not found: {_options.ContentDir}");
        }

        var sourceRoot = Path.GetFullPath(_options.ContentDir);

        var files = SafeFileEnumerator.EnumerateFiles(sourceRoot, "*.md")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p, StringComparer.Ordinal)
            .ToArray();

        if (_options.IncludePaths is { Count: > 0 })
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in _options.IncludePaths)
            {
                if (string.IsNullOrWhiteSpace(p))
                {
                    continue;
                }

                var rel = p.Trim().Replace('/', Path.DirectorySeparatorChar);
                if (!rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    rel += ".md";
                }

                var full = Path.GetFullPath(Path.Combine(sourceRoot, rel));
                allowed.Add(full);
            }

            files = files.Where(f => allowed.Contains(Path.GetFullPath(f))).ToArray();
        }

        if (_options.IncludeGlobs is { Count: > 0 } globs)
        {
            var regexes = globs.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => BuildGlobRegex(x.Trim()))
                .ToList();

            if (regexes.Count > 0)
            {
                files = files.Where(f =>
                {
                    var rel = Path.GetRelativePath(sourceRoot, f).Replace('\\', '/');
                    return regexes.Any(r => r.IsMatch(rel));
                }).ToArray();
            }
        }

        if (_options.MaxItems is > 0)
        {
            files = files.Take(_options.MaxItems.Value).ToArray();
        }

        var languageDirectories = BuildLanguageDirectories(_options.Languages);
        var duplicateFileNames = files
            .GroupBy(file => Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var explicitSlugFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files.Where(file => duplicateFileNames.Contains(Path.GetFileNameWithoutExtension(file))))
        {
            if (TryReadExplicitSlug(file, sourceRoot) is not null)
            {
                explicitSlugFiles.Add(file);
            }
        }

        var duplicateKeys = files
            .Where(file => !explicitSlugFiles.Contains(file))
            .Select(file =>
            {
                var relativePath = Path.GetRelativePath(sourceRoot, file);
                return new
                {
                    Variant = ResolveVariantDirectory(relativePath, languageDirectories),
                    Name = Path.GetFileNameWithoutExtension(file)
                };
            })
            .GroupBy(entry => $"{entry.Variant}\0{entry.Name}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = new List<RawContentDocument>(capacity: files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var verified = _opener.Open(file, sourceRoot);
            string markdown;
            using (var reader = new StreamReader(
                       verified.Stream,
                       Encoding.UTF8,
                       detectEncodingFromByteOrderMarks: true,
                       bufferSize: 4096,
                       leaveOpen: true))
            {
                markdown = await reader.ReadToEndAsync(cancellationToken);
            }
            var slug = Path.GetFileNameWithoutExtension(file);

            var frontMatterValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = "markdown",
                ["sourcePath"] = file
            };

            var bodyMarkdown = markdown;
            var hasFrontMatter = MarkdownFrontMatterParser.TryExtractFrontMatter(
                markdown, out var frontMatterYaml, out var body);
            if (hasFrontMatter)
            {
                bodyMarkdown = body;
                IReadOnlyDictionary<string, object> fm;
                try
                {
                    fm = MarkdownFrontMatterParser.ParseFrontMatter(frontMatterYaml, _logger);
                }
                catch (ContentException ex)
                {
                    throw new ContentException($"Invalid front matter in '{file}': {ex.Message}", ex);
                }

                foreach (var kv in fm)
                {
                    frontMatterValues[kv.Key] = kv.Value;
                }
            }

            MarkdownFrontMatterParser.ThrowIfUnterminated(markdown, hasFrontMatter, file);

            // A filename is not a stable identity across nested content directories.
            // Keep the existing filename slug when it is unique, but use the relative
            // path only for colliding names in the same language variant so existing
            // sites keep their URLs and language directories keep their own identity.
            var relativePath = Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
            var variantDirectory = ResolveVariantDirectory(relativePath, languageDirectories);
            if (!frontMatterValues.ContainsKey("slug") &&
                duplicateKeys.Contains($"{variantDirectory}\0{slug}"))
            {
                var fallbackPath = variantDirectory.Length > 0 &&
                                   relativePath.StartsWith(variantDirectory + "/", StringComparison.OrdinalIgnoreCase)
                    ? relativePath[(variantDirectory.Length + 1)..]
                    : relativePath;
                slug = Path.ChangeExtension(fallbackPath, null)!
                    .TrimStart('.', '/');
                _logger.Info($"event=markdown.slug.collision_fallback file={file} slug={slug}");
            }

            if (!frontMatterValues.ContainsKey("type") &&
                !string.IsNullOrWhiteSpace(_options.DefaultType))
            {
                frontMatterValues["type"] = _options.DefaultType;
            }

            if (frontMatterValues.TryGetValue("slug", out var slugObj) && slugObj is string slugText && !string.IsNullOrWhiteSpace(slugText))
            {
                slug = slugText.Trim();
            }

            var title = frontMatterValues.TryGetValue("title", out var titleObj) && titleObj is string titleText && !string.IsNullOrWhiteSpace(titleText)
                ? titleText.Trim()
                : MarkdownTextHelper.ExtractTitle(bodyMarkdown) ?? slug;

            if (!frontMatterValues.TryGetValue("summary", out var summaryObj) || string.IsNullOrWhiteSpace(summaryObj?.ToString()))
            {
                if (_options.AutoSummary)
                {
                    var maxLen = _options.AutoSummaryMaxLength;
                    var extracted = ExtractSummaryFromMarkdown(bodyMarkdown, maxLen);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        frontMatterValues["summary"] = extracted;
                    }
                }
            }

            var tableOfContents = BasicMarkdownToHtml.ExtractTableOfContents(bodyMarkdown);
            if (tableOfContents.Count > 0)
            {
                frontMatterValues["tableOfContents"] = tableOfContents;
            }

            frontMatterValues["bodyFingerprint"] = MarkdownTextHelper.ComputeBodyFingerprint(markdown);

            DateTimeOffset? publishAt = null;
            if (frontMatterValues.TryGetValue("publishAt", out var publishObj) && publishObj is string publishText && MarkdownFieldBuilder.TryParseDateTimeOffset(publishText, out var dto))
            {
                publishAt = dto.ToUniversalTime();
            }

            var fields = MarkdownFieldBuilder.BuildFields(frontMatterValues);
            fields = ContentFieldReader.WithValues(fields, BuildCanonicalFields(frontMatterValues, title, slug, publishAt));

            items.Add(new RawContentDocument(
                SourceId: slug,
                SourceKind: "markdown",
                Title: title,
                Slug: slug,
                PublishedAt: publishAt,
                Body: new RawBody(BodyKey: file),
                Properties: RawContentValue.FromFields(fields),
                Source: new ContentSourceInfo("markdown", SourcePath: file),
                CustomFields: fields
            ));
        }

        return new RawContentLoadResult(items, new MarkdownBodyStore(sourceRoot, _opener));
    }

    private static IReadOnlyDictionary<string, object> BuildCanonicalFields(
        IReadOnlyDictionary<string, object> frontMatterValues,
        string title,
        string slug,
        DateTimeOffset? publishAt)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = title,
            ["slug"] = slug
        };

        if (publishAt is not null)
        {
            values["publishAt"] = publishAt.Value;
        }

        AddIfPresent(frontMatterValues, values, "type");
        AddIfPresent(frontMatterValues, values, "collection");
        AddIfPresent(frontMatterValues, values, "language");
        AddIfPresent(frontMatterValues, values, "i18nKey");
        AddIfPresent(frontMatterValues, values, "i18n_key");
        AddIfPresent(frontMatterValues, values, "summary");
        AddIfPresent(frontMatterValues, values, "description");
        AddIfPresent(frontMatterValues, values, "excerpt");
        AddIfPresent(frontMatterValues, values, "draft");
        AddIfPresent(frontMatterValues, values, "route");
        AddIfPresent(frontMatterValues, values, "url");
        AddIfPresent(frontMatterValues, values, "outputPath");
        AddIfPresent(frontMatterValues, values, "template");
        AddIfPresent(frontMatterValues, values, "source");
        AddIfPresent(frontMatterValues, values, "sourcePath");
        AddIfPresent(frontMatterValues, values, "bodyFingerprint");
        AddIfPresent(frontMatterValues, values, "tableOfContents");

        return values;
    }

    private static void AddIfPresent(
        IReadOnlyDictionary<string, object> source,
        IDictionary<string, object> target,
        string key)
    {
        if (source.TryGetValue(key, out var value) && value is not null)
        {
            target[key] = value;
        }
    }

    private string? TryReadExplicitSlug(string file, string sourceRoot)
    {
        using var verified = _opener.Open(file, sourceRoot);
        using var reader = new StreamReader(
            verified.Stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);
        var markdown = reader.ReadToEnd();
        if (!MarkdownFrontMatterParser.TryExtractFrontMatter(markdown, out var frontMatterYaml, out _))
        {
            return null;
        }

        var frontMatter = MarkdownFrontMatterParser.ParseFrontMatter(frontMatterYaml, _logger);
        return frontMatter.TryGetValue("slug", out var value) &&
               value is string slug &&
               !string.IsNullOrWhiteSpace(slug)
            ? slug.Trim()
            : null;
    }

    private static IReadOnlyList<string> BuildLanguageDirectories(IReadOnlyList<string>? languages)
        => languages?
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private static string ResolveVariantDirectory(string relativePath, IReadOnlyList<string> languages)
    {
        var normalized = relativePath.Replace('\\', '/');
        var separator = normalized.IndexOf('/');
        if (separator <= 0)
        {
            return string.Empty;
        }

        var firstSegment = normalized[..separator];
        return languages.Any(language =>
            string.Equals(firstSegment, language, StringComparison.OrdinalIgnoreCase) ||
            language.StartsWith(firstSegment + "-", StringComparison.OrdinalIgnoreCase))
            ? firstSegment
            : string.Empty;
    }

    private static Regex BuildGlobRegex(string glob)
    {
        var pattern = glob.Replace('\\', '/');
        var sb = new StringBuilder(pattern.Length * 2);
        sb.Append("^");

        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];
            if (ch == '*')
            {
                var isDouble = i + 1 < pattern.Length && pattern[i + 1] == '*';
                if (isDouble)
                {
                    // A trailing "**/" also matches the content root (zero directories).
                    if (i + 2 < pattern.Length && pattern[i + 2] == '/')
                    {
                        sb.Append("(?:.*/)?");
                        i += 2;
                    }
                    else
                    {
                        sb.Append(".*");
                        i++;
                    }
                }
                else
                {
                    sb.Append("[^/]*");
                }
                continue;
            }

            if (ch == '?')
            {
                sb.Append("[^/]");
                continue;
            }

            if ("+()^$.{}[]|\\".IndexOf(ch) >= 0)
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        sb.Append("$");
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }


    internal static string ExtractSummaryFromMarkdown(string markdown, int maxLength)
        => MarkdownTextHelper.ExtractSummaryFromMarkdown(markdown, maxLength);

    internal static async Task<string> RenderHtmlFromFileAsync(string filePath, CancellationToken cancellationToken)
        => await MarkdownTextHelper.RenderHtmlFromFileAsync(filePath, cancellationToken);
}
