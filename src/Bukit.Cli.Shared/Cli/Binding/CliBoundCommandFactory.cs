using Bukit.Cli.Shared.Cli.Metadata;

namespace Bukit.Cli.Shared.Cli.Binding;

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

            var optionName = token;
            string? inlineValue = null;
            var eqIndex = token.IndexOf('=');
            if (eqIndex >= 0)
            {
                optionName = token.Substring(0, eqIndex);
                inlineValue = token.Substring(eqIndex + 1);
            }

            if (!optionMap.TryGetValue(optionName, out var optionSpec))
                continue;

            if (optionSpec.Type == CliOptionType.Flag)
            {
                options[optionSpec.Name] = "true";
                continue;
            }

            if (inlineValue is not null)
            {
                options[optionSpec.Name] = inlineValue;
                continue;
            }

            if (i + 1 < args.Count && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
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
