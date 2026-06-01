namespace Bukit.Importing;

internal static class RouteMapLoader
{
    internal static RouteMapConfig? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var config = new RouteMapConfig();
            var lines = File.ReadAllLines(path);
            var inPagesBlock = false;

            for (var lineNo = 0; lineNo < lines.Length; lineNo++)
            {
                var trimmed = lines[lineNo].Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    continue;

                if (!inPagesBlock && trimmed == "pages:")
                {
                    inPagesBlock = true;
                    continue;
                }

                if (trimmed.StartsWith("- source:"))
                {
                    inPagesBlock = true;
                    config.Pages.Add(new RouteMapPage
                    {
                        Source = ExtractYamlValue(trimmed, "- source:")
                    });
                }
                else if (config.Pages.Count > 0)
                {
                    var last = config.Pages[^1];
                    if (trimmed.StartsWith("route:"))
                        config.Pages[^1] = last with { Route = ExtractYamlValue(trimmed, "route:") };
                    else if (trimmed.StartsWith("type:"))
                        config.Pages[^1] = last with { Type = ExtractYamlValue(trimmed, "type:") };
                    else if (trimmed.StartsWith("template:"))
                        config.Pages[^1] = last with { Template = ExtractYamlValue(trimmed, "template:") };
                    else if (trimmed.StartsWith("slug:"))
                        config.Pages[^1] = last with { Slug = ExtractYamlValue(trimmed, "slug:") };
                    else if (trimmed.StartsWith("description:"))
                        config.Pages[^1] = last with { Description = ExtractYamlValue(trimmed, "description:") };
                    else if (!trimmed.StartsWith("-"))
                        Console.Error.WriteLine($"Route map line {lineNo + 1}: unknown field '{trimmed.Split(':')[0]}'");
                }
            }

            return config;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load route map '{path}': {ex.Message}");
            return null;
        }
    }

    private static string ExtractYamlValue(string line, string prefix)
    {
        var value = line[prefix.Length..].Trim();
        if (value.StartsWith('"') && value.EndsWith('"'))
            value = value[1..^1];
        else if (value.StartsWith('\'') && value.EndsWith('\''))
            value = value[1..^1];
        return value;
    }
}
