using System.Text.Json;

namespace Bukit.Engine.Incremental;

public sealed class BuildManifest
{
    public int Version { get; set; } = 2;
    public string TemplateHash { get; set; } = string.Empty;
    public Dictionary<string, BuildManifestEntry> Entries { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Media { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Assets { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Static { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, PluginOutputManifestEntry> PluginOutputs { get; set; } = new(StringComparer.Ordinal);

    public static BuildManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return new BuildManifest();
        }

        try
        {
            using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536);
            using var doc = JsonDocument.Parse(stream);

            var manifest = new BuildManifest();

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new BuildManifest();
            }

            if (root.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == JsonValueKind.Number)
            {
                manifest.Version = versionProp.GetInt32();
            }

            if (root.TryGetProperty("templateHash", out var templateHashProp) && templateHashProp.ValueKind == JsonValueKind.String)
            {
                manifest.TemplateHash = templateHashProp.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("entries", out var entriesProp) && entriesProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in entriesProp.EnumerateObject())
                {
                    var key = prop.Name;
                    var entryEl = prop.Value;
                    if (entryEl.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var entry = new BuildManifestEntry
                    {
                        OutputPath = GetString(entryEl, "outputPath") ?? key,
                        Url = GetString(entryEl, "url") ?? string.Empty,
                        Template = GetString(entryEl, "template") ?? string.Empty,
                        MetadataHash = GetString(entryEl, "metadataHash") ?? string.Empty,
                        ContentHash = GetString(entryEl, "contentHash") ?? string.Empty,
                        RouteHash = GetString(entryEl, "routeHash") ?? string.Empty,
                        TemplateHash = GetString(entryEl, "templateHash") ?? string.Empty,
                        RenderDependencyHash = GetString(entryEl, "renderDependencyHash") ?? string.Empty
                    };

                    manifest.Entries[key] = entry;
                }
            }

            ReadTrackedFileSet(root, "media", manifest.Media);
            ReadTrackedFileSet(root, "assets", manifest.Assets);
            ReadTrackedFileSet(root, "static", manifest.Static);
            ReadPluginOutputs(root, "pluginOutputs", manifest.PluginOutputs);

            return manifest;
        }
        catch (Exception ex) when (ex is FileNotFoundException or JsonException or IOException)
        {
            Console.Error.WriteLine($"[warn] Failed to load build manifest '{manifestPath}': {ex.Message}");
            return new BuildManifest();
        }
    }

    public void Save(string manifestPath)
    {
        var dir = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = manifestPath + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", Version);
                writer.WriteString("templateHash", TemplateHash);

                writer.WriteStartObject("entries");
                foreach (var kv in Entries.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(kv.Key);
                    writer.WriteStartObject();
                    writer.WriteString("outputPath", kv.Value.OutputPath);
                    writer.WriteString("url", kv.Value.Url);
                    writer.WriteString("template", kv.Value.Template);
                    writer.WriteString("metadataHash", kv.Value.MetadataHash);
                    writer.WriteString("contentHash", kv.Value.ContentHash);
                    writer.WriteString("routeHash", kv.Value.RouteHash);
                    writer.WriteString("templateHash", kv.Value.TemplateHash);
                    writer.WriteString("renderDependencyHash", kv.Value.RenderDependencyHash);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();

                WriteTrackedFileSet(writer, "media", Media);
                WriteTrackedFileSet(writer, "assets", Assets);
                WriteTrackedFileSet(writer, "static", Static);
                WritePluginOutputs(writer, "pluginOutputs", PluginOutputs);
                writer.WriteEndObject();
                writer.Flush();
            }

            File.Move(tempPath, manifestPath, overwrite: true);
        }
        catch
        {
            DeleteFileBestEffort(tempPath);
            throw;
        }
    }

    private static void DeleteFileBestEffort(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ReadTrackedFileSet(JsonElement root, string propertyName, Dictionary<string, string> target)
    {
        if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var item in prop.EnumerateObject())
        {
            target[item.Name] = item.Value.ValueKind == JsonValueKind.String
                ? item.Value.GetString() ?? string.Empty
                : string.Empty;
        }
    }

    private static void ReadPluginOutputs(JsonElement root, string propertyName, Dictionary<string, PluginOutputManifestEntry> target)
    {
        if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var item in prop.EnumerateObject())
        {
            if (item.Value.ValueKind == JsonValueKind.String)
            {
                target[item.Name] = new PluginOutputManifestEntry
                {
                    Path = item.Name,
                    Hash = item.Value.GetString() ?? string.Empty
                };
                continue;
            }

            if (item.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            target[item.Name] = new PluginOutputManifestEntry
            {
                Plugin = GetString(item.Value, "plugin") ?? string.Empty,
                Hook = GetString(item.Value, "hook") ?? string.Empty,
                Path = GetString(item.Value, "path") ?? item.Name,
                Hash = GetString(item.Value, "hash") ?? string.Empty
            };
        }
    }

    private static void WriteTrackedFileSet(Utf8JsonWriter writer, string propertyName, Dictionary<string, string> values)
    {
        writer.WriteStartObject(propertyName);
        foreach (var kv in values.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            writer.WriteString(kv.Key, kv.Value);
        }

        writer.WriteEndObject();
    }

    private static void WritePluginOutputs(Utf8JsonWriter writer, string propertyName, Dictionary<string, PluginOutputManifestEntry> values)
    {
        writer.WriteStartObject(propertyName);
        foreach (var kv in values.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(kv.Key);
            writer.WriteStartObject();
            writer.WriteString("plugin", kv.Value.Plugin);
            writer.WriteString("hook", kv.Value.Hook);
            writer.WriteString("path", kv.Value.Path);
            writer.WriteString("hash", kv.Value.Hash);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p))
        {
            return null;
        }

        return p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }
}

public sealed class PluginOutputManifestEntry
{
    public string Plugin { get; set; } = string.Empty;
    public string Hook { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}

public sealed class BuildManifestEntry
{
    public string OutputPath { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string MetadataHash { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string RouteHash { get; set; } = string.Empty;
    public string TemplateHash { get; set; } = string.Empty;
    public string RenderDependencyHash { get; set; } = string.Empty;
}
