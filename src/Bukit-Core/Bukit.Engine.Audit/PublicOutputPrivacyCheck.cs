using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static partial class PublicOutputPrivacyCheck
{
    private static readonly HashSet<string> s_textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".csv", ".htm", ".html", ".js", ".json", ".jsonld", ".md", ".mjs",
        ".rss", ".svg", ".txt", ".xml", ".yaml", ".yml"
    };

    internal static PublicOutputPrivacyCheckResult Evaluate(
        AppConfig config,
        string outputDir,
        IReadOnlyList<PublishedVariant> variants)
    {
        var notionRecords = CollectNotionRecords(variants);
        var notionSources = config.Content.Sources?
            .Where(source => string.Equals(source.Type, "notion", StringComparison.OrdinalIgnoreCase) || source.Notion is not null)
            .ToArray() ?? [];
        if (notionSources.Length == 0 && notionRecords.Count == 0)
        {
            return new PublicOutputPrivacyCheckResult("not_applicable", []);
        }

        var sensitiveTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in notionSources)
        {
            AddIdentifierTokens(sensitiveTokens, source.Notion?.DatabaseId);
        }

        foreach (var record in notionRecords)
        {
            AddIdentifierTokens(sensitiveTokens, record.Identity.Id);
            AddIdentifierTokens(sensitiveTokens, record.Identity.CanonicalUrlKey);
            foreach (var entity in record.Entities)
            {
                AddIdentifierTokens(sensitiveTokens, entity.Name);
                AddIdentifierTokens(sensitiveTokens, entity.Id);
                AddIdentifierTokens(sensitiveTokens, entity.Url);
                foreach (var sameAs in entity.SameAs ?? [])
                {
                    AddIdentifierTokens(sensitiveTokens, sameAs);
                }
            }

            foreach (var relation in record.Relations)
            {
                AddIdentifierTokens(sensitiveTokens, relation.Target);
                AddIdentifierTokens(sensitiveTokens, relation.TargetId);
            }
        }

        if (!Directory.Exists(outputDir))
        {
            return new PublicOutputPrivacyCheckResult("passed", []);
        }

        var matcher = NotionIdentifierMatcher.From(sensitiveTokens);

        var errors = new List<string>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(outputDir, "*", options))
        {
            var relativePath = NormalizePath(Path.GetRelativePath(outputDir, path));
            if (IsInternalBuildArtifact(relativePath))
            {
                continue;
            }

            var pathLeak = matcher.ContainsAny(relativePath);
            var contentScan = FilePrivacyScan.Clean;
            if (s_textExtensions.Contains(Path.GetExtension(path)))
            {
                contentScan = ScanFileForLeaks(path, matcher);
            }

            if (pathLeak || contentScan == FilePrivacyScan.Leak)
            {
                errors.Add($"BKT-BUILD-SECURITY-PRIVACY-0001: public output contains internal Notion provenance at '{RedactPath(relativePath, sensitiveTokens)}'.");
            }
            else if (contentScan == FilePrivacyScan.Incomplete)
            {
                // Do not claim a confirmed leak; the scan could not prove the output clean.
                errors.Add($"BKT-BUILD-SECURITY-PRIVACY-0001: unable to complete the Notion provenance privacy scan for public output at '{RedactPath(relativePath, sensitiveTokens)}' because the JSON is malformed, too deep, or contains invalid Unicode.");
            }
        }

        return new PublicOutputPrivacyCheckResult(errors.Count == 0 ? "passed" : "failed", errors);
    }

    private static IReadOnlyList<ContentRecord> CollectNotionRecords(IReadOnlyList<PublishedVariant> variants)
    {
        var records = variants
            .SelectMany(variant => (variant.ContentGraph ?? CanonicalContentGraph.Empty).Records
                .Concat(variant.RoutedDocuments.Select(document => document.Document.Record))
                .Concat(variant.DerivedDocuments.Select(document => document.Document.Record)))
            .Where(record => string.Equals(record.Provenance.Source, "notion", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(record => record.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return records;
    }

    private static void AddIdentifierTokens(HashSet<string> tokens, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var matches = NotionIdentifierRegex().Matches(value);
        foreach (Match match in matches)
        {
            var compact = match.Value.Replace("-", string.Empty, StringComparison.Ordinal);
            tokens.Add(match.Value);
            tokens.Add(compact);
            if (Guid.TryParse(compact, out var guid))
            {
                tokens.Add(guid.ToString("D"));
            }
        }

        if (matches.Count > 0)
        {
            tokens.Add(value.Trim());
        }
    }

    /// <summary>
    /// Streams a file line-by-line, checking each line for sensitive tokens
    /// and provider markers. Returns <c>true</c> as soon as a leak is found
    /// (early exit), avoiding full-file reads on large outputs.
    /// Falls back to full JSON parse for .json/.jsonld to catch Unicode escapes.
    /// </summary>
    private static FilePrivacyScan ScanFileForLeaks(string path, NotionIdentifierMatcher matcher)
    {
        var extension = Path.GetExtension(path);
        using var reader = new StreamReader(path);
        while (reader.ReadLine() is { } line)
        {
            if (matcher.ContainsAny(line))
            {
                return FilePrivacyScan.Leak;
            }

            if (LineProviderMarkerRegex().IsMatch(line) || JsonProviderMarkerRegex().IsMatch(line))
            {
                return FilePrivacyScan.Leak;
            }
        }

        // JSON fallback: parse full document to catch Unicode-escaped values and names
        // (e.g. "not\u0069on") that streaming regex cannot detect.
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jsonld", StringComparison.OrdinalIgnoreCase))
        {
            return ScanJson(File.ReadAllText(path), matcher) switch
            {
                JsonPrivacyScan.Leak => FilePrivacyScan.Leak,
                JsonPrivacyScan.Incomplete => FilePrivacyScan.Incomplete,
                _ => FilePrivacyScan.Clean
            };
        }

        return FilePrivacyScan.Clean;
    }

    /// <summary>
    /// Shared JSON privacy scan for build and deployment checks. Parses once, then checks
    /// decoded string values and property names against the caller's tokens. Parse errors,
    /// depth exhaustion and string-decode failures finish as <see cref="JsonPrivacyScan.Incomplete"/>
    /// instead of silently passing.
    /// </summary>
    internal static JsonPrivacyScan ScanJson(string json, IReadOnlySet<string> sensitiveTokens)
        => ScanJson(json, NotionIdentifierMatcher.From(sensitiveTokens));

    private static JsonPrivacyScan ScanJson(string json, NotionIdentifierMatcher sensitiveTokens)
    {
        System.Text.Json.JsonDocument document;
        try
        {
            document = System.Text.Json.JsonDocument.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return JsonPrivacyScan.Incomplete;
        }

        using (document)
        {
            try
            {
                return ContainsJsonLeak(document.RootElement, sensitiveTokens)
                    ? JsonPrivacyScan.Leak
                    : JsonPrivacyScan.Clean;
            }
            catch (JsonStringDecodeException)
            {
                // Only the narrow GetString()/JsonProperty.Name decode boundary is
                // classified as incomplete; other InvalidOperationException paths keep
                // their original failure behavior.
                return JsonPrivacyScan.Incomplete;
            }
        }
    }

    private static bool ContainsJsonLeak(System.Text.Json.JsonElement element, NotionIdentifierMatcher sensitiveTokens)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                string propertyName;
                try
                {
                    propertyName = property.Name;
                }
                catch (InvalidOperationException)
                {
                    throw new JsonStringDecodeException();
                }

                if (sensitiveTokens.ContainsAny(propertyName))
                {
                    return true;
                }

                if ((propertyName.Equals("source", StringComparison.OrdinalIgnoreCase) ||
                     propertyName.Equals("sourceKey", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == System.Text.Json.JsonValueKind.String &&
                    string.Equals(ReadJsonString(property.Value), "notion", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (ContainsJsonLeak(property.Value, sensitiveTokens))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsJsonLeak(item, sensitiveTokens))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return sensitiveTokens.ContainsAny(ReadJsonString(element) ?? string.Empty);
        }

        return false;
    }

    private static string? ReadJsonString(System.Text.Json.JsonElement element)
    {
        try
        {
            return element.GetString();
        }
        catch (InvalidOperationException)
        {
            throw new JsonStringDecodeException();
        }
    }

    private sealed class JsonStringDecodeException : Exception
    {
    }

    /// <summary>
    /// Finds Notion identifiers in one pass. Every sensitive token is (or contains) a Notion
    /// id in dashed or compact form, so scanning hex/dash runs and checking each 32-char
    /// compact window covers both forms. Cost is linear in the text, not text x tokens.
    /// </summary>
    internal sealed class NotionIdentifierMatcher
    {
        private const int IdLength = 32;
        private readonly HashSet<string> _compactIds;
        private readonly string[] _otherTokens;

        private NotionIdentifierMatcher(HashSet<string> compactIds, string[] otherTokens)
        {
            _compactIds = compactIds;
            _otherTokens = otherTokens;
        }

        internal static NotionIdentifierMatcher From(IReadOnlySet<string> tokens)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var others = new List<string>();
            foreach (var token in tokens)
            {
                var found = false;
                foreach (Match match in NotionIdentifierRegex().Matches(token))
                {
                    ids.Add(match.Value.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant());
                    found = true;
                }

                // Callers only pass id-bearing tokens; keep any other token on the slow path.
                if (!found && token.Length > 0)
                {
                    others.Add(token);
                }
            }

            return new NotionIdentifierMatcher(ids, [.. others]);
        }

        internal bool ContainsAny(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (_compactIds.Count > 0 && ContainsId(value))
            {
                return true;
            }

            foreach (var token in _otherTokens)
            {
                if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsId(string value)
        {
            Span<char> window = stackalloc char[IdLength];
            var buffer = new System.Text.StringBuilder();
            for (var i = 0; i <= value.Length; i++)
            {
                var c = i < value.Length ? value[i] : '\0';
                if (char.IsAsciiHexDigit(c))
                {
                    buffer.Append(char.ToLowerInvariant(c));
                    continue;
                }

                if (c == '-' && buffer.Length > 0)
                {
                    continue;
                }

                if (buffer.Length >= IdLength && HasKnownWindow(buffer, window))
                {
                    return true;
                }

                buffer.Clear();
            }

            return false;
        }

        private bool HasKnownWindow(System.Text.StringBuilder run, Span<char> window)
        {
            var lookup = _compactIds.GetAlternateLookup<ReadOnlySpan<char>>();
            for (var start = 0; start + IdLength <= run.Length; start++)
            {
                run.CopyTo(start, window, IdLength);
                if (lookup.Contains(window))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static string RedactPath(string relativePath, IReadOnlySet<string> sensitiveTokens)
    {
        var redacted = relativePath;
        foreach (var token in sensitiveTokens.OrderByDescending(static token => token.Length))
        {
            redacted = redacted.Replace(token, "[redacted-notion-id]", StringComparison.OrdinalIgnoreCase);
        }

        return NotionIdentifierRegex().Replace(redacted, "[redacted-notion-id]");
    }

    private static bool IsInternalBuildArtifact(string relativePath)
    {
        if (relativePath.Equals(".bukit-build-state.json", StringComparison.OrdinalIgnoreCase) ||
            relativePath.Equals(".bukit-output-marker", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return relativePath.Equals(".bukit", StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith(".bukit/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    [GeneratedRegex("(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant)]
    private static partial Regex NotionIdentifierRegex();

    [GeneratedRegex("(?i)\"(?:source|sourceKey)\"\\s*:\\s*\"notion\"", RegexOptions.CultureInvariant)]
    private static partial Regex JsonProviderMarkerRegex();

    [GeneratedRegex("(?im)^\\s*(?:[-*]\\s*)?(?:source|sourceKey)\\s*:\\s*[\"']?notion[\"']?\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex LineProviderMarkerRegex();
}

internal sealed record PublicOutputPrivacyCheckResult(
    string Status,
    IReadOnlyList<string> Errors);

internal enum JsonPrivacyScan
{
    Clean,
    Leak,
    Incomplete
}

internal enum FilePrivacyScan
{
    Clean,
    Leak,
    Incomplete
}
