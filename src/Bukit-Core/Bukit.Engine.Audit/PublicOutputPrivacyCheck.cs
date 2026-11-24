using System.Buffers;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static partial class PublicOutputPrivacyCheck
{
    private static readonly SearchValues<char> s_hostNameChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-.");
    private static readonly SearchValues<char> s_hexDigits = SearchValues.Create("0123456789abcdefABCDEF");
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
        var cloudBaseSources = config.Content.Sources?
            .Where(source => string.Equals(source.Type, "cloudbase", StringComparison.OrdinalIgnoreCase) || source.CloudBase is not null)
            .ToArray() ?? [];
        if (notionSources.Length == 0 && notionRecords.Count == 0 && cloudBaseSources.Length == 0)
        {
            return new PublicOutputPrivacyCheckResult("not_applicable", []);
        }

        var notionTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in notionSources)
        {
            AddIdentifierTokens(notionTokens, source.Notion?.DatabaseId);
        }

        var cloudBaseTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCloudBaseTokens(cloudBaseTokens, cloudBaseSources);

        foreach (var record in notionRecords)
        {
            AddIdentifierTokens(notionTokens, record.Identity.Id);
            AddIdentifierTokens(notionTokens, record.Identity.CanonicalUrlKey);
            foreach (var entity in record.Entities)
            {
                AddIdentifierTokens(notionTokens, entity.Name);
                AddIdentifierTokens(notionTokens, entity.Id);
                AddIdentifierTokens(notionTokens, entity.Url);
                foreach (var sameAs in entity.SameAs ?? [])
                {
                    AddIdentifierTokens(notionTokens, sameAs);
                }
            }

            foreach (var relation in record.Relations)
            {
                AddIdentifierTokens(notionTokens, relation.Target);
                AddIdentifierTokens(notionTokens, relation.TargetId);
            }
        }

        if (!Directory.Exists(outputDir))
        {
            return new PublicOutputPrivacyCheckResult("passed", []);
        }

        var allTokens = new HashSet<string>(notionTokens, StringComparer.OrdinalIgnoreCase);
        allTokens.UnionWith(cloudBaseTokens);
        var matcher = NotionIdentifierMatcher.From(allTokens);
        var notionMatcher = NotionIdentifierMatcher.From(notionTokens);
        var cloudBaseMatcher = NotionIdentifierMatcher.From(cloudBaseTokens);
        var redactionTokens = allTokens;

        var errors = new List<string>();
        var reportDirectories = BuildReportPaths.ReportDirectories(config.Site);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(outputDir, "*", options))
        {
            var relativePath = NormalizePath(Path.GetRelativePath(outputDir, path));
            if (IsInternalBuildArtifact(relativePath, reportDirectories))
            {
                continue;
            }

            var pathSource = SourceFor(relativePath, notionMatcher, cloudBaseMatcher);
            var contentScan = s_textExtensions.Contains(Path.GetExtension(path))
                ? ScanFileForLeaks(path, matcher, notionMatcher, cloudBaseMatcher)
                : new FileScanResult(FilePrivacyScan.Clean, PrivacySource.None);

            if (pathSource != PrivacySource.None || contentScan.Status == FilePrivacyScan.Leak)
            {
                var source = pathSource | contentScan.Source;
                errors.Add($"BKT-BUILD-SECURITY-PRIVACY-0001: public output contains {ProvenanceLabel(source)} at '{RedactPath(relativePath, redactionTokens)}'.");
            }
            else if (contentScan.Status == FilePrivacyScan.Incomplete)
            {
                // Do not claim a confirmed leak; the scan could not prove the output clean.
                errors.Add($"BKT-BUILD-SECURITY-PRIVACY-0001: unable to complete the content provenance privacy scan for public output at '{RedactPath(relativePath, redactionTokens)}' because the JSON is malformed, too deep, or contains invalid Unicode.");
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

    private static void AddCloudBaseTokens(HashSet<string> sensitiveTokens, IReadOnlyList<ContentSourceConfig> sources)
    {
        foreach (var source in sources)
        {
            var cloudBase = source.CloudBase;
            if (string.IsNullOrWhiteSpace(cloudBase?.EnvId))
            {
                continue;
            }

            sensitiveTokens.Add("cloud://");
            sensitiveTokens.Add(cloudBase.EnvId + ".api.tcloudbasegateway.com");
            sensitiveTokens.Add(cloudBase.EnvId + ".api.intl.tcloudbasegateway.com");
            sensitiveTokens.Add(cloudBase.EnvId + ".storage.tcloudbase.com");
            sensitiveTokens.Add(".storage.tcloudbase.com");
        }
    }

    /// <summary>
    /// Finds <paramref name="token"/> in <paramref name="text"/>, ignoring case. A host-name token
    /// (only letters, digits, <c>-</c> and <c>.</c>, such as <c>prod-1.api.tcloudbasegateway.com</c>)
    /// matches only at a host-label boundary, so another environment's host (<c>xprod-1.api…</c>)
    /// is not a match; a percent (<c>%2F</c>) or backslash (<c>\n</c>, <c>\x2F</c>, <c>\u002F</c>) escape
    /// before it counts as a boundary. Any other token (such as <c>cloud://</c>) matches anywhere.
    /// </summary>
    internal static bool ContainsHostToken(string text, string token)
    {
        if (token.Length == 0)
        {
            return false;
        }

        if (!char.IsAsciiLetterOrDigit(token[0]) || token.AsSpan().ContainsAnyExcept(s_hostNameChars))
        {
            return text.Contains(token, StringComparison.OrdinalIgnoreCase);
        }

        for (var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
             index >= 0;
             index = text.IndexOf(token, index + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (index == 0 || !IsHostLabelChar(text[index - 1]) || FollowsEscape(text, index))
            {
                return true;
            }
        }

        return false;

        static bool IsHostLabelChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '-';

        static bool FollowsEscape(string text, int index)
            => (index >= 2 && text[index - 2] == '\\' && text[index - 1] is 'n' or 'r' or 't' or 'b' or 'f') ||
               (index >= 3 && text[index - 3] == '%' && IsHex(text, index - 2, 2)) ||
               (index >= 4 && text[index - 4] == '\\' && text[index - 3] is 'x' or 'X' && IsHex(text, index - 2, 2)) ||
               (index >= 6 && text[index - 6] == '\\' && text[index - 5] is 'u' or 'U' && IsHex(text, index - 4, 4));

        static bool IsHex(string text, int start, int length)
            => !text.AsSpan(start, length).ContainsAnyExcept(s_hexDigits);
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
    /// Streams a file line-by-line, checking each line for sensitive tokens and provider
    /// markers. Returns the leak status and which provider matched, so the caller can name
    /// the right one. Falls back to a full JSON parse for .json/.jsonld.
    /// </summary>
    private static FileScanResult ScanFileForLeaks(
        string path,
        NotionIdentifierMatcher matcher,
        NotionIdentifierMatcher notionMatcher,
        NotionIdentifierMatcher cloudBaseMatcher)
    {
        var extension = Path.GetExtension(path);
        using var reader = new StreamReader(path);
        while (reader.ReadLine() is { } line)
        {
            if (matcher.ContainsAny(line))
            {
                return new FileScanResult(FilePrivacyScan.Leak, SourceFor(line, notionMatcher, cloudBaseMatcher));
            }

            if (LineProviderMarkerRegex().IsMatch(line) || JsonProviderMarkerRegex().IsMatch(line))
            {
                return new FileScanResult(FilePrivacyScan.Leak, PrivacySource.Notion);
            }

            if (CloudBaseSignedLinkRegex().IsMatch(line))
            {
                return new FileScanResult(FilePrivacyScan.Leak, PrivacySource.CloudBase);
            }
        }

        // JSON fallback: parse full document to catch Unicode-escaped values and names
        // (e.g. "not\u0069on") that streaming regex cannot detect.
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jsonld", StringComparison.OrdinalIgnoreCase))
        {
            var json = File.ReadAllText(path);
            if (ScanJson(json, matcher) == JsonPrivacyScan.Leak)
            {
                var source = PrivacySource.None;
                if (ScanJson(json, notionMatcher) == JsonPrivacyScan.Leak)
                {
                    source |= PrivacySource.Notion;
                }

                if (ScanJson(json, cloudBaseMatcher) == JsonPrivacyScan.Leak)
                {
                    source |= PrivacySource.CloudBase;
                }

                return new FileScanResult(FilePrivacyScan.Leak, source);
            }

            return ScanJson(json, matcher) == JsonPrivacyScan.Incomplete
                ? new FileScanResult(FilePrivacyScan.Incomplete, PrivacySource.None)
                : new FileScanResult(FilePrivacyScan.Clean, PrivacySource.None);
        }

        return new FileScanResult(FilePrivacyScan.Clean, PrivacySource.None);
    }

    private static PrivacySource SourceFor(
        string value,
        NotionIdentifierMatcher notionMatcher,
        NotionIdentifierMatcher cloudBaseMatcher)
    {
        var source = PrivacySource.None;
        if (notionMatcher.ContainsAny(value))
        {
            source |= PrivacySource.Notion;
        }

        if (cloudBaseMatcher.ContainsAny(value))
        {
            source |= PrivacySource.CloudBase;
        }

        return source;
    }

    private static string ProvenanceLabel(PrivacySource source)
        => source switch
        {
            PrivacySource.CloudBase => "internal CloudBase provenance",
            PrivacySource.Notion => "internal Notion provenance",
            _ => "internal content provenance"
        };

    [Flags]
    private enum PrivacySource
    {
        None = 0,
        Notion = 1,
        CloudBase = 2
    }

    private readonly record struct FileScanResult(FilePrivacyScan Status, PrivacySource Source);

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
                if (ContainsHostToken(value, token))
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

    private static bool IsInternalBuildArtifact(string relativePath, IReadOnlyList<string> reportDirectories)
        => relativePath.Equals(".bukit-build-state.json", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Equals(".bukit-output-marker", StringComparison.OrdinalIgnoreCase) ||
           BuildReportPaths.IsInReportDirectory(relativePath, reportDirectories);

    private static string NormalizePath(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    [GeneratedRegex("(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant)]
    private static partial Regex NotionIdentifierRegex();

    [GeneratedRegex("(?i)\"(?:source|sourceKey)\"\\s*:\\s*\"notion\"", RegexOptions.CultureInvariant)]
    private static partial Regex JsonProviderMarkerRegex();

    [GeneratedRegex(@"(?i)(?:/v1/storages/object/sign/[^\s""'<>]*[?&]token=|\.storage\.tcloudbase\.com/[^\s""'<>]*[?&]token=)", RegexOptions.CultureInvariant)]
    private static partial Regex CloudBaseSignedLinkRegex();

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
