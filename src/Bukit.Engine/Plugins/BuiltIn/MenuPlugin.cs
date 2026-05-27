using System.Text.Json;

namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;
public sealed class MenuPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "menu";
    public string Version => "1.0.0";

    public void AfterBuild(BuildContext context)
    {
        var menus = context.Config.Site.Menus;
        if (menus is null || menus.Count == 0)
        {
            return;
        }

        context.Data["menus"] = menus;

        var jsonPath = Path.Combine(context.OutputDir, "menus.json");
        Directory.CreateDirectory(context.OutputDir);

        using var stream = File.Create(jsonPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        foreach (var (key, items) in menus)
        {
            writer.WriteStartArray(key);
            WriteMenuItems(writer, items);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WriteMenuItems(Utf8JsonWriter writer, IReadOnlyList<Config.MenuConfig>? items)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items.OrderBy(x => x.Weight))
        {
            writer.WriteStartObject();
            writer.WriteString("identifier", item.Identifier);
            writer.WriteString("name", item.Name);
            writer.WriteString("url", item.Url);

            if (item.Children is { Count: > 0 })
            {
                writer.WriteStartArray("children");
                WriteMenuItems(writer, item.Children);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
    }
}
