using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigContractDriftTests
{
    private static readonly Regex CodeSpanRegex = new("`([^`\\r\\n]+)`", RegexOptions.Compiled);
    private static readonly Regex ConfigPathRegex = new("^[A-Za-z][A-Za-z0-9]*(?:\\[\\])?(?:\\.[A-Za-z][A-Za-z0-9]*(?:\\[\\])?|\\.\\*)+$", RegexOptions.Compiled);
    private static readonly Regex YamlKeyRegex = new("^([A-Za-z][A-Za-z0-9]*):(?:\\s|$)", RegexOptions.Compiled);
    private static readonly Regex FencedCodeStartRegex = new("^```\\s*([A-Za-z0-9_-]*)", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> ShortConfigPathPrefixes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["markdown"] = "content.sources[].markdown",
        ["notion"] = "content.sources[].notion"
    };

    private static readonly IReadOnlySet<string> NonSiteYamlReferencePrefixes = new HashSet<string>(StringComparer.Ordinal)
    {
        ".bukit",
        "Bukit",
        "ContentDocument",
        "RouteInfo",
        "README",
        "page",
        "site.base_url",
        "site.data",
        "site.modules",
        "theme.yaml"
    };

    private static readonly string[] NonConfigPathExtensions =
    {
        ".cs",
        ".html",
        ".json",
        ".md",
        ".txt",
        ".xml",
        ".yaml",
        ".yml"
    };

    private static readonly IReadOnlySet<string> AllowedNegativeReferences = new HashSet<string>(StringComparer.Ordinal)
    {
        "theme.extends"
    };

    private static readonly IReadOnlySet<string> OpenEndedDynamicConfigPrefixes = new HashSet<string>(StringComparer.Ordinal)
    {
        "site.plugins",
        "theme.params"
    };

    [Fact]
    public void ConfigSchema_AllStrictTopLevelFieldsMatchAppConfig()
    {
        var schemaFields = GetSchemaProperties("$");
        var appConfigFields = typeof(AppConfig).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => ToYamlKey(property.Name))
            .ToHashSet(StringComparer.Ordinal);
        var strictRootFields = GetStrictFieldSet("RootKeys");

        Assert.Equal(appConfigFields.Order(StringComparer.Ordinal), schemaFields.Order(StringComparer.Ordinal));
        Assert.Equal(strictRootFields.Order(StringComparer.Ordinal), schemaFields.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ConfigDocs_DoNotReferenceUnknownSiteYamlFields()
    {
        AssertSiteYamlFieldReferencesAreKnown(EnumerateMarkdownFiles("guide/user", "guide/dev"));
    }

    [Fact]
    public void SkillsConfig_DoNotReferenceUnknownSiteYamlFields()
    {
        AssertSiteYamlFieldReferencesAreKnown(EnumerateMarkdownFiles("guide/skills"));
    }

    [Fact]
    public void Readmes_DoNotReferenceUnknownSiteYamlFields()
    {
        AssertSiteYamlFieldReferencesAreKnown(EnumerateReadmeFiles());
    }

    [Theory]
    [InlineData("BuildContext.Data")]
    [InlineData("BuildContext.Config")]
    [InlineData("CliParser.Parse")]
    [InlineData("CommandDescriptor.DispatchAsync")]
    [InlineData("CliErrorRenderer.CliErrorPayload")]
    [InlineData("RouteGenerator.GenerateWithSource")]
    [InlineData("SectionSchemaValidator.Validate")]
    [InlineData("Incremental.HashUtil")]
    [InlineData("RssGenerator.Post")]
    [InlineData("PluginRegistry.GetAllPlugins")]
    [InlineData("Directory.Build.props")]
    public void InlineCodeSpanConfigReferences_IgnoreClrApiIdentities(string reference)
    {
        Assert.False(TryNormalizeConfigPath(reference, out _));
    }

    [Theory]
    [InlineData("site.title", "site.title")]
    [InlineData("markdown.dir", "content.sources[].markdown.dir")]
    public void InlineCodeSpanConfigReferences_NormalizeKnownLowercasePaths(string reference, string expected)
    {
        Assert.True(TryNormalizeConfigPath(reference, out var normalized));
        Assert.Equal(expected, normalized);
        Assert.True(IsAllowedConfigPath(normalized, BuildAllowedConfigPaths()));
    }

    [Fact]
    public void InlineCodeSpanConfigReferences_RejectUnknownLowercasePaths()
    {
        Assert.True(TryNormalizeConfigPath("site.notARealField", out var normalized));
        Assert.False(IsAllowedConfigPath(normalized, BuildAllowedConfigPaths()));
    }

    [Theory]
    [InlineData("site", "SiteKeys")]
    [InlineData("content", "ContentKeys")]
    [InlineData("build", "BuildKeys")]
    [InlineData("theme", "ThemeKeys")]
    [InlineData("taxonomy", "TaxonomyKeys")]
    [InlineData("logging", "LoggingKeys")]
    [InlineData("deploy", "DeployKeys")]
    [InlineData("content.sources[]", null)]
    public void ConfigSchema_StrictValidatorTopLevelSectionsStayInSync(string schemaPath, string? strictFieldName)
    {
        var schemaFields = GetSchemaProperties(schemaPath);
        var strictFields = strictFieldName is null
            ? new HashSet<string>(StringComparer.Ordinal)
            {
                "type", "name", "mode", "collection", "addToCollections", "markdown", "notion", "dataIndex"
            }
            : GetStrictFieldSet(strictFieldName);

        Assert.Equal(strictFields.Order(StringComparer.Ordinal), schemaFields.Order(StringComparer.Ordinal));
    }

    private static void AssertSiteYamlFieldReferencesAreKnown(IEnumerable<string> files)
    {
        var allowedPaths = BuildAllowedConfigPaths();
        var failures = new List<string>();

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(RepoRoot(), file);
            var lines = File.ReadAllLines(file);
            foreach (var reference in ExtractYamlBlockReferences(lines))
            {
                if (!IsAllowedConfigPath(reference.Path, allowedPaths))
                {
                    failures.Add($"{relative}:{reference.LineNumber}: unknown site.yaml field `{reference.Path}`");
                }
            }

            for (var index = 0; index < lines.Length; index++)
            {
                foreach (Match match in CodeSpanRegex.Matches(lines[index]))
                {
                    var value = match.Groups[1].Value.Trim();
                    if (!TryNormalizeConfigPath(value, out var normalized))
                    {
                        continue;
                    }

                    if (!IsAllowedConfigPath(normalized, allowedPaths))
                    {
                        failures.Add($"{relative}:{index + 1}: unknown site.yaml field `{value}`");
                    }
                }
            }
        }

        Assert.False(failures.Count > 0, string.Join(Environment.NewLine, failures));
    }

    private static IReadOnlyList<(int LineNumber, string Path)> ExtractYamlBlockReferences(IReadOnlyList<string> lines)
    {
        var references = new List<(int LineNumber, string Path)>();
        var inFence = false;
        var isYamlFence = false;
        var fenceStartLine = 0;
        var fenceLines = new List<(int LineNumber, string Text)>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (inFence)
                {
                    if (isYamlFence)
                    {
                        references.AddRange(ExtractSiteYamlReferencesFromBlock(fenceLines));
                    }

                    inFence = false;
                    isYamlFence = false;
                    fenceStartLine = 0;
                    fenceLines.Clear();
                    continue;
                }

                var match = FencedCodeStartRegex.Match(trimmed);
                var language = match.Success ? match.Groups[1].Value : string.Empty;
                inFence = true;
                isYamlFence = language.Equals("yaml", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("yml", StringComparison.OrdinalIgnoreCase);
                fenceStartLine = index + 1;
                continue;
            }

            if (inFence && isYamlFence)
            {
                fenceLines.Add((index + 1, line));
            }
        }

        if (inFence && isYamlFence)
        {
            throw new InvalidOperationException($"Unclosed yaml fence starting at line {fenceStartLine}.");
        }

        return references;
    }

    private static IReadOnlyList<(int LineNumber, string Path)> ExtractSiteYamlReferencesFromBlock(IReadOnlyList<(int LineNumber, string Text)> blockLines)
    {
        if (!LooksLikeSiteYamlBlock(blockLines))
        {
            return Array.Empty<(int LineNumber, string Path)>();
        }

        var references = new List<(int LineNumber, string Path)>();
        var stack = new List<(int Indent, string Path)>();

        foreach (var (lineNumber, text) in blockLines)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var trimmedStart = text.TrimStart();
            if (trimmedStart.StartsWith('#'))
            {
                continue;
            }

            var indent = text.Length - trimmedStart.Length;
            var isSequenceItem = trimmedStart.StartsWith("- ", StringComparison.Ordinal);
            var candidate = isSequenceItem ? trimmedStart[2..].TrimStart() : trimmedStart;
            var match = YamlKeyRegex.Match(candidate);
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups[1].Value;
            var valuePart = candidate[match.Length..].Trim();
            var isContainer = valuePart.Length == 0 || valuePart.StartsWith('#');
            while (stack.Count > 0 && stack[^1].Indent >= indent)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            var parentPath = stack.Count == 0 ? string.Empty : stack[^1].Path;
            if (isSequenceItem && parentPath.Length > 0)
            {
                parentPath += "[]";
                stack.Add((indent, parentPath));
            }

            var path = string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}.{key}";
            references.Add((lineNumber, path));
            if (isContainer)
            {
                stack.Add((indent, path));
            }
        }

        return references;
    }

    private static bool LooksLikeSiteYamlBlock(IReadOnlyList<(int LineNumber, string Text)> blockLines)
    {
        foreach (var (_, text) in blockLines)
        {
            var trimmed = text.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var match = YamlKeyRegex.Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            return GetSchemaProperties("$").Contains(match.Groups[1].Value);
        }

        return false;
    }

    private static bool TryNormalizeConfigPath(string value, out string normalized)
    {
        normalized = value.Trim();
        if (!ConfigPathRegex.IsMatch(normalized))
        {
            return false;
        }

        if (char.IsUpper(normalized[0]))
        {
            return false;
        }

        foreach (var extension in NonConfigPathExtensions)
        {
            if (normalized.EndsWith(extension, StringComparison.Ordinal))
            {
                return false;
            }
        }

        foreach (var prefix in NonSiteYamlReferencePrefixes)
        {
            if (normalized.Equals(prefix, StringComparison.Ordinal) || normalized.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (AllowedNegativeReferences.Contains(normalized))
        {
            return false;
        }

        var firstSegment = normalized.Split('.')[0];
        if (ShortConfigPathPrefixes.TryGetValue(firstSegment, out var longPrefix))
        {
            normalized = longPrefix + normalized[firstSegment.Length..];
        }

        return true;
    }

    private static bool IsAllowedConfigPath(string path, IReadOnlySet<string> allowedPaths)
    {
        if (allowedPaths.Contains(path))
        {
            return true;
        }

        foreach (var allowedPath in allowedPaths)
        {
            if (!allowedPath.Contains(".*", StringComparison.Ordinal))
            {
                continue;
            }

            var pattern = "^" + Regex.Escape(allowedPath).Replace("\\.\\*", "\\.[^.]+", StringComparison.Ordinal) + "$";
            if (Regex.IsMatch(path, pattern))
            {
                return true;
            }
        }

        foreach (var prefix in OpenEndedDynamicConfigPrefixes)
        {
            if (path.StartsWith(prefix + ".", StringComparison.Ordinal) &&
                allowedPaths.Contains(prefix + ".*"))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> BuildAllowedConfigPaths()
    {
        using var doc = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var paths = new HashSet<string>(StringComparer.Ordinal);
        CollectSchemaPaths(doc.RootElement, "$", paths);
        return paths;
    }

    private static void CollectSchemaPaths(JsonElement schema, string path, HashSet<string> paths)
    {
        if (path != "$")
        {
            paths.Add(path);
        }

        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                var childPath = path == "$" ? property.Name : $"{path}.{property.Name}";
                CollectSchemaPaths(property.Value, childPath, paths);
            }
        }

        if (schema.TryGetProperty("items", out var items))
        {
            CollectSchemaPaths(items, path + "[]", paths);
        }

        if (schema.TryGetProperty("additionalProperties", out var additionalProperties) &&
            additionalProperties.ValueKind == JsonValueKind.True)
        {
            paths.Add(path + ".*");
        }
        else if (schema.TryGetProperty("additionalProperties", out additionalProperties) &&
            additionalProperties.ValueKind == JsonValueKind.Object)
        {
            CollectSchemaPaths(additionalProperties, path + ".*", paths);
        }
    }

    private static HashSet<string> GetSchemaProperties(string path)
    {
        using var doc = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var node = ResolveSchemaPath(doc.RootElement, path);
        return node.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static JsonElement ResolveSchemaPath(JsonElement root, string path)
    {
        if (path == "$")
        {
            return root;
        }

        var node = root;
        foreach (var rawSegment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var isArray = rawSegment.EndsWith("[]", StringComparison.Ordinal);
            var segment = isArray ? rawSegment[..^2] : rawSegment;
            node = node.GetProperty("properties").GetProperty(segment);
            if (isArray)
            {
                node = node.GetProperty("items");
            }
        }

        return node;
    }

    private static HashSet<string> GetStrictFieldSet(string fieldName)
    {
        var field = typeof(ConfigStrictFieldValidator).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = Assert.IsAssignableFrom<HashSet<string>>(field.GetValue(null));
        return new HashSet<string>(value, StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateMarkdownFiles(params string[] roots)
    {
        var repoRoot = RepoRoot();
        foreach (var root in roots)
        {
            var directory = Path.Combine(repoRoot, root);
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(file).Equals("SKILL.md", StringComparison.Ordinal))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateReadmeFiles()
    {
        var repoRoot = RepoRoot();
        foreach (var fileName in new[] { "README.md", "README.zh-CN.md", "README.ms.md" })
        {
            yield return Path.Combine(repoRoot, fileName);
        }
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string ToYamlKey(string propertyName)
        => char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
