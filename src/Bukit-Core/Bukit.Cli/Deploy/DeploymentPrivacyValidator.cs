using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.CloudBase.Transport;
using Bukit.Engine;

namespace Bukit.Cli.Deploy;

internal static partial class DeploymentPrivacyValidator
{
    private const string PublishAuditSchema = "https://bukit.dev/schemas/publish-audit-report.v1.json";
    private static readonly HashSet<string> s_textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".csv", ".htm", ".html", ".js", ".json", ".jsonld", ".md", ".mjs",
        ".rss", ".svg", ".txt", ".xml", ".yaml", ".yml"
    };

    internal static IReadOnlyList<string> Validate(string outputDir, string stagedDir, IReadOnlyList<string>? cloudBaseEnvIds = null)
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

            var notionLeak = ContainsSensitiveToken(relativePath, sensitiveTokens);
            var cloudBaseLeak = false;
            var contentScan = FilePrivacyScan.Clean;
            if (s_textExtensions.Contains(Path.GetExtension(path)))
            {
                var text = File.ReadAllText(path);
                if (ContainsSensitiveToken(text, sensitiveTokens))
                {
                    notionLeak = true;
                }
                else if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                         Path.GetExtension(path).Equals(".jsonld", StringComparison.OrdinalIgnoreCase))
                {
                    contentScan = PublicOutputPrivacyCheck.ScanJson(text, sensitiveTokens) switch
                    {
                        JsonPrivacyScan.Leak => FilePrivacyScan.Leak,
                        JsonPrivacyScan.Incomplete => FilePrivacyScan.Incomplete,
                        _ => FilePrivacyScan.Clean
                    };
                }
                else if (JsonProviderMarkerRegex().IsMatch(text) || LineProviderMarkerRegex().IsMatch(text))
                {
                    notionLeak = true;
                }

                // JSON that cannot be fully decoded is reported as an incomplete scan (below) rather
                // than as a confirmed cloud:// leak; either way the deploy fails.
                cloudBaseLeak =
                    HostSignedUrlRegex().IsMatch(text) ||
                    ContainsCloudBaseIdentifier(text, cloudBaseEnvIds) ||
                    (contentScan != FilePrivacyScan.Incomplete &&
                     CloudBaseDeployUrlScanner.ContainsCloudReference(text, Path.GetExtension(path)));
            }

            if (notionLeak || contentScan == FilePrivacyScan.Leak || cloudBaseLeak)
            {
                var label = cloudBaseLeak && notionLeak
                    ? "internal content provenance"
                    : cloudBaseLeak ? "internal CloudBase provenance" : "internal Notion provenance";
                errors.Add($"BKT-DEPLOY-PRIVACY-0001: staged public output contains {label} at '{RedactPath(relativePath, sensitiveTokens)}'.");
            }
            else if (contentScan == FilePrivacyScan.Incomplete)
            {
                var scanLabel = PublicOutputPrivacyCheck.IncompleteScanLabel(cloudBaseEnvIds is { Count: > 0 });
                errors.Add($"BKT-DEPLOY-PRIVACY-0001: unable to complete the {scanLabel} privacy scan for staged public output at '{RedactPath(relativePath, sensitiveTokens)}' because the JSON is malformed, too deep, or contains invalid Unicode; rebuild after fixing it.");
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

    private static string RedactPath(string relativePath, IReadOnlySet<string> sensitiveTokens)
    {
        var redacted = relativePath;
        foreach (var token in sensitiveTokens.OrderByDescending(static token => token.Length))
        {
            redacted = redacted.Replace(token, "[redacted-notion-id]", StringComparison.OrdinalIgnoreCase);
        }

        return NotionIdentifierRegex().Replace(redacted, "[redacted-notion-id]");
    }

    private static bool ContainsCloudBaseIdentifier(string text, IReadOnlyList<string>? envIds)
    {
        if (envIds is null)
        {
            return false;
        }

        foreach (var envId in envIds)
        {
            if (string.IsNullOrWhiteSpace(envId))
            {
                continue;
            }

            // Host forms match at a label boundary only: prod-1 must not match xprod-1's host.
            if (PublicOutputPrivacyCheck.ContainsHostToken(text, envId + CloudBaseEndpoints.GatewaySuffix) ||
                PublicOutputPrivacyCheck.ContainsHostToken(text, envId + CloudBaseEndpoints.IntlGatewaySuffix) ||
                PublicOutputPrivacyCheck.ContainsHostToken(text, CloudBaseEndpoints.StorageCdnHost(envId)) ||
                text.Contains($"cloud://{envId}.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    [GeneratedRegex("(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])", RegexOptions.CultureInvariant)]
    private static partial Regex NotionIdentifierRegex();

    [GeneratedRegex("(?i)\"(?:source|sourceKey)\"\\s*:\\s*\"notion\"", RegexOptions.CultureInvariant)]
    private static partial Regex JsonProviderMarkerRegex();

    [GeneratedRegex("(?im)^\\s*(?:[-*]\\s*)?(?:source|sourceKey)\\s*:\\s*[\"']?notion[\"']?\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex LineProviderMarkerRegex();

    /// <summary>
    /// A gateway-host URL on the signed path, or a storage CDN host URL, with a <c>token</c>
    /// parameter, in any <c>//</c> URL form (https, http, protocol-relative or another scheme). Slashes
    /// may be JSON-escaped (<c>\/</c>), the host may carry a port, and the <c>&amp;</c> before
    /// <c>token</c> may be written <c>\u0026</c> or as an HTML entity (<c>&amp;amp;</c>, <c>&amp;#38;</c>).
    /// The URL ends at whitespace, a quote or an angle bracket, raw or JSON-escaped, so a
    /// <c>token</c> outside the URL does not count.
    /// </summary>
    [GeneratedRegex(@"(?i)(?:\\?/){2}[a-z0-9.-]+\.(?:api(?:\.intl)?\.tcloudbasegateway\.com(?::\d{1,5})?\\?/v1\\?/storages\\?/object\\?/sign\\?/|storage\.tcloudbase\.com(?::\d{1,5})?\\?/)(?:[^\s""'<>\\]|\\/|\\u(?!0022|0027|003[ce])[0-9a-f]{4})*(?:[?&]|\\u0026|&(?:amp|#0*38|#x0*26);)token=", RegexOptions.CultureInvariant)]
    private static partial Regex HostSignedUrlRegex();

    private sealed record DeploymentIdentityTokens(bool ReportValid, HashSet<string> Tokens);
}
