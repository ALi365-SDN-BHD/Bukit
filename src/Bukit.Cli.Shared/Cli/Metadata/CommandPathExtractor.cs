namespace Bukit.Cli.Shared.Cli.Metadata;

public static class CommandPathExtractor
{
    public static IReadOnlyList<string> ExtractAllCommandPaths(CliCommandRegistry registry)
    {
        var paths = new List<string>();

        foreach (var command in registry.Commands)
        {
            var prefix = new List<string>();
            CollectPaths(command, prefix, paths);
        }

        return paths;
    }

    public static IReadOnlyList<CliOptionSpec>? ExtractCommandOptions(string path, CliCommandRegistry registry)
    {
        var spec = ResolveSpec(path, registry);
        return spec?.Options;
    }

    public static CliCommandSpec? ResolveSpec(string path, CliCommandRegistry registry)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parts = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var current = registry.Resolve(parts[0]);
        if (current is null)
        {
            return null;
        }

        for (var i = 1; i < parts.Length; i++)
        {
            var subs = current.Subcommands;
            if (subs is null || subs.Count == 0)
            {
                return null;
            }

            CliCommandSpec? found = null;
            foreach (var sub in subs)
            {
                if (string.Equals(sub.Name, parts[i], StringComparison.OrdinalIgnoreCase))
                {
                    found = sub;
                    break;
                }
            }

            if (found is null)
            {
                return null;
            }

            current = found;
        }

        return current;
    }

    private static void CollectPaths(CliCommandSpec command, List<string> prefix, List<string> paths)
    {
        prefix.Add(command.Name);
        paths.Add(string.Join(" ", prefix));

        var subs = command.Subcommands;
        if (subs is { Count: > 0 })
        {
            foreach (var sub in subs)
            {
                CollectPaths(sub, prefix, paths);
            }
        }

        prefix.RemoveAt(prefix.Count - 1);
    }
}
