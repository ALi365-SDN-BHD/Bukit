using System.Text;
using System.Text.Json;
using Bukit.Cli;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Labs.Cli.Commands;

public static class DataCommand
{
    internal static void PrintModuleSummary(IReadOnlyList<ContentDocument> documents)
    {
        if (documents.Count == 0)
        {
            Console.WriteLine("Data modules: (none)");
            return;
        }

        var byType = new Dictionary<string, List<ContentDocument>>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            if (!ContentFieldReader.IsDataItem(document)) continue;

            var type = ContentFieldReader.GetContentType(document, "module");

            if (!byType.ContainsKey(type))
                byType[type] = new List<ContentDocument>();
            byType[type].Add(document);
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
            source = ContentFieldReader.GetText(moduleItems.First().CustomFields, "sourceKey") ?? source;

            var sourceMode = "unknown";
            sourceMode = ContentFieldReader.GetText(moduleItems.First().CustomFields, "sourceMode") ?? sourceMode;

            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in moduleItems)
            {
                var language = ContentFieldReader.GetText(m, "language");
                if (!string.IsNullOrWhiteSpace(language))
                    languages.Add(language);
            }
            var languageStr = languages.Count == 0 ? "-" : languages.Count == 1 ? languages.First() : "mixed";

            var fieldCount = 0;
            foreach (var m in moduleItems)
            {
                if (m.CustomFields is { Count: > 0 } && m.CustomFields.Count > fieldCount)
                    fieldCount = m.CustomFields.Count;
            }

            var allFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in moduleItems)
            {
                if (m.CustomFields is not null)
                    foreach (var f in m.CustomFields.Keys)
                        allFields.Add(f);
            }

            var fields = allFields.Count > 0 ? $"[{string.Join(", ", allFields.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))}]" : "";
            Console.WriteLine($"  {type,-14} ×{moduleItems.Count}  source={source,-10}  mode={sourceMode,-8}  lang={languageStr,-6}  fields={fieldCount}  {fields}");
        }
    }

    internal static void PrintModuleDetail(IReadOnlyList<ContentDocument> documents, string moduleName)
    {
        var matching = new List<ContentDocument>();
        foreach (var document in documents)
        {
            if (!ContentFieldReader.IsDataItem(document)) continue;
            var type = ContentFieldReader.GetContentType(document, "module");
            if (string.Equals(type, moduleName, StringComparison.OrdinalIgnoreCase))
                matching.Add(document);
        }

        if (matching.Count == 0)
        {
            Console.WriteLine($"Module '{moduleName}' not found.");
            return;
        }

        Console.WriteLine($"Module: {moduleName} ({matching.Count} items)");
        Console.WriteLine();
        foreach (var document in matching)
        {
            Console.WriteLine($"  {document.Id}");
            Console.WriteLine($"    Title: {document.Title}");
            Console.WriteLine($"    Slug:  {document.Slug}");
            if (document.CustomFields is { Count: > 0 })
            {
                Console.WriteLine($"    Fields:");
                foreach (var f in document.CustomFields.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"      {f.Key}: {f.Value.Value}");
            }

            Console.WriteLine();
        }
    }

    internal static string DumpModulesJson(IReadOnlyList<ContentDocument> documents)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteStartObject("modules");

        var byType = new SortedDictionary<string, List<ContentDocument>>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            if (!ContentFieldReader.IsDataItem(document)) continue;
            var type = ContentFieldReader.GetContentType(document, "module");

            if (!byType.ContainsKey(type))
                byType[type] = new List<ContentDocument>();
            byType[type].Add(document);
        }

        foreach (var (type, moduleItems) in byType)
        {
            writer.WriteStartArray(type);
            foreach (var document in moduleItems)
            {
                writer.WriteStartObject();
                writer.WriteString("id", document.Id);
                writer.WriteString("title", document.Title);
                writer.WriteString("slug", document.Slug);
                if (document.CustomFields is { Count: > 0 })
                {
                    writer.WriteStartObject("fields");
                    foreach (var f in document.CustomFields.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
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
