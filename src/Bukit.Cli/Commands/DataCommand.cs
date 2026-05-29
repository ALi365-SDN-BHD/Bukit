using System.Text;
using System.Text.Json;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class DataCommand
{
    internal static void PrintModuleSummary(IReadOnlyList<ContentItem> items)
    {
        if (items.Count == 0)
        {
            Console.WriteLine("Data modules: (none)");
            return;
        }

        var byType = new Dictionary<string, List<ContentItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!MetaHelpers.IsDataItem(item)) continue;

            var type = "module";
            if (item.Meta.TryGetValue("type", out var t) && t is not null && !string.IsNullOrWhiteSpace(t.ToString()))
                type = t.ToString()!;

            if (!byType.ContainsKey(type))
                byType[type] = new List<ContentItem>();
            byType[type].Add(item);
        }

        if (byType.Count == 0)
        {
            Console.WriteLine("Data modules: (none)");
            return;
        }

        Console.WriteLine("Data modules:");
        foreach (var (type, moduleItems) in byType.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var source = "unknown";
            if (moduleItems.First().Meta.TryGetValue("sourceKey", out var sk) && sk is not null)
                source = sk.ToString()!;

            var sourceMode = "unknown";
            if (moduleItems.First().Meta.TryGetValue("sourceMode", out var sm) && sm is not null)
                sourceMode = sm.ToString()!;

            var allFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in moduleItems)
            {
                if (m.Fields is not null)
                    foreach (var f in m.Fields.Keys)
                        allFields.Add(f);
            }

            var fields = allFields.Count > 0 ? $"[{string.Join(", ", allFields.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))}]" : "";
            Console.WriteLine($"  {type,-14} ×{moduleItems.Count}  source={source,-10}  mode={sourceMode,-8} {fields}");
        }
    }

    internal static void PrintModuleDetail(IReadOnlyList<ContentItem> items, string moduleName)
    {
        var matching = new List<ContentItem>();
        foreach (var item in items)
        {
            if (!MetaHelpers.IsDataItem(item)) continue;
            var type = "module";
            if (item.Meta.TryGetValue("type", out var t) && t is not null && !string.IsNullOrWhiteSpace(t.ToString()))
                type = t.ToString()!;
            if (string.Equals(type, moduleName, StringComparison.OrdinalIgnoreCase))
                matching.Add(item);
        }

        if (matching.Count == 0)
        {
            Console.WriteLine($"Module '{moduleName}' not found.");
            return;
        }

        Console.WriteLine($"Module: {moduleName} ({matching.Count} items)");
        Console.WriteLine();
        foreach (var item in matching)
        {
            Console.WriteLine($"  {item.Id}");
            Console.WriteLine($"    Title: {item.Title}");
            Console.WriteLine($"    Slug:  {item.Slug}");
            if (item.Fields is { Count: > 0 })
            {
                Console.WriteLine($"    Fields:");
                foreach (var f in item.Fields.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"      {f.Key}: {f.Value.Value}");
            }

            Console.WriteLine();
        }
    }

    internal static string DumpModulesJson(IReadOnlyList<ContentItem> items)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteStartObject("modules");

        var byType = new SortedDictionary<string, List<ContentItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!MetaHelpers.IsDataItem(item)) continue;
            var type = "module";
            if (item.Meta.TryGetValue("type", out var t) && t is not null && !string.IsNullOrWhiteSpace(t.ToString()))
                type = t.ToString()!;

            if (!byType.ContainsKey(type))
                byType[type] = new List<ContentItem>();
            byType[type].Add(item);
        }

        foreach (var (type, moduleItems) in byType)
        {
            writer.WriteStartArray(type);
            foreach (var item in moduleItems)
            {
                writer.WriteStartObject();
                writer.WriteString("id", item.Id);
                writer.WriteString("title", item.Title);
                writer.WriteString("slug", item.Slug);
                if (item.Fields is { Count: > 0 })
                {
                    writer.WriteStartObject("fields");
                    foreach (var f in item.Fields.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        var val = f.Value.Value;
                        if (val is string s)
                            writer.WriteString(f.Key, s);
                        else if (val is bool b)
                            writer.WriteBoolean(f.Key, b);
                        else if (val is int or long or double or float)
                            writer.WriteNumber(f.Key, Convert.ToDouble(val));
                        else if (val is not null)
                            writer.WriteString(f.Key, val.ToString());
                        else
                            writer.WriteNull(f.Key);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static Task<int> RunAsync(ArgReader reader)
    {
        var spec = BukitCliSpecs.CreateRegistry().Resolve("data");
        var command = CliBoundCommandFactory.Create(reader, spec);
        return RunAsync(command);
    }

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        var config = ConfigLoader.Load(resolved.FullConfigPath);
        ConfigValidator.Validate(config);

        var factory = new DefaultContentProviderFactory();
        var contentPipeline = new ContentPipeline(factory, new ConsoleLogger(LogLevel.Info));
        var contentResult = await contentPipeline.ExecuteAsync(config, rootDir, new ConfigOverrides(), Path.Combine(rootDir, ".cache", "media"));

        var items = contentResult.Items;
        var sub = command.GetArgument(0) ?? "inspect";

        switch (sub)
        {
            case "inspect":
                var moduleName = command.GetString("--module");
                if (moduleName is not null)
                    PrintModuleDetail(items, moduleName);
                else
                    PrintModuleSummary(items);
                return 0;
            case "dump":
                var format = command.GetString("--format");
                if (format is not null && format != "json")
                {
                    Console.Error.WriteLine("Unsupported format. Only json is supported.");
                    return 1;
                }
                Console.WriteLine(DumpModulesJson(items));
                return 0;
            default:
                Console.Error.WriteLine($"Unknown subcommand: {sub}. Use inspect or dump.");
                return 1;
        }
    }
}
