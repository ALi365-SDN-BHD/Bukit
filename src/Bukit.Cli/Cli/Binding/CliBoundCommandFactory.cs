using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Binding;

public static class CliBoundCommandFactory
{
    public static CliBoundCommand Create(IReadOnlyList<string> args, CliCommandSpec? spec = null)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<string>();
        var optionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void CollectOptions(IReadOnlyList<CliOptionSpec>? opts)
        {
            if (opts is null) return;
            foreach (var option in opts)
            {
                if (optionSet.Add(option.Name))
                {
                    if (option.Type == CliOptionType.Flag)
                    {
                        if (args.Any(a => string.Equals(a, option.Name, StringComparison.OrdinalIgnoreCase)))
                            options[option.Name] = "true";
                    }
                    else
                    {
                        var value = GetOption(args, option.Name);
                        if (value is not null)
                            options[option.Name] = value;
                    }
                }
            }
        }

        if (spec is not null)
        {
            CollectOptions(spec.Options);
            if (spec.Subcommands is not null)
            {
                foreach (var sub in spec.Subcommands)
                    CollectOptions(sub.Options);
            }
        }

        for (var i = 1; ; i++)
        {
            if (i >= args.Count) break;
            var arg = args[i];
            if (arg.StartsWith("-", StringComparison.Ordinal)) break;
            arguments.Add(arg);
        }

        return new CliBoundCommand(options, arguments);
    }

    private static string? GetOption(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..];
            }

            if (!string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Count)
            {
                return null;
            }

            return args[i + 1];
        }

        return null;
    }
}
