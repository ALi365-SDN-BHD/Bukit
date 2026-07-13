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
        IReadOnlyList<BuildVariantResult> variants)
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

            var pathLeak = ContainsSensitiveToken(relativePath, sensitiveTokens);
            var contentLeak = false;
            if (s_textExtensions.Contains(Path.GetExtension(path)))
            {
                var text = File.ReadAllText(path);
                contentLeak = ContainsSensitiveToken(text, sensitiveTokens) || ContainsProviderMarker(text, Path.GetExtension(path));
            }

            if (pathLeak || contentLeak)
            {
                errors.Add($"BKT-BUILD-SECURITY-PRIVACY-0001: public output contains internal Notion provenance at '{RedactPath(relativePath, sensitiveTokens)}'.");
            }
        }

        return new PublicOutputPrivacyCheckResult(errors.Count == 0 ? "passed" : "failed", errors);
    }

    private static IReadOnlyList<ContentRecord> CollectNotionRecords(IReadOnlyList<BuildVariantResult> variants)
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

    private static bool ContainsSensitiveToken(string value, IReadOnlySet<string> sensitiveTokens)
        => sensitiveTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsProviderMarker(string value, string extension)
    {
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jsonld", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(value);
                if (ContainsJsonProviderMarker(document.RootElement))
                {
                    return true;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall through to the text checks for incomplete generated output.
            }
        }

        return JsonProviderMarkerRegex().IsMatch(value) || LineProviderMarkerRegex().IsMatch(value);
    }

    private static bool ContainsJsonProviderMarker(System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.Equals("source", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("sourceKey", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == System.Text.Json.JsonValueKind.String &&
                    string.Equals(property.Value.GetString(), "notion", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (ContainsJsonProviderMarker(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsJsonProviderMarker(item))
                {
                    return true;
                }
            }
        }

        return false;
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
