using System.Text.Json;

namespace Bukit.Engine.PublishAuditRules;

internal static class RepresentationAuditRules
{
    internal static void Analyze(PublishDocument document, string outputDir, List<SeoAuditIssue> issues)
    {
        var requiredKinds = PublishRepresentationRegistry.DocumentKinds();
        if (requiredKinds.Any(kind => !document.RepresentationKinds.Contains(kind, StringComparer.OrdinalIgnoreCase)))
        {
            issues.Add(new SeoAuditIssue("error", "publish.representation_missing", document.RouteUrl, "Published content is missing one or more required representations (html/semantic-html/json/markdown)."));
        }

        if (document.ContentRecord is null)
        {
            return;
        }

        AnalyzeProjectionFile(document, outputDir, "json", ".json", issues);
        AnalyzeProjectionFile(document, outputDir, "markdown", ".md", issues);
        AnalyzeJsonProjection(document, outputDir, issues);
        AnalyzeMarkdownProjection(document, outputDir, issues);
        AnalyzeAgentManifest(document, outputDir, issues);
    }

    private static void AnalyzeProjectionFile(
        PublishDocument document,
        string outputDir,
        string kind,
        string extension,
        List<SeoAuditIssue> issues)
    {
        if (!document.RepresentationKinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var paths = BuildProjectionPathCandidates(document, outputDir, extension);
        if (paths.Any(File.Exists))
        {
            return;
        }

        var relativePath = Path.GetRelativePath(outputDir, paths[0]).Replace(Path.DirectorySeparatorChar, '/');
        issues.Add(new SeoAuditIssue("error", "publish.representation_file_missing", document.RouteUrl, $"Published content declares {kind} representation but the file is missing: {relativePath}."));
    }

    private static IReadOnlyList<string> BuildProjectionPathCandidates(PublishDocument document, string outputDir, string extension)
    {
        var record = document.ContentRecord!;
        var paths = new List<string>
        {
            DefaultContentProjectionWriter.GetContentProjectionBasePath(outputDir, record) + extension
        };

        var normalizedOutputPath = document.OutputPath.Replace('\\', '/');
        var slash = normalizedOutputPath.IndexOf('/', StringComparison.Ordinal);
        if (slash > 0)
        {
            var firstSegment = normalizedOutputPath[..slash];
            if (!string.Equals(firstSegment, "content", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(DefaultContentProjectionWriter.GetContentProjectionBasePath(Path.Combine(outputDir, firstSegment), record) + extension);
            }
        }

        return paths;
    }

    private static void AnalyzeJsonProjection(PublishDocument document, string outputDir, List<SeoAuditIssue> issues)
    {
        var path = FindProjectionPath(document, outputDir, ".json");
        if (path is null)
        {
            return;
        }

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var root = json.RootElement;
            var mismatch = !RouteEquals(ReadString(root, "route"), document) ||
                           !StringEquals(ReadString(root, "language"), document.Language) ||
                           !StringEquals(ReadString(root, "reviewStatus"), document.ReviewStatus) ||
                           !StringEquals(ReadString(root, "source"), document.Source) ||
                           !ContainsEntities(root, document.EntityNames);
            if (mismatch)
            {
                issues.Add(new SeoAuditIssue("error", "publish.representation_json_mismatch", document.RouteUrl, "JSON content representation does not match the publish document identity, language, trust, provenance, or entities."));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new SeoAuditIssue("error", "publish.representation_json_invalid", document.RouteUrl, $"JSON content representation is invalid JSON: {ex.Message}"));
        }
    }

    private static void AnalyzeMarkdownProjection(PublishDocument document, string outputDir, List<SeoAuditIssue> issues)
    {
        var path = FindProjectionPath(document, outputDir, ".md");
        if (path is null)
        {
            return;
        }

        var markdown = File.ReadAllText(path);
        var route = ReadMarkdownValue(markdown, "- Route:");
        var mismatch = !RouteEquals(route, document) ||
                       (document.Language is not null && !markdown.Contains($"- Language: {document.Language}", StringComparison.Ordinal)) ||
                       (document.ReviewStatus is not null && !markdown.Contains($"- Review Status: {document.ReviewStatus}", StringComparison.Ordinal)) ||
                       (document.Source is not null && !markdown.Contains($"- Source: {document.Source}", StringComparison.Ordinal));
        if (mismatch)
        {
            issues.Add(new SeoAuditIssue("error", "publish.representation_markdown_mismatch", document.RouteUrl, "Markdown content representation does not match the publish document route, language, review status, or provenance."));
        }
    }

    private static void AnalyzeAgentManifest(PublishDocument document, string outputDir, List<SeoAuditIssue> issues)
    {
        if (!document.Indexable)
        {
            return;
        }

        var path = Path.Combine(outputDir, "agent-manifest.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            if (!json.RootElement.TryGetProperty("documents", out var documents) ||
                documents.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new SeoAuditIssue("error", "publish.manifest_invalid", document.RouteUrl, "Agent manifest is missing a documents array."));
                return;
            }

            JsonElement? manifestDocument = null;
            foreach (var item in documents.EnumerateArray())
            {
                if (RouteEquals(ReadString(item, "route"), document))
                {
                    manifestDocument = item;
                    break;
                }
            }

            if (manifestDocument is null)
            {
                foreach (var item in documents.EnumerateArray())
                {
                if (StringEquals(ReadString(item, "id"), document.SourceItemId) ||
                    StringEquals(ReadString(item, "canonicalId"), document.ContentRecord?.Identity.CanonicalUrlKey))
                {
                    if (ManifestLanguageMatches(item, document))
                    {
                        manifestDocument = item;
                        break;
                    }
                }
            }
            }

            if (manifestDocument is null)
            {
                return;
            }

            var value = manifestDocument.Value;
            var mismatch = !RouteEquals(ReadString(value, "route"), document) ||
                           !StringEquals(ReadString(value, "language"), document.Language) ||
                           !StringEquals(ReadString(value, "reviewStatus"), document.ReviewStatus) ||
                           !StringEquals(ReadString(value, "source"), document.Source) ||
                           !ContainsManifestEntities(value, document.EntityNames);
            if (mismatch)
            {
                issues.Add(new SeoAuditIssue("error", "publish.manifest_mismatch", document.RouteUrl, BuildManifestMismatchMessage(value, document)));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new SeoAuditIssue("error", "publish.manifest_invalid", document.RouteUrl, $"Agent manifest is invalid JSON: {ex.Message}"));
        }
    }

    private static string? FindProjectionPath(PublishDocument document, string outputDir, string extension)
        => BuildProjectionPathCandidates(document, outputDir, extension).FirstOrDefault(File.Exists);

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static bool StringEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return true;
        }

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ManifestLanguageMatches(JsonElement element, PublishDocument document)
    {
        var language = ReadString(element, "language");
        if (string.IsNullOrWhiteSpace(document.Language))
        {
            return true;
        }

        return string.Equals(language, document.Language, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildManifestMismatchMessage(JsonElement value, PublishDocument document)
        => "Agent manifest document does not match the publish document identity, language, trust, provenance, or entities. " +
           $"Expected route={document.RouteUrl}, language={document.Language ?? "-"}, reviewStatus={document.ReviewStatus ?? "-"}, source={document.Source ?? "-"}; " +
           $"actual route={ReadString(value, "route") ?? "-"}, language={ReadString(value, "language") ?? "-"}, reviewStatus={ReadString(value, "reviewStatus") ?? "-"}, source={ReadString(value, "source") ?? "-"}.";

    private static bool RouteEquals(string? actual, PublishDocument document)
    {
        if (StringEquals(actual, document.RouteUrl))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        var normalizedOutputPath = document.OutputPath.Replace('\\', '/');
        var slash = normalizedOutputPath.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0)
        {
            return false;
        }

        var firstSegment = normalizedOutputPath[..slash];
        if (string.Equals(firstSegment, "content", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedActual = NormalizeRoute(actual);
        var prefixedActual = NormalizeRoute("/" + firstSegment.Trim('/') + (normalizedActual == "/" ? "/" : normalizedActual));
        return string.Equals(prefixedActual, NormalizeRoute(document.RouteUrl), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoute(string route)
    {
        var trimmed = route.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "/")
        {
            return "/";
        }

        return "/" + trimmed.Trim('/') + "/";
    }

    private static string? ReadMarkdownValue(string markdown, string prefix)
    {
        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return null;
    }

    private static bool ContainsEntities(JsonElement root, IReadOnlyList<string> expectedEntities)
    {
        if (expectedEntities.Count == 0)
        {
            return true;
        }

        if (!root.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var names = entities.EnumerateArray()
            .Select(entity => ReadString(entity, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return expectedEntities.All(entity => names.Contains(entity));
    }

    private static bool ContainsManifestEntities(JsonElement root, IReadOnlyList<string> expectedEntities)
    {
        if (expectedEntities.Count == 0)
        {
            return true;
        }

        if (!root.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var names = entities.EnumerateArray()
            .Where(entity => entity.ValueKind == JsonValueKind.String)
            .Select(entity => entity.GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return expectedEntities.All(entity => names.Contains(entity));
    }
}
