using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Deploy;

internal static partial class DeploymentPrivacyValidator
{
    private const string PublishAuditSchema = "https://bukit.dev/schemas/publish-audit-report.v1.json";
    private static readonly HashSet<string> s_textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".csv", ".htm", ".html", ".js", ".json", ".jsonld", ".md", ".mjs",
        ".rss", ".svg", ".txt", ".xml", ".yaml", ".yml"
    };

    internal static IReadOnlyList<string> Validate(string outputDir, string stagedDir)
    {
        var identity = CollectSensitiveTokens(outputDir);
        if (!identity.ReportValid)
        {
            return ["BKT-DEPLOY-PRIVACY-0002: required internal publish identity report is missing or invalid; rebuild before deployment."];
        }

        var sensitiveTokens = identity.Tokens;
        var errors = new List<string>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(stagedDir, "*", options))
        {
            var relativePath = NormalizePath(Path.GetRelativePath(stagedDir, path));
            if (relativePath.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith(".git/", StringComparison.OrdinalIgnoreCase))
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
                errors.Add($"BKT-DEPLOY-PRIVACY-0001: staged public output contains internal Notion provenance at '{RedactPath(relativePath, sensitiveTokens)}'.");
            }
        }

        return errors;
    }

    private static DeploymentIdentityTokens CollectSensitiveTokens(string outputDir)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(outputDir, ".bukit", "publish-audit-report.json");
        if (!File.Exists(path))
        {
            return new DeploymentIdentityTokens(false, tokens);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("schema", out var schema) ||
                schema.ValueKind != JsonValueKind.String ||
                !string.Equals(schema.GetString(), PublishAuditSchema, StringComparison.Ordinal) ||
                !document.RootElement.TryGetProperty("documents", out var documents) ||
                documents.ValueKind != JsonValueKind.Array)
            {
                return new DeploymentIdentityTokens(false, tokens);
            }

            CollectSensitiveTokens(document.RootElement, tokens);
            return new DeploymentIdentityTokens(true, tokens);
        }
        catch (JsonException)
        {
            return new DeploymentIdentityTokens(false, tokens);
        }
    }

    private static void CollectSensitiveTokens(JsonElement element, HashSet<string> tokens)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string? source = null;
            string? sourceItemId = null;
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals("source", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    source = property.Value.GetString();
                }
                else if (property.Name.Equals("sourceItemId", StringComparison.OrdinalIgnoreCase) &&
                         property.Value.ValueKind == JsonValueKind.String)
                {
                    sourceItemId = property.Value.GetString();
                }
            }

            if (string.Equals(source, "notion", StringComparison.OrdinalIgnoreCase))
            {
                AddIdentifierTokens(tokens, sourceItemId);
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectSensitiveTokens(property.Value, tokens);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectSensitiveTokens(item, tokens);
            }
        }
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
                using var document = JsonDocument.Parse(value);
                if (ContainsJsonProviderMarker(document.RootElement))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                // Fall through to text checks for incomplete generated output.
            }
        }

        return JsonProviderMarkerRegex().IsMatch(value) || LineProviderMarkerRegex().IsMatch(value);
    }

    private static bool ContainsJsonProviderMarker(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.Equals("source", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("sourceKey", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.String &&
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
        else if (element.ValueKind == JsonValueKind.Array)
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

    private static string NormalizePath(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    [GeneratedRegex("(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant)]
    private static partial Regex NotionIdentifierRegex();

    [GeneratedRegex("(?i)\"(?:source|sourceKey)\"\\s*:\\s*\"notion\"", RegexOptions.CultureInvariant)]
    private static partial Regex JsonProviderMarkerRegex();

    [GeneratedRegex("(?im)^\\s*(?:[-*]\\s*)?(?:source|sourceKey)\\s*:\\s*[\"']?notion[\"']?\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex LineProviderMarkerRegex();

    private sealed record DeploymentIdentityTokens(bool ReportValid, HashSet<string> Tokens);
}
