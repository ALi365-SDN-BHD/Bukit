using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Binding;

public static class CliBoundCommandFactory
{
    public static CliBoundCommand Create(IReadOnlyList<string> args, CliCommandSpec? spec = null)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<string>();

        var optionMap = BuildOptionMap(spec);

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("-", StringComparison.Ordinal))
            {
                arguments.Add(token);
                continue;
            }

            if (!optionMap.TryGetValue(token, out var optionSpec))
                continue;

            if (optionSpec.Type == CliOptionType.Flag)
            {
                options[optionSpec.Name] = "true";
                continue;
            }

            if (i + 1 < args.Count)
            {
                options[optionSpec.Name] = args[++i];
            }
        }

        return new CliBoundCommand(options, arguments);
    }

    private static Dictionary<string, CliOptionSpec> BuildOptionMap(CliCommandSpec? spec)
    {
        var map = new Dictionary<string, CliOptionSpec>(StringComparer.OrdinalIgnoreCase);

        void Collect(IReadOnlyList<CliOptionSpec>? opts)
        {
            if (opts is null) return;
            foreach (var o in opts)
            {
                map[o.Name] = o;
                if (!string.IsNullOrWhiteSpace(o.ShortName))
                    map[o.ShortName] = o;
            }
        }

        if (spec is not null)
        {
            Collect(spec.Options);
            if (spec.Subcommands is not null)
            {
                foreach (var sub in spec.Subcommands)
                    Collect(sub.Options);
            }
        }

        return map;
    }
}
