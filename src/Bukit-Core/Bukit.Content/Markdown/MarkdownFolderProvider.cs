using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using System.Security.Cryptography;
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
);

public sealed class MarkdownFolderProvider : IContentProvider
{
    private readonly MarkdownFolderProviderOptions _options;

    public MarkdownFolderProvider(MarkdownFolderProviderOptions options)
    {
        _options = options;
    }

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

        var files = SafeFileEnumerator.EnumerateFiles(_options.ContentDir, "*.md")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
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

                var full = Path.GetFullPath(Path.Combine(_options.ContentDir, rel));
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
                    var rel = Path.GetRelativePath(_options.ContentDir, f).Replace('\\', '/');
                    return regexes.Any(r => r.IsMatch(rel));
                }).ToArray();
            }
        }

        if (_options.MaxItems is > 0)
        {
            files = files.Take(_options.MaxItems.Value).ToArray();
        }

        var items = new List<RawContentDocument>(capacity: files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var markdown = await File.ReadAllTextAsync(file, cancellationToken);
            var slug = Path.GetFileNameWithoutExtension(file);

            var frontMatterValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["source"] = "markdown",
                ["sourcePath"] = file
            };

            var bodyMarkdown = markdown;
            if (MarkdownFrontMatterParser.TryExtractFrontMatter(markdown, out var frontMatterYaml, out var body))
            {
                bodyMarkdown = body;
                var fm = MarkdownFrontMatterParser.ParseFrontMatter(frontMatterYaml);
                foreach (var kv in fm)
                {
                    frontMatterValues[kv.Key] = kv.Value;
                }
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

            frontMatterValues["bodyFingerprint"] = ComputeBodyFingerprint(bodyMarkdown);

            var publishAt = File.GetLastWriteTimeUtc(file);
            if (frontMatterValues.TryGetValue("publishAt", out var publishObj) && publishObj is string publishText && MarkdownFieldBuilder.TryParseDateTimeOffset(publishText, out var dto))
            {
                publishAt = dto.UtcDateTime;
            }

            var fields = MarkdownFieldBuilder.BuildFields(frontMatterValues);
            fields = ContentFieldReader.WithValues(fields, BuildCanonicalFields(frontMatterValues, title, slug, publishAt));

            items.Add(new RawContentDocument(
                Id: slug,
                Title: title,
                Slug: slug,
                PublishAt: publishAt,
                Body: new RawBody(BodyKey: file),
                Properties: RawContentValue.FromFields(fields),
                Source: new ContentSourceInfo("markdown", SourcePath: file),
                CustomFields: fields
            ));
        }

        return new RawContentLoadResult(items, new MarkdownBodyStore());
    }

    private static string ComputeBodyFingerprint(string markdown)
    {
        var bytes = Encoding.UTF8.GetBytes(markdown ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, object> BuildCanonicalFields(
        IReadOnlyDictionary<string, object> frontMatterValues,
        string title,
        string slug,
        DateTimeOffset publishAt)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = title,
            ["slug"] = slug,
            ["publishAt"] = publishAt
        };

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
                    sb.Append(".*");
                    i++;
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
