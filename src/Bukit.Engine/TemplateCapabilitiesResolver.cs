using System.Collections.Concurrent;
using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Engine;

internal static class TemplateCapabilitiesResolver
{
    private const string ManifestFileName = "bukit.templates.yaml";
    private static readonly ConcurrentDictionary<string, Task<TemplateCapabilitiesManifest?>> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> FallbackCache = new(StringComparer.OrdinalIgnoreCase);
    internal static void ValidateManifest(string layoutsDir)
    {
        var task = Cache.GetOrAdd(layoutsDir, static dir => LoadManifestAsync(dir));
        task.GetAwaiter().GetResult();
    }

    internal static bool ShouldIncludeListPageContent(string templateRelativePath, string layoutsDir, string mode)
        => ResolveListPageContent(templateRelativePath, layoutsDir, mode).IncludeContent;

    internal static ListPageContentResolution ResolveListPageContent(string templateRelativePath, string layoutsDir, string mode)
    {
        var normalizedMode = (mode ?? "auto").Trim().ToLowerInvariant();
        if (normalizedMode == "always")
        {
            return new ListPageContentResolution(true, false, "mode_always");
        }

        if (normalizedMode == "never")
        {
            return new ListPageContentResolution(false, false, "mode_never");
        }

        var declared = ResolveDeclaredNeedsPageContent(layoutsDir, templateRelativePath);
        if (declared.HasValue)
        {
            return new ListPageContentResolution(declared.Value, false, "declared");
        }

        var analysis = TemplateStaticAnalysisService.AnalyzeNeedsPageContent(layoutsDir, templateRelativePath);
        if (analysis.NeedsPageContent.HasValue)
        {
            return new ListPageContentResolution(analysis.NeedsPageContent.Value, false, analysis.Source);
        }

        return new ListPageContentResolution(FallbackHeuristic(templateRelativePath, layoutsDir), true, $"heuristic:{analysis.Source}");
    }

    private static bool? ResolveDeclaredNeedsPageContent(string layoutsDir, string templateRelativePath)
    {
        return GetCapabilities(templateRelativePath, layoutsDir)?.NeedsPageContent;
    }

    internal static TemplateCapabilityFlags? GetCapabilities(string templateRelativePath, string layoutsDir)
    {
        var task = Cache.GetOrAdd(layoutsDir, static dir => LoadManifestAsync(dir));
        var manifest = task.GetAwaiter().GetResult();
        if (manifest?.Templates is null)
        {
            return null;
        }

        var key = NormalizeManifestKey(templateRelativePath);
        if (!manifest.Templates.TryGetValue(key, out var template) || template?.Capabilities is null)
        {
            return null;
        }

        return template.Capabilities;
    }

    internal static bool SupportsPagination(string templateRelativePath, string layoutsDir)
        => GetCapabilities(templateRelativePath, layoutsDir)?.SupportsPagination == true;

    internal static bool SupportsTaxonomy(string templateRelativePath, string layoutsDir)
        => GetCapabilities(templateRelativePath, layoutsDir)?.SupportsTaxonomy == true;

    internal static bool SupportsSearchSnippets(string templateRelativePath, string layoutsDir)
        => GetCapabilities(templateRelativePath, layoutsDir)?.SupportsSearchSnippets == true;

    private static async Task<TemplateCapabilitiesManifest?> LoadManifestAsync(string layoutsDir)
    {
        var manifestPath = Path.Combine(layoutsDir, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var text = await File.ReadAllTextAsync(manifestPath);
            var manifest = ReadManifest(text);
            ValidateManifestContents(manifest, layoutsDir);
            return manifest;
        }
        catch (ConfigException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Failed to parse {ManifestFileName}: {ex.Message}");
        }
    }

    private static TemplateCapabilitiesManifest ReadManifest(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ConfigException($"{ManifestFileName} must contain a root mapping.");
        }

        var templatesNode = GetRequiredMapping(root, "templates", $"{ManifestFileName} must contain a templates mapping.");
        var templates = new Dictionary<string, TemplateCapabilityDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (rawKey, definition) in ReadTemplateDefinitions(templatesNode))
        {
            templates[rawKey] = definition;
        }

        return new TemplateCapabilitiesManifest
        {
            Templates = templates
        };
    }

    private static bool FallbackHeuristic(string templateRelativePath, string layoutsDir)
    {
        var key = CacheKey(layoutsDir, templateRelativePath);
        return FallbackCache.GetOrAdd(key, _ =>
        {
            var fullPath = Path.Combine(layoutsDir, templateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return true;
            }

            var template = File.ReadAllText(fullPath);
            return template.Contains(".content", StringComparison.OrdinalIgnoreCase) ||
                   template.Contains("include", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string CacheKey(string layoutsDir, string templateRelativePath)
        => $"{layoutsDir}\u0000{templateRelativePath.Replace('\\', '/')}";

    private static void ValidateManifestContents(TemplateCapabilitiesManifest? manifest, string layoutsDir)
    {
        if (manifest?.Templates is null || manifest.Templates.Count == 0)
        {
            throw new ConfigException($"{ManifestFileName} must contain a non-empty templates mapping.");
        }

        var layoutsFullPath = Path.GetFullPath(layoutsDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        foreach (var (rawKey, definition) in manifest.Templates)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                throw new ConfigException($"{ManifestFileName} contains an empty template path.");
            }

            var key = NormalizeManifestKey(rawKey);
            if (Path.IsPathRooted(key))
            {
                throw new ConfigException($"Template path '{rawKey}' in {ManifestFileName} must be relative to layouts.");
            }

            var fullPath = Path.GetFullPath(Path.Combine(layoutsDir, key.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(layoutsFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException($"Template path '{rawKey}' in {ManifestFileName} must stay within layouts.");
            }

            if (!File.Exists(fullPath))
            {
                throw new ConfigException($"Template declared in {ManifestFileName} not found: {rawKey}");
            }

            if (definition?.Capabilities is null)
            {
                throw new ConfigException($"Template '{rawKey}' in {ManifestFileName} must declare capabilities.");
            }

            if (!definition.Capabilities.HasAnyValue())
            {
                throw new ConfigException($"Template '{rawKey}' in {ManifestFileName} must declare at least one capability.");
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, TemplateCapabilityDefinition>> ReadTemplateDefinitions(YamlMappingNode templatesNode)
    {
        foreach (var entry in templatesNode.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
            {
                throw new ConfigException($"{ManifestFileName} template keys must be strings.");
            }

            var key = keyNode.Value?.Trim() ?? string.Empty;
            var definitionNode = entry.Value as YamlMappingNode
                ?? throw new ConfigException($"Template '{key}' in {ManifestFileName} must be a mapping.");
            yield return new KeyValuePair<string, TemplateCapabilityDefinition>(key, ReadTemplateDefinition(key, definitionNode));
        }
    }

    private static TemplateCapabilityDefinition ReadTemplateDefinition(string templatePath, YamlMappingNode definitionNode)
    {
        var capabilitiesNode = GetRequiredMapping(
            definitionNode,
            "capabilities",
            $"Template '{templatePath}' in {ManifestFileName} must declare capabilities.");

        return new TemplateCapabilityDefinition
        {
            Capabilities = ReadCapabilities(templatePath, capabilitiesNode)
        };
    }

    private static TemplateCapabilityFlags ReadCapabilities(string templatePath, YamlMappingNode capabilitiesNode)
    {
        return new TemplateCapabilityFlags
        {
            NeedsPageContent = GetOptionalBool(capabilitiesNode, "needs_page_content", templatePath),
            SupportsPagination = GetOptionalBool(capabilitiesNode, "supports_pagination", templatePath),
            SupportsTaxonomy = GetOptionalBool(capabilitiesNode, "supports_taxonomy", templatePath),
            SupportsSearchSnippets = GetOptionalBool(capabilitiesNode, "supports_search_snippets", templatePath),
            Fields = ReadFieldDeclarations(capabilitiesNode, templatePath)
        };
    }

    private static YamlMappingNode GetRequiredMapping(YamlMappingNode node, string key, string errorMessage)
    {
        foreach (var entry in node.Children)
        {
            if (entry.Key is YamlScalarNode keyNode &&
                string.Equals(keyNode.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value as YamlMappingNode ?? throw new ConfigException(errorMessage);
            }
        }

        throw new ConfigException(errorMessage);
    }

    private static List<TemplateFieldDeclaration>? ReadFieldDeclarations(YamlMappingNode node, string templatePath)
    {
        YamlSequenceNode? fieldsSeq = null;
        foreach (var entry in node.Children)
        {
            if (entry.Key is YamlScalarNode keyNode &&
                string.Equals(keyNode.Value, "fields", StringComparison.OrdinalIgnoreCase) &&
                entry.Value is YamlSequenceNode seq)
            {
                fieldsSeq = seq;
                break;
            }
        }

        if (fieldsSeq is null) return null;

        var fields = new List<TemplateFieldDeclaration>();
        foreach (var item in fieldsSeq.Children)
        {
            if (item is not YamlMappingNode fieldNode) continue;

            var field = new TemplateFieldDeclaration();
            foreach (var prop in fieldNode.Children)
            {
                if (prop.Key is not YamlScalarNode keyNode || prop.Value is not YamlScalarNode valNode) continue;
                var val = valNode.Value?.Trim();
                if (string.Equals(keyNode.Value, "key", StringComparison.OrdinalIgnoreCase))
                    field = field with { Key = val };
                else if (string.Equals(keyNode.Value, "type", StringComparison.OrdinalIgnoreCase))
                    field = field with { Type = val };
                else if (string.Equals(keyNode.Value, "label", StringComparison.OrdinalIgnoreCase))
                    field = field with { Label = val };
                else if (string.Equals(keyNode.Value, "suggestion", StringComparison.OrdinalIgnoreCase))
                    field = field with { Suggestion = val };
            }

            if (!string.IsNullOrWhiteSpace(field.Key))
            {
                fields.Add(field);
            }
        }

        return fields.Count > 0 ? fields : null;
    }

    private static bool? GetOptionalBool(YamlMappingNode node, string key, string templatePath)
    {
        foreach (var entry in node.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode ||
                !string.Equals(keyNode.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Value is not YamlScalarNode valueNode || string.IsNullOrWhiteSpace(valueNode.Value))
            {
                return null;
            }

            if (bool.TryParse(valueNode.Value, out var value))
            {
                return value;
            }

            throw new ConfigException($"Template '{templatePath}' in {ManifestFileName} has invalid boolean value for {key}.");
        }

        return null;
    }

    private static string NormalizeManifestKey(string templateRelativePath)
        => templateRelativePath.Replace('\\', '/');

    internal sealed class TemplateCapabilitiesManifest
    {
        public Dictionary<string, TemplateCapabilityDefinition>? Templates { get; init; }
    }

    internal sealed class TemplateCapabilityDefinition
    {
        public TemplateCapabilityFlags? Capabilities { get; init; }
    }

    internal sealed class TemplateCapabilityFlags
    {
        public bool? NeedsPageContent { get; init; }
        public bool? SupportsPagination { get; init; }
        public bool? SupportsTaxonomy { get; init; }
        public bool? SupportsSearchSnippets { get; init; }
        public List<TemplateFieldDeclaration>? Fields { get; init; }

        internal bool HasAnyValue()
        {
            return NeedsPageContent.HasValue ||
                   SupportsPagination.HasValue ||
                   SupportsTaxonomy.HasValue ||
                   SupportsSearchSnippets.HasValue ||
                   Fields is { Count: > 0 };
        }
    }

    internal sealed record ListPageContentResolution(
        bool IncludeContent,
        bool UsedHeuristic,
        string Source);

    internal sealed record TemplateFieldDeclaration
    {
        public string? Key { get; init; }
        public string? Type { get; init; }
        public string? Label { get; init; }
        public string? Suggestion { get; init; }
    }
}
