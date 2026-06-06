using System.Text;
using System.Text.Json;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class DataCommand
{
    private sealed record ModuleView(
        string Id,
        string Title,
        string Slug,
        string Type,
        string? Source,
        string? SourceMode,
        string? Language,
        IReadOnlyDictionary<string, ContentField> Fields);

    internal static void PrintModuleSummary(IReadOnlyList<ContentDocument> documents)
        => PrintModuleSummary(BuildModuleViews(documents));

    internal static void PrintModuleSummary(IReadOnlyList<ContentItem> items)
        => PrintModuleSummary(BuildModuleViews(items));

    private static void PrintModuleSummary(IReadOnlyList<ModuleView> modules)
    {
        if (modules.Count == 0)
        {
            Console.WriteLine("Data modules: (none)");
            return;
        }

        var byType = new Dictionary<string, List<ModuleView>>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules)
        {
            if (!byType.ContainsKey(module.Type))
                byType[module.Type] = new List<ModuleView>();
            byType[module.Type].Add(module);
        }

        if (byType.Count == 0)
        {
            Console.WriteLine("Data modules: (none)");
            return;
        }

        Console.WriteLine("Data modules:");
        foreach (var (type, moduleItems) in byType.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var source = string.IsNullOrWhiteSpace(moduleItems.First().Source) ? "unknown" : moduleItems.First().Source!;
            var sourceMode = string.IsNullOrWhiteSpace(moduleItems.First().SourceMode) ? "unknown" : moduleItems.First().SourceMode!;

            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in moduleItems)
            {
                if (!string.IsNullOrWhiteSpace(m.Language))
                    languages.Add(m.Language);
            }
            var languageStr = languages.Count == 0 ? "-" : languages.Count == 1 ? languages.First() : "mixed";

            var fieldCount = 0;
            foreach (var m in moduleItems)
            {
                if (m.Fields.Count > fieldCount)
                    fieldCount = m.Fields.Count;
            }

            var allFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in moduleItems)
            {
                foreach (var f in m.Fields.Keys)
                    allFields.Add(f);
            }

            var fields = allFields.Count > 0 ? $"[{string.Join(", ", allFields.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))}]" : "";
            Console.WriteLine($"  {type,-14} ×{moduleItems.Count}  source={source,-10}  mode={sourceMode,-8}  lang={languageStr,-6}  fields={fieldCount}  {fields}");
        }
    }

    internal static void PrintModuleDetail(IReadOnlyList<ContentDocument> documents, string moduleName)
        => PrintModuleDetail(BuildModuleViews(documents), moduleName);

    internal static void PrintModuleDetail(IReadOnlyList<ContentItem> items, string moduleName)
        => PrintModuleDetail(BuildModuleViews(items), moduleName);

    private static void PrintModuleDetail(IReadOnlyList<ModuleView> modules, string moduleName)
    {
        var matching = new List<ModuleView>();
        foreach (var module in modules)
        {
            if (string.Equals(module.Type, moduleName, StringComparison.OrdinalIgnoreCase))
                matching.Add(module);
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
            if (item.Fields.Count > 0)
            {
                Console.WriteLine($"    Fields:");
                foreach (var f in item.Fields.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"      {f.Key}: {f.Value.Value}");
            }

            Console.WriteLine();
        }
    }

    internal static string DumpModulesJson(IReadOnlyList<ContentDocument> documents)
        => DumpModulesJson(BuildModuleViews(documents));

    internal static string DumpModulesJson(IReadOnlyList<ContentItem> items)
        => DumpModulesJson(BuildModuleViews(items));

    private static string DumpModulesJson(IReadOnlyList<ModuleView> modules)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteStartObject("modules");

        var byType = new SortedDictionary<string, List<ModuleView>>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules)
        {
            if (!byType.ContainsKey(module.Type))
                byType[module.Type] = new List<ModuleView>();
            byType[module.Type].Add(module);
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
                if (item.Fields.Count > 0)
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

    private static IReadOnlyList<ModuleView> BuildModuleViews(IReadOnlyList<ContentDocument> documents)
    {
        return documents
            .Where(document => document.Publish.IsDataModule)
            .Select(document => new ModuleView(
                document.Record.Identity.Id,
                document.Record.Presentation.Title,
                document.Record.Identity.Slug,
                string.IsNullOrWhiteSpace(document.Record.Classification.Type) ? "module" : document.Record.Classification.Type,
                document.Record.Provenance.Source,
                "data",
                document.Record.Presentation.Language,
                document.CustomFields))
            .ToArray();
    }

    private static IReadOnlyList<ModuleView> BuildModuleViews(IReadOnlyList<ContentItem> items)
    {
        return items
            .Where(IsDataItem)
            .Select(item =>
            {
                var source = GetTextField(item.Fields, "sourceKey");
                var sourceMode = GetTextField(item.Fields, "sourceMode");
                return new ModuleView(
                    item.Id,
                    item.Title,
                    item.Slug,
                    GetTextField(item.Fields, "type") ?? "module",
                    source,
                    sourceMode,
                    GetTextField(item.Fields, "language"),
                    item.Fields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase));
            })
            .ToArray();
    }

    private static bool IsDataItem(ContentItem item)
    {
        var sourceMode = GetTextField(item.Fields, "sourceMode");
        if (!string.Equals(sourceMode, "data", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string? GetTextField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null || !fields.TryGetValue(key, out var field) || field.Value is null)
        {
            return null;
        }

        var value = field.Value.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

        var documents = contentResult.Documents;
        var sub = command.GetArgument(0) ?? "inspect";

        switch (sub)
        {
            case "inspect":
                var moduleName = command.GetString("--module");
                if (moduleName is not null)
                    PrintModuleDetail(documents, moduleName);
                else
                    PrintModuleSummary(documents);
                return 0;
            case "dump":
                var format = command.GetString("--format");
                if (format is not null && format != "json")
                {
                    Console.Error.WriteLine("Unsupported format. Only json is supported.");
                    return 1;
                }
                Console.WriteLine(DumpModulesJson(documents));
                return 0;
            default:
                Console.Error.WriteLine($"Unknown subcommand: {sub}. Use inspect or dump.");
                return 1;
        }
    }
}
