using System.Text;
using System.Text.Json;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class RouteCommand
{
    internal sealed record RouteInspectEntry(
        string Url,
        string OutputPath,
        string Template,
        string? Collection,
        string? Type,
        string? Language,
        string RouteSource);

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        var config = ConfigLoader.Load(resolved.FullConfigPath);
        ConfigValidator.Validate(config);

        var factory = new DefaultContentProviderFactory();
        var contentPipeline = new ContentPipeline(factory, new ConsoleLogger(LogLevel.Warn));
        var contentResult = await contentPipeline.ExecuteAsync(config, rootDir, new ConfigOverrides(), Path.Combine(rootDir, ".cache", "media"));

        var collections = BuildCollectionRules(config);

        var entries = new List<RouteInspectEntry>();
        foreach (var item in contentResult.Items)
        {
            if (MetaHelpers.IsDataItem(item)) continue;

            var (route, routeSource) = GenerateRouteForItem(item, collections);

            var collection = item.Meta.TryGetValue("collection", out var c) && c is not null
                ? c.ToString() : null;
            var type = item.Meta.TryGetValue("type", out var t) && t is not null
                ? t.ToString() : "page";
            var language = item.Meta.TryGetValue("language", out var l) && l is not null
                ? l.ToString() : null;

            entries.Add(new RouteInspectEntry(
                route.Url,
                route.OutputPath,
                route.Template,
                collection,
                type,
                language,
                routeSource));
        }

        var sub = command.GetArgument(0) ?? "inspect";
        switch (sub)
        {
            case "inspect":
                var filterCollection = command.GetString("--collection");
                if (filterCollection is not null)
                    entries = entries.Where(e => string.Equals(e.Collection, filterCollection, StringComparison.OrdinalIgnoreCase)).ToList();

                var asJson = command.GetString("--json") is not null;
                if (asJson)
                    PrintInspectJson(entries);
                else
                    PrintInspectTable(entries);
                return 0;
            default:
                Console.Error.WriteLine($"Unknown subcommand: {sub}. Use inspect.");
                return 1;
        }
    }

    private static Dictionary<string, (string Permalink, string Template)>? BuildCollectionRules(AppConfig config)
    {
        if (config.Site.Collections is null || config.Site.Collections.Count == 0)
            return null;

        var rules = new Dictionary<string, (string Permalink, string Template)>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in config.Site.Collections)
        {
            rules[kv.Key] = (kv.Value.Permalink, kv.Value.Template);
        }
        return rules;
    }

    private static (Engine.Abstractions.Routing.RouteInfo Route, string RouteSource) GenerateRouteForItem(
        ContentItem item,
        Dictionary<string, (string Permalink, string Template)>? collections)
    {
        if (item.Meta.TryGetValue("route", out var routeObj) && routeObj is IReadOnlyDictionary<string, object> routeMap)
        {
            var urlStr = GetOptionalString(routeMap, "url");
            var outputPathStr = GetOptionalString(routeMap, "outputPath");
            var templateStr = GetOptionalString(routeMap, "template");

            if (urlStr is not null && outputPathStr is not null && templateStr is not null)
            {
                var rt = new Engine.Abstractions.Routing.RouteInfo(
                    Url: urlStr.Trim(),
                    OutputPath: outputPathStr.Trim(),
                    Template: templateStr.Trim());
                return (rt, "FullOverride");
            }

            if (urlStr is not null || outputPathStr is not null || templateStr is not null)
            {
                var baseRoute = GenerateBaseRoute(item, collections);
                var url = urlStr?.Trim() ?? baseRoute.Url;
                var outputPath = outputPathStr?.Trim() ?? baseRoute.OutputPath;
                var template = templateStr?.Trim() ?? baseRoute.Template;
                var rt = new Engine.Abstractions.Routing.RouteInfo(url, outputPath, template);
                return (rt, "PartialOverride");
            }
        }

        return (GenerateBaseRoute(item, collections), DetermineBaseSource(item, collections));
    }

    private static Engine.Abstractions.Routing.RouteInfo GenerateBaseRoute(
        ContentItem item,
        Dictionary<string, (string Permalink, string Template)>? collections)
    {
        var collectionKey = item.Meta.TryGetValue("collection", out var c) && c is not null
            ? c.ToString() ?? string.Empty
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(collectionKey) && collections is not null && collections.TryGetValue(collectionKey, out var rule))
        {
            var url = ExpandPattern(rule.Permalink, item);
            var outputPath = BuildOutputPathFromUrl(url);
            return new Engine.Abstractions.Routing.RouteInfo(url, outputPath, rule.Template);
        }

        var typeVal = item.Meta.TryGetValue("type", out var t) && t is not null
            ? t.ToString() ?? "page"
            : "page";

        if (typeVal.Equals("post", StringComparison.OrdinalIgnoreCase))
        {
            return new Engine.Abstractions.Routing.RouteInfo(
                Url: $"/blog/{item.Slug}/",
                OutputPath: $"blog/{item.Slug}/index.html",
                Template: "pages/post.html");
        }

        return new Engine.Abstractions.Routing.RouteInfo(
            Url: $"/pages/{item.Slug}/",
            OutputPath: $"pages/{item.Slug}/index.html",
            Template: "pages/page.html");
    }

    private static string DetermineBaseSource(
        ContentItem item,
        Dictionary<string, (string, string)>? collections)
    {
        var collectionKey = item.Meta.TryGetValue("collection", out var c) && c is not null
            ? c.ToString() ?? string.Empty
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(collectionKey) && collections is not null && collections.ContainsKey(collectionKey))
            return "Collection";

        var typeVal = item.Meta.TryGetValue("type", out var t) && t is not null
            ? t.ToString() ?? string.Empty
            : string.Empty;

        if (typeVal.Equals("post", StringComparison.OrdinalIgnoreCase) || typeVal.Equals("page", StringComparison.OrdinalIgnoreCase))
            return "Permalink";

        return "BuiltinFallback";
    }

    private static string ExpandPattern(string pattern, ContentItem item)
    {
        var result = pattern;
        result = result.Replace("{slug}", item.Slug, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{title}", Slugify(item.Title), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{year}", item.PublishAt.Year.ToString("D4"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{month}", item.PublishAt.Month.ToString("D2"), StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{day}", item.PublishAt.Day.ToString("D2"), StringComparison.OrdinalIgnoreCase);

        var typeVal = item.Meta.TryGetValue("type", out var t) && t is not null ? (t.ToString() ?? "page") : "page";
        result = result.Replace("{type}", typeVal, StringComparison.OrdinalIgnoreCase);

        var collectionVal = item.Meta.TryGetValue("collection", out var c) && c is not null
            ? c.ToString() ?? string.Empty
            : string.Empty;
        result = result.Replace("{collection}", collectionVal, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    private static string BuildOutputPathFromUrl(string url)
    {
        var trimmed = url.Trim('/');
        return string.IsNullOrEmpty(trimmed) ? "index.html" : $"{trimmed}/index.html";
    }

    private static string? GetOptionalString(IReadOnlyDictionary<string, object> map, string key)
    {
        if (map.TryGetValue(key, out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        return null;
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var ch in input.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
            else if (ch is ' ' or '-' or '_')
                sb.Append(sb.Length > 0 && sb[^1] != '-' ? '-' : ' ');
        }
        var slug = sb.ToString().Replace(" ", "-").Trim('-');
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");
        return slug;
    }

    private static void PrintInspectTable(List<RouteInspectEntry> entries)
    {
        if (entries.Count == 0)
        {
            Console.WriteLine("Routes: (none)");
            return;
        }

        Console.WriteLine($"Routes: ({entries.Count})");
        Console.WriteLine();
        Console.WriteLine($"  {"URL",-32} {"Output",-40} {"Template",-24} {"Collection",-14} {"Type",-8} {"Source",-16}");
        Console.WriteLine($"  {"---",-32} {"------",-40} {"--------",-24} {"----------",-14} {"----",-8} {"------",-16}");

        foreach (var e in entries.OrderBy(e => e.Url, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {e.Url,-32} {e.OutputPath,-40} {e.Template,-24} {e.Collection ?? "-",-14} {e.Type ?? "-",-8} {e.RouteSource,-16}");
        }
    }

    private static void PrintInspectJson(List<RouteInspectEntry> entries)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartArray();
        foreach (var e in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("url", e.Url);
            writer.WriteString("outputPath", e.OutputPath);
            writer.WriteString("template", e.Template);
            if (e.Collection is not null)
                writer.WriteString("collection", e.Collection);
            if (e.Type is not null)
                writer.WriteString("type", e.Type);
            if (e.Language is not null)
                writer.WriteString("language", e.Language);
            writer.WriteString("routeSource", e.RouteSource);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        Console.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }
}
