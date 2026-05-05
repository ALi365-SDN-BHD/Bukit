using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Parsing;

public static class CliParser
{
    public static CliParseResult Parse(CliCommandSpec command, IReadOnlyList<string> args)
    {
        var diagnostics = new List<CliDiagnostic>();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();
        var optionMap = (command.Options ?? Array.Empty<CliOptionSpec>())
            .SelectMany(x => new[] { x.Name, x.ShortName }.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => (Key: v!, Spec: x)))
            .ToDictionary(x => x.Key, x => x.Spec, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("-", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            if (!optionMap.TryGetValue(token, out var spec))
            {
                diagnostics.Add(new CliDiagnostic("unknown-option", $"Unknown option: {token}"));
                continue;
            }

            if (spec.Type == CliOptionType.Flag)
            {
                options[spec.Name] = "true";
                continue;
            }

            if (i + 1 >= args.Count)
            {
                diagnostics.Add(new CliDiagnostic("missing-option-value", $"Missing value for {spec.Name}"));
                continue;
            }

            var value = args[++i];
            if (spec.Type == CliOptionType.Integer && !int.TryParse(value, out _))
            {
                diagnostics.Add(new CliDiagnostic("invalid-option-value", $"Invalid value for {spec.Name}: {value}"));
                continue;
            }

            if (spec.AllowedValues is not null && spec.AllowedValues.Count > 0 && !spec.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new CliDiagnostic("invalid-option-value", $"Invalid value for {spec.Name}: {value}"));
                continue;
            }

            options[spec.Name] = value;
        }

        var argumentSpecs = command.Arguments ?? Array.Empty<CliArgumentSpec>();
        for (var i = 0; i < argumentSpecs.Count; i++)
        {
            if (argumentSpecs[i].Required && i >= positionals.Count)
            {
                diagnostics.Add(new CliDiagnostic("missing-argument", $"Missing required argument: <{argumentSpecs[i].Name}>"));
            }
        }

        foreach (var spec in command.Options ?? Array.Empty<CliOptionSpec>())
        {
            if (!string.IsNullOrWhiteSpace(spec.ConflictWith) && options.ContainsKey(spec.Name) && options.ContainsKey(spec.ConflictWith))
            {
                diagnostics.Add(new CliDiagnostic("conflicting-options", $"Options {spec.Name} and {spec.ConflictWith} cannot be used together"));
            }
        }

        return new CliParseResult(command, new CliBoundCommand(options, positionals), diagnostics);
    }
}
