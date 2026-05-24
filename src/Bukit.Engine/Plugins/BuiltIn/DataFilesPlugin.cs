using System.Text.Json;
using Bukit.Content;
using Bukit.Routing;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class DataFilesPlugin : IBukitPlugin, IDerivePagesPlugin
{
    public string Name => "data-files";
    public string Version => "1.0.0";

    public IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        var dataDir = Path.Combine(context.RootDir, "data");
        if (!Directory.Exists(dataDir))
        {
            return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var languages = context.Config.Site.Languages;
        if (languages is { Count: > 0 })
        {
            foreach (var lang in languages)
            {
                var langDir = Path.Combine(dataDir, lang);
                if (Directory.Exists(langDir))
                {
                    result[lang] = LoadDataDirectory(langDir);
                }
            }
        }

        var defaultData = LoadDataDirectory(dataDir);
        foreach (var (key, value) in defaultData)
        {
            if (!result.ContainsKey(key))
            {
                result[key] = value;
            }
        }

        if (result.Count > 0)
        {
            context.Data["__data_files"] = result;
        }

        return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
    }

    private static Dictionary<string, object> LoadDataDirectory(string dir)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is not ".yaml" and not ".yml" and not ".json" and not ".toml")
            {
                continue;
            }

            var key = Path.GetFileNameWithoutExtension(file);
            object? data = null;

            try
            {
                var content = File.ReadAllText(file);

                if (ext is ".json")
                {
                    data = JsonSerializer.Deserialize<Dictionary<string, object>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                else
                {
                    data = new DeserializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .Build()
                        .Deserialize<object>(content);
                }
            }
            catch
            {
                continue;
            }

            if (data is not null)
            {
                result[key] = data;
            }
        }

        foreach (var subDir in Directory.EnumerateDirectories(dir))
        {
            var subName = Path.GetFileName(subDir);
            var subData = LoadDataDirectory(subDir);
            if (subData.Count > 0)
            {
                result[subName] = subData;
            }
        }

        return result;
    }
}
