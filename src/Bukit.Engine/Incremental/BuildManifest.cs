using System.Text.Json;

namespace Bukit.Engine.Incremental;

public sealed class BuildManifest
{
    public int Version { get; set; } = 2;
    public string TemplateHash { get; set; } = string.Empty;
    public Dictionary<string, BuildManifestEntry> Entries { get; set; } = new(StringComparer.Ordinal);

    public static BuildManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return new BuildManifest();
        }

        try
        {
            using var stream = File.OpenRead(manifestPath);
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
                        TemplateHash = GetString(entryEl, "templateHash") ?? string.Empty
                    };

                    manifest.Entries[key] = entry;
                }
            }

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
            using (var stream = File.Create(tempPath))
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
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
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.Flush();
            }

            File.Move(tempPath, manifestPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
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

public sealed class BuildManifestEntry
{
    public string OutputPath { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string MetadataHash { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string RouteHash { get; set; } = string.Empty;
    public string TemplateHash { get; set; } = string.Empty;
}

