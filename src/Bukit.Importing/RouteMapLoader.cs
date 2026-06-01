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

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    continue;

                if (trimmed.StartsWith("- source:"))
                {
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
