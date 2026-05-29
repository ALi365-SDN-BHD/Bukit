using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Binding;

public static class CliBoundCommandFactory
{
    public static CliBoundCommand Create(ArgReader reader, CliCommandSpec? spec = null)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<string>();

        if (spec?.Options is not null)
        {
            foreach (var option in spec.Options)
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
