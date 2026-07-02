using System.Text;
using System.Text.Json;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class PagesIndexCacheHelper
{
    public static Dictionary<string, object>? TryLoadCache(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var obj = ToObject(doc.RootElement);
            return obj as Dictionary<string, object>;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[warn] pages-index: failed to load cache '{path}': {ex.Message}");
            return null;
        }
    }

    public static void TrySaveCache(string path, Dictionary<string, object> index)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var cache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in index)
            {
                if (kv.Value is not Dictionary<string, object> page)
                {
                    continue;
                }

                if (!page.TryGetValue("url", out var urlObj) || urlObj is not string url || !string.IsNullOrEmpty(url))
                {
                    continue;
                }

                if (!page.ContainsKey("external_url"))
                {
                    continue;
                }

                cache[kv.Key] = page;
            }

            using var fs = File.Create(path);
            using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = false });
            writer.WriteStartObject();
            foreach (var kv in cache)
            {
                writer.WritePropertyName(kv.Key);
                WriteJsonValue(writer, kv.Value);
            }
            writer.WriteEndObject();
            writer.Flush();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[warn] pages-index: failed to save cache '{path}': {ex.Message}");
        }
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, object? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (value is string s)
        {
            writer.WriteStringValue(s);
            return;
        }

        if (value is bool b)
        {
            writer.WriteBooleanValue(b);
            return;
        }

        if (value is int i)
        {
            writer.WriteNumberValue(i);
            return;
        }

        if (value is long l)
        {
            writer.WriteNumberValue(l);
            return;
        }

        if (value is double d)
        {
            writer.WriteNumberValue(d);
            return;
        }

        if (value is float f)
        {
            writer.WriteNumberValue(f);
            return;
        }

        if (value is decimal dec)
        {
            writer.WriteNumberValue(dec);
            return;
        }

        if (value is DateTimeOffset dto)
        {
            writer.WriteStringValue(dto.ToString("O"));
            return;
        }

        if (value is DateTime dt)
        {
            writer.WriteStringValue(dt.ToString("O"));
            return;
        }

        if (value is IReadOnlyDictionary<string, object> roDict)
        {
            writer.WriteStartObject();
            foreach (var kv in roDict)
            {
                writer.WritePropertyName(kv.Key);
                WriteJsonValue(writer, kv.Value);
            }
            writer.WriteEndObject();
            return;
        }

        if (value is IDictionary<string, object> dict)
        {
            writer.WriteStartObject();
            foreach (var kv in dict)
            {
                writer.WritePropertyName(kv.Key);
                WriteJsonValue(writer, kv.Value);
            }
            writer.WriteEndObject();
            return;
        }

        if (value is System.Collections.IEnumerable seq)
        {
            writer.WriteStartArray();
            foreach (var x in seq)
            {
                WriteJsonValue(writer, x);
            }
            writer.WriteEndArray();
            return;
        }

        writer.WriteStringValue(value.ToString());
    }

    private static object? ToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Object => ToDictionary(el),
            JsonValueKind.Array => el.EnumerateArray().Select(ToObject).ToList(),
            JsonValueKind.String => el.GetString() ?? string.Empty,
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static Dictionary<string, object> ToDictionary(JsonElement el)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in el.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(p.Name))
            {
                continue;
            }

            dict[p.Name] = ToObject(p.Value) ?? string.Empty;
        }

        return dict;
    }

    public static string NormalizeCacheMode(string mode)
    {
        return (mode ?? "off").Trim().ToLowerInvariant() switch
        {
            "readonly" => "readonly",
            "readwrite" => "readwrite",
            _ => "off"
        };
    }

    public static string ResolveCachePath(string rootDir, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var raw = configured.Trim();
            return Path.IsPathRooted(raw) ? raw : Path.Combine(rootDir, raw);
        }

        return Path.Combine(rootDir, ".cache", "notion", "pages-index.json");
    }
}
