using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Commands.DocsCheck;

public static class CommandPathExtractor
{
    public static IReadOnlyList<string> ExtractAllCommandPaths(CliCommandRegistry registry)
    {
        var paths = new List<string>();
        foreach (var command in registry.Commands)
        {
            CollectPaths(command, command.Name, paths);
        }
        return paths;
    }

    public static IReadOnlyList<string> ExtractCommandOptions(CliCommandSpec spec)
    {
        if (spec.Options is null or { Count: 0 })
        {
            return Array.Empty<string>();
        }

        var names = new List<string>(spec.Options.Count);
        foreach (var option in spec.Options)
        {
            names.Add(option.Name);
        }
        return names;
    }

    public static CliCommandSpec? ResolveSpec(CliCommandRegistry registry, string commandPath)
    {
        var segments = commandPath.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var spec = registry.Resolve(segments[0]);
        if (spec is null)
        {
            return null;
        }

        for (var i = 1; i < segments.Length; i++)
        {
            if (spec.Subcommands is null)
            {
                return null;
            }

            spec = spec.Subcommands.FirstOrDefault(s =>
                string.Equals(s.Name, segments[i], StringComparison.OrdinalIgnoreCase));
            if (spec is null)
            {
                return null;
            }
        }

        return spec;
    }

    private static void CollectPaths(CliCommandSpec spec, string prefix, List<string> paths)
    {
        paths.Add(prefix);

        if (spec.Subcommands is null or { Count: 0 })
        {
            return;
        }

        foreach (var sub in spec.Subcommands)
        {
            CollectPaths(sub, $"{prefix} {sub.Name}", paths);
        }
    }
}
