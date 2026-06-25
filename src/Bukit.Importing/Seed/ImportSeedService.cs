using System.Text;
using System.Text.Json;
using Bukit.Shared;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Bukit.Importing.Seed;

public sealed class ImportSeedService : IImportSeedService
{
    private static readonly (string FileBase, string Collection)[] KnownFiles =
    [
        ("pages", "page"),
        ("navigation", "navigation"),
        ("posts", "post"),
        ("companies", "company"),
        ("services", "service")
    ];

    public ImportSeedResult Import(ImportSeedOptions options)
    {
        string projectRoot = NormalizeFullPath(options.ProjectRoot);
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return ImportSeedResult.Failed(Diagnostic("import.seedDirInvalid", "Project root is required."));
        }

        string seedDirectory = NormalizeFullPath(options.SeedDirectory);
        if (string.IsNullOrWhiteSpace(seedDirectory) || !IsInsideDirectory(seedDirectory, projectRoot))
        {
            return ImportSeedResult.Failed(Diagnostic("import.seedDirInvalid", "Seed directory must be inside the project root.", RelativePath(projectRoot, options.SeedDirectory)));
        }

        if (!Directory.Exists(seedDirectory))
        {
            return ImportSeedResult.Failed(Diagnostic("import.seedDirNotFound", "Seed directory was not found.", RelativePath(projectRoot, seedDirectory)));
        }

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            return ImportSeedResult.Failed(Diagnostic("import.missingOutput", "Output directory is required."));
        }

        string outputDirectory = NormalizeFullPath(options.OutputDirectory);
        if (!IsInsideDirectory(outputDirectory, projectRoot))
        {
            return ImportSeedResult.Failed(Diagnostic("import.outputOutsideProject", "Output directory must stay inside the project root.", RelativePath(projectRoot, outputDirectory)));
        }

        if (Directory.Exists(outputDirectory)
            && Directory.EnumerateFileSystemEntries(outputDirectory).Any()
            && !options.Force)
        {
            return ImportSeedResult.Failed(Diagnostic("import.outputAlreadyExists", "Output directory already contains files. Re-run with force to overwrite.", RelativePath(projectRoot, outputDirectory)));
        }

        List<ImportSeedRecord> records;
        try
        {
            records = ReadDirectory(seedDirectory, projectRoot);
        }
        catch (ImportSeedReadException ex)
        {
            return ImportSeedResult.Failed(Diagnostic("import.seedRecordInvalid", ex.Message, ex.RelativePath));
        }

        try
        {
            IReadOnlyList<ImportSeedArtifact> artifacts = WriteMarkdown(projectRoot, outputDirectory, records, options.Force);
            return ImportSeedResult.Succeeded(artifacts);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ImportSeedResult.Failed(Diagnostic("import.seedWriteFailed", ex.Message, RelativePath(projectRoot, outputDirectory)));
        }
    }

    private static List<ImportSeedRecord> ReadDirectory(string seedDirectory, string projectRoot)
    {
        var records = new List<ImportSeedRecord>();
        foreach ((string fileBase, string collection) in KnownFiles)
        {
            ReadIfExists(records, Path.Combine(seedDirectory, $"{fileBase}.json"), collection, projectRoot);
            ReadIfExists(records, Path.Combine(seedDirectory, $"{fileBase}.yaml"), collection, projectRoot);
            ReadIfExists(records, Path.Combine(seedDirectory, $"{fileBase}.yml"), collection, projectRoot);
        }

        return records;
    }

    private static void ReadIfExists(List<ImportSeedRecord> records, string path, string collection, string projectRoot)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            records.AddRange(extension switch
            {
                ".json" => ReadJson(path, collection),
                ".yaml" or ".yml" => ReadYaml(path, collection),
                _ => []
            });
        }
        catch (Exception ex) when (ex is JsonException or YamlException or IOException)
        {
            throw new ImportSeedReadException($"Invalid seed record file: {RelativePath(projectRoot, path)}", RelativePath(projectRoot, path), ex);
        }
    }

    private static IEnumerable<ImportSeedRecord> ReadJson(string path, string collection)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? title = ReadString(item, "title") ?? ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            yield return new ImportSeedRecord(
                Collection: NormalizeCollection(collection, ReadString(item, "type")),
                Title: title,
                Slug: ReadString(item, "slug") ?? "",
                Summary: ReadString(item, "summary"),
                Content: ReadString(item, "content"),
                Language: ReadString(item, "language"),
                Published: ReadBool(item, "published") ?? true,
                SeoTitle: ReadString(item, "seo_title"),
                SeoDescription: ReadString(item, "seo_description"),
                ExtraFields: ReadExtraFields(item));
        }
    }

    private static IEnumerable<ImportSeedRecord> ReadYaml(string path, string collection)
    {
        var stream = new YamlStream();
        using var reader = File.OpenText(path);
        stream.Load(reader);
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlSequenceNode sequence)
        {
            yield break;
        }

        foreach (YamlMappingNode node in sequence.Children.OfType<YamlMappingNode>())
        {
            string? title = ReadString(node, "title") ?? ReadString(node, "name");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            yield return new ImportSeedRecord(
                Collection: NormalizeCollection(collection, ReadString(node, "type")),
                Title: title,
                Slug: ReadString(node, "slug") ?? "",
                Summary: ReadString(node, "summary"),
                Content: ReadString(node, "content"),
                Language: ReadString(node, "language"),
                Published: ReadBool(node, "published") ?? true,
                SeoTitle: ReadString(node, "seo_title"),
                SeoDescription: ReadString(node, "seo_description"),
                ExtraFields: ReadExtraFields(node));
        }
    }

    private static IReadOnlyList<ImportSeedArtifact> WriteMarkdown(
        string projectRoot,
        string outputDirectory,
        IReadOnlyList<ImportSeedRecord> records,
        bool overwrite)
    {
        Directory.CreateDirectory(outputDirectory);
        var artifacts = new List<ImportSeedArtifact>();

        foreach (ImportSeedRecord record in records)
        {
            string slug = GetEffectiveSlug(record);
            string path = ResolvePath(outputDirectory, record, slug);
            if (File.Exists(path) && !overwrite)
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, BuildMarkdown(record, slug));
            artifacts.Add(new ImportSeedArtifact(
                Type: "markdown",
                Path: RelativePath(projectRoot, path),
                Description: $"Imported {record.Collection} seed record."));
        }

        return artifacts;
    }

    private static string ResolvePath(string outputDirectory, ImportSeedRecord record, string slug)
        => record.Collection switch
        {
            "navigation" => Path.Combine(outputDirectory, "navigation", $"{slug}.md"),
            "post" => Path.Combine(outputDirectory, "posts", $"{slug}.md"),
            "company" => Path.Combine(outputDirectory, "companies", $"{slug}.md"),
            "service" => Path.Combine(outputDirectory, "services", $"{slug}.md"),
            _ when slug.Equals("index", StringComparison.OrdinalIgnoreCase) =>
                Path.Combine(outputDirectory, "index.md"),
            _ => Path.Combine(outputDirectory, "pages", $"{slug}.md")
        };

    private static string GetEffectiveSlug(ImportSeedRecord record)
    {
        string slug = string.IsNullOrWhiteSpace(record.Slug)
            ? SlugHelper.Slugify(record.Title)
            : SlugHelper.Slugify(record.Slug);

        return string.IsNullOrWhiteSpace(slug) ? "index" : slug;
    }

    private static string BuildMarkdown(ImportSeedRecord record, string slug)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{EscapeYaml(record.Title)}\"");
        sb.AppendLine($"slug: \"{EscapeYaml(slug)}\"");
        sb.AppendLine($"type: \"{record.Collection}\"");
        if (!string.IsNullOrWhiteSpace(record.Summary))
        {
            sb.AppendLine($"summary: \"{EscapeYaml(record.Summary)}\"");
        }

        if (!string.IsNullOrWhiteSpace(record.Language))
        {
            sb.AppendLine($"language: \"{EscapeYaml(record.Language)}\"");
        }

        if (!string.IsNullOrWhiteSpace(record.SeoTitle))
        {
            sb.AppendLine($"seo_title: \"{EscapeYaml(record.SeoTitle)}\"");
        }

        if (!string.IsNullOrWhiteSpace(record.SeoDescription))
        {
            sb.AppendLine($"seo_description: \"{EscapeYaml(record.SeoDescription)}\"");
        }

        if (record.ExtraFields is not null)
        {
            foreach ((string key, object? value) in record.ExtraFields)
            {
                if (value is null)
                {
                    continue;
                }

                sb.AppendLine(value switch
                {
                    bool b => $"{key}: {b.ToString().ToLowerInvariant()}",
                    int or long or float or double or decimal => $"{key}: {value}",
                    _ => $"{key}: \"{EscapeYaml(value.ToString() ?? "")}\""
                });
            }
        }

        sb.AppendLine($"published: {record.Published.ToString().ToLowerInvariant()}");
        sb.AppendLine("---");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(record.Content))
        {
            sb.AppendLine(record.Content);
        }

        return sb.ToString();
    }

    private static string EscapeYaml(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string NormalizeCollection(string fallback, string? type)
    {
        string normalized = string.IsNullOrWhiteSpace(type) ? fallback : type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "home" or "page" or "pages" => "page",
            "post" or "posts" or "article" or "articles" => "post",
            "company" or "companies" => "company",
            "service" or "services" => "service",
            "navigation" or "nav" or "menu" or "menus" => "navigation",
            _ => fallback
        };
    }

    private static IReadOnlyDictionary<string, object?>? ReadExtraFields(JsonElement item)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in item.EnumerateObject())
        {
            if (IsCoreField(property.Name))
            {
                continue;
            }

            fields[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number when property.Value.TryGetInt64(out long l) => l,
                JsonValueKind.Number when property.Value.TryGetDouble(out double d) => d,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        return fields.Count == 0 ? null : fields;
    }

    private static IReadOnlyDictionary<string, object?>? ReadExtraFields(YamlMappingNode node)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach ((YamlNode keyNode, YamlNode valueNode) in node.Children)
        {
            if (keyNode is not YamlScalarNode key
                || string.IsNullOrWhiteSpace(key.Value)
                || IsCoreField(key.Value)
                || valueNode is not YamlScalarNode value)
            {
                continue;
            }

            fields[key.Value] = ParseYamlScalar(value.Value);
        }

        return fields.Count == 0 ? null : fields;
    }

    private static bool IsCoreField(string name)
        => name is "title" or "name" or "slug" or "type" or "summary" or "content" or
            "language" or "published" or "seo_title" or "seo_description";

    private static object? ParseYamlScalar(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (bool.TryParse(value, out bool b))
        {
            return b;
        }

        if (long.TryParse(value, out long l))
        {
            return l;
        }

        if (double.TryParse(value, out double d))
        {
            return d;
        }

        return value;
    }

    private static string? ReadString(JsonElement item, string name)
        => item.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool? ReadBool(JsonElement item, string name)
        => item.TryGetProperty(name, out JsonElement property) ? property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        } : null;

    private static string? ReadString(YamlMappingNode node, string name)
        => TryGetScalar(node, name)?.Value;

    private static bool? ReadBool(YamlMappingNode node, string name)
        => bool.TryParse(TryGetScalar(node, name)?.Value, out bool value) ? value : null;

    private static YamlScalarNode? TryGetScalar(YamlMappingNode node, string name)
    {
        foreach ((YamlNode keyNode, YamlNode valueNode) in node.Children)
        {
            if (keyNode is YamlScalarNode key
                && string.Equals(key.Value, name, StringComparison.OrdinalIgnoreCase)
                && valueNode is YamlScalarNode scalar)
            {
                return scalar;
            }
        }

        return null;
    }

    private static ImportSeedDiagnostic Diagnostic(string code, string message, string? path = null)
        => new(code, "error", message, path is null ? null : NormalizeSeparators(path));

    private static string NormalizeFullPath(string path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);

    private static bool IsInsideDirectory(string path, string directory)
    {
        string fullPath = NormalizeFullPath(path);
        string fullDirectory = NormalizeFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullDirectory, StringComparison.Ordinal)
            || fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string RelativePath(string projectRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string fullPath = NormalizeFullPath(path);
        string relative = IsInsideDirectory(fullPath, projectRoot)
            ? Path.GetRelativePath(projectRoot, fullPath)
            : fullPath;
        return NormalizeSeparators(relative);
    }

    private static string NormalizeSeparators(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private sealed class ImportSeedReadException : Exception
    {
        public ImportSeedReadException(string message, string relativePath, Exception innerException)
            : base(message, innerException)
        {
            RelativePath = relativePath;
        }

        public string RelativePath { get; }
    }
}
