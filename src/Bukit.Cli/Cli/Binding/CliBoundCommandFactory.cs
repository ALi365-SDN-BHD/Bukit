using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Binding;

public static class CliBoundCommandFactory
{
    public static CliBoundCommand Create(ArgReader reader, CliCommandSpec? spec = null)
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
                        if (reader.HasFlag(option.Name))
                            options[option.Name] = "true";
                    }
                    else
                    {
                        var value = reader.GetOption(option.Name);
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
            var arg = reader.GetArg(i);
            if (arg is null) break;
            if (arg.StartsWith("-", StringComparison.Ordinal)) break;
            arguments.Add(arg);
        }

        return new CliBoundCommand(options, arguments);
    }
}
