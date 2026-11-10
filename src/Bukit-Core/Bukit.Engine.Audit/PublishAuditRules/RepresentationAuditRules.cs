using System.Text.Json;

namespace Bukit.Engine.PublishAuditRules;

internal static class RepresentationAuditRules
{
    internal static void Analyze(PublishDocument document, string outputDir, List<PublishAuditIssue> issues)
    {
        using var agentManifest = new AgentManifestSnapshot(outputDir);
        Analyze(document, outputDir, issues, agentManifest);
    }

    /// <param name="agentManifest">Shared by every document of one audit run, so the manifest is parsed once.</param>
    internal static void Analyze(PublishDocument document, string outputDir, List<PublishAuditIssue> issues, AgentManifestSnapshot agentManifest)
    {
        var requiredKinds = PublishRepresentationRegistry.DocumentKinds();
        if (requiredKinds.Any(kind => !document.RepresentationKinds.Contains(kind, StringComparer.OrdinalIgnoreCase)))
        {
            issues.Add(new PublishAuditIssue("error", "publish.representation_missing", document.RouteUrl, "Published content is missing one or more required representations (html/semantic-html/json/markdown)."));
        }

        if (document.ContentRecord is null)
        {
            return;
        }

        AnalyzeProjectionFile(document, outputDir, "json", ".json", issues);
        AnalyzeProjectionFile(document, outputDir, "markdown", ".md", issues);
        AnalyzeJsonProjection(document, outputDir, issues);
        AnalyzeMarkdownProjection(document, outputDir, issues);
        AnalyzeAgentManifest(document, agentManifest, issues);
    }

    private static void AnalyzeProjectionFile(
        PublishDocument document,
        string outputDir,
        string kind,
        string extension,
        List<PublishAuditIssue> issues)
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
        issues.Add(new PublishAuditIssue("error", "publish.representation_file_missing", document.RouteUrl, $"Published content declares {kind} representation but the file is missing: {relativePath}."));
    }

    private static IReadOnlyList<string> BuildProjectionPathCandidates(PublishDocument document, string outputDir, string extension)
    {
        var kind = extension == ".json" ? "json" : "markdown";
        var actual = document.ProjectionOutputs.FirstOrDefault(x => x.Kind == kind);
        var relative = actual?.Path ?? ContentProjectionPaths.GetContentProjectionRelativePath(
            new Engine.Abstractions.Routing.RouteInfo(document.RouteUrl, document.OutputPath, string.Empty), extension);
        return [FileWriter.GetSafeFullPath(outputDir, relative)];
    }

    private static void AnalyzeJsonProjection(PublishDocument document, string outputDir, List<PublishAuditIssue> issues)
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
            var publicId = document.ContentRecord is null
                ? null
                : PublicContentProjectionPolicy.ResolvePublicId(document.ContentRecord, document.RouteUrl);
            var publicEntities = document.ContentRecord is null
                ? document.EntityNames
                : PublicContentProjectionPolicy.SanitizeEntities(document.ContentRecord).Select(entity => entity.Name).ToArray();
            var mismatch = !RouteEquals(ReadString(root, "route"), document) ||
                           (publicId is not null && !StringEquals(ReadString(root, "id"), publicId)) ||
                           (publicId is not null && !StringEquals(ReadString(root, "canonicalUrlKey"), publicId)) ||
                           !StringEquals(ReadString(root, "language"), document.Language) ||
                           !StringEquals(ReadString(root, "reviewStatus"), document.ReviewStatus) ||
                           root.TryGetProperty("source", out _) ||
                           !ContainsEntities(root, publicEntities);
            if (mismatch)
            {
                issues.Add(new PublishAuditIssue("error", "publish.representation_json_mismatch", document.RouteUrl, "JSON content representation does not match the publish document identity, language, trust, provenance, or entities."));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new PublishAuditIssue("error", "publish.representation_json_invalid", document.RouteUrl, $"JSON content representation is invalid JSON: {ex.Message}"));
        }
    }

    private static void AnalyzeMarkdownProjection(PublishDocument document, string outputDir, List<PublishAuditIssue> issues)
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
                       markdown.Contains("- Source:", StringComparison.Ordinal);
        if (mismatch)
        {
            issues.Add(new PublishAuditIssue("error", "publish.representation_markdown_mismatch", document.RouteUrl, "Markdown content representation does not match the publish document route, language, review status, or provenance."));
        }
    }

    private static void AnalyzeAgentManifest(PublishDocument document, AgentManifestSnapshot manifest, List<PublishAuditIssue> issues)
    {
        if (!document.Indexable || !manifest.Exists)
        {
            return;
        }

        if (manifest.InvalidJsonMessage is not null)
        {
            issues.Add(new PublishAuditIssue("error", "publish.manifest_invalid", document.RouteUrl, $"Agent manifest is invalid JSON: {manifest.InvalidJsonMessage}"));
            return;
        }

        var documents = manifest.Documents;
        if (documents is null)
        {
            issues.Add(new PublishAuditIssue("error", "publish.manifest_invalid", document.RouteUrl, "Agent manifest is missing a documents array."));
            return;
        }

        JsonElement? manifestDocument = null;
        for (var i = 0; i < documents.Length; i++)
        {
            if (RouteEquals(manifest.Route(i), document))
            {
                manifestDocument = documents[i];
                break;
            }
        }

        if (manifestDocument is null)
        {
            for (var i = 0; i < documents.Length; i++)
            {
                if (StringEquals(manifest.Id(i), document.SourceItemId) ||
                    StringEquals(manifest.CanonicalId(i), document.ContentRecord?.Identity.CanonicalUrlKey))
                {
                    if (ManifestLanguageMatches(manifest.Language(i), document))
                    {
                        manifestDocument = documents[i];
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
        var publicId = document.ContentRecord is null
            ? null
            : PublicContentProjectionPolicy.ResolvePublicId(document.ContentRecord, document.RouteUrl);
        var publicEntities = document.ContentRecord is null
            ? document.EntityNames
            : PublicContentProjectionPolicy.SanitizeEntities(document.ContentRecord).Select(entity => entity.Name).ToArray();
        var mismatch = !RouteEquals(ReadString(value, "route"), document) ||
                       (publicId is not null && !StringEquals(ReadString(value, "id"), publicId)) ||
                       (publicId is not null && !StringEquals(ReadString(value, "canonicalId"), publicId)) ||
                       !StringEquals(ReadString(value, "language"), document.Language) ||
                       !StringEquals(ReadString(value, "reviewStatus"), document.ReviewStatus) ||
                       value.TryGetProperty("source", out _) ||
                       !ContainsManifestEntities(value, publicEntities);
        if (mismatch)
        {
            issues.Add(new PublishAuditIssue("error", "publish.manifest_mismatch", document.RouteUrl, BuildManifestMismatchMessage(value, document)));
        }
    }

    private static string? FindProjectionPath(PublishDocument document, string outputDir, string extension)
        => BuildProjectionPathCandidates(document, outputDir, extension).FirstOrDefault(File.Exists);

    internal static string? ReadString(JsonElement element, string property)
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

    private static bool ManifestLanguageMatches(string? language, PublishDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Language))
        {
            return true;
        }

        return string.Equals(language, document.Language, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildManifestMismatchMessage(JsonElement value, PublishDocument document)
        => "Agent manifest document does not match the publish-safe identity, language, trust, or entities. " +
           $"Expected route={document.RouteUrl}, language={document.Language ?? "-"}, reviewStatus={document.ReviewStatus ?? "-"}; " +
           $"actual route={ReadString(value, "route") ?? "-"}, language={ReadString(value, "language") ?? "-"}, reviewStatus={ReadString(value, "reviewStatus") ?? "-"}.";

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

/// <summary>
/// <c>agent-manifest.json</c> read and parsed once for a whole audit run. The file is loaded
/// on first use, and the strings the per-document lookup compares are read once per entry,
/// in the same order the per-document scan first needs them, so failures surface at the
/// same point as before.
/// </summary>
internal sealed class AgentManifestSnapshot(string outputDir) : IDisposable
{
    private bool _loaded;
    private bool _exists;
    private string? _invalidJsonMessage;
    private JsonDocument? _json;
    private JsonElement[]? _documents;
    private Field _route;
    private Field _id;
    private Field _canonicalId;
    private Field _language;

    internal bool Exists { get { Load(); return _exists; } }

    internal string? InvalidJsonMessage { get { Load(); return _invalidJsonMessage; } }

    /// <summary>The <c>documents</c> array entries, or null when the array is missing.</summary>
    internal JsonElement[]? Documents { get { Load(); return _documents; } }

    internal string? Route(int index) => _route.Get(_documents!, index, "route");

    internal string? Id(int index) => _id.Get(_documents!, index, "id");

    internal string? CanonicalId(int index) => _canonicalId.Get(_documents!, index, "canonicalId");

    internal string? Language(int index) => _language.Get(_documents!, index, "language");

    public void Dispose() => _json?.Dispose();

    private void Load()
    {
        if (_loaded)
        {
            return;
        }

        var path = Path.Combine(outputDir, "agent-manifest.json");
        _exists = File.Exists(path);
        if (_exists)
        {
            try
            {
                _json = JsonDocument.Parse(File.ReadAllText(path));
                if (_json.RootElement.TryGetProperty("documents", out var documents) &&
                    documents.ValueKind == JsonValueKind.Array)
                {
                    _documents = [.. documents.EnumerateArray()];
                }
            }
            catch (JsonException ex)
            {
                _invalidJsonMessage = ex.Message;
            }
        }

        _loaded = true;
    }

    private struct Field
    {
        private string?[]? _values;
        private bool[]? _read;

        internal string? Get(JsonElement[] documents, int index, string property)
        {
            _values ??= new string?[documents.Length];
            _read ??= new bool[documents.Length];
            if (!_read[index])
            {
                _values[index] = RepresentationAuditRules.ReadString(documents[index], property);
                _read[index] = true;
            }

            return _values[index];
        }
    }
}
