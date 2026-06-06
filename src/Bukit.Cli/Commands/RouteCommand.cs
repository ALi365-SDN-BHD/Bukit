using System.Text;
using System.Text.Json;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
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

        var entries = new List<RouteInspectEntry>();
        foreach (var document in contentResult.Documents)
        {
            if (ContentFieldReader.IsDataItem(document)) continue;

            var (route, routeSource) = RouteInventoryValidator.GenerateRouteWithSource(document, config.Site);

            var collection = NullIfEmpty(ContentFieldReader.GetCollection(document));
            var type = NullIfEmpty(ContentFieldReader.GetContentType(document));
            var language = ContentFieldReader.GetText(document, "language");

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

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
