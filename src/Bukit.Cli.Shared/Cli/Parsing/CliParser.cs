using System.Linq;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;

namespace Bukit.Cli.Shared.Cli.Parsing;

public static class CliParser
{
    public static CliParseResult Parse(CliCommandSpec command, IReadOnlyList<string> args)
    {
        if (command.Subcommands is { Count: > 0 } && args.Count > 0)
        {
            var firstToken = args[0];
            if (!firstToken.StartsWith("-", StringComparison.Ordinal))
            {
                var subSpec = command.Subcommands
                    .FirstOrDefault(s => string.Equals(s.Name, firstToken, StringComparison.OrdinalIgnoreCase));
                if (subSpec is null)
                {
                    subSpec = command.Subcommands
                        .FirstOrDefault(s => s.Aliases is not null && s.Aliases.Any(a => string.Equals(a, firstToken, StringComparison.OrdinalIgnoreCase)));
                }

                if (subSpec is not null)
                {
                    var remainingArgs = args.Skip(1).ToList();
                    var parentBound = new CliBoundCommand(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), new[] { firstToken }.ToList());
                    var innerResult = Parse(subSpec, remainingArgs);
                    return new SubcommandParseResult(command, parentBound, innerResult.Diagnostics, firstToken, innerResult);
                }
            }
        }

        var bound = CliBoundCommandFactory.Create(args, command);
        var diagnostics = Validate(command, args, bound);
        return new SimpleParseResult(command, bound, diagnostics);
    }

    private static List<CliDiagnostic> Validate(CliCommandSpec command, IReadOnlyList<string> args, CliBoundCommand bound)
    {
        var diagnostics = new List<CliDiagnostic>();

        var optionMap = BuildValidationOptionMap(command);

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("-", StringComparison.Ordinal))
                continue;

            var optionName = token;
            string? inlineValue = null;
            var eqIndex = token.IndexOf('=');
            if (eqIndex >= 0)
            {
                optionName = token.Substring(0, eqIndex);
                inlineValue = token.Substring(eqIndex + 1);
            }

            if (!optionMap.TryGetValue(optionName, out var spec))
            {
                diagnostics.Add(new CliDiagnostic("unknown-option", $"Unknown option: {optionName}"));
                continue;
            }

            if (spec.Type == CliOptionType.Flag)
            {
                if (inlineValue is not null)
                {
                    diagnostics.Add(new CliDiagnostic("invalid-option-value", $"Flag option {spec.Name} does not accept a value"));
                }
                continue;
            }

            if (inlineValue is not null)
            {
                ValidateOptionValue(spec, inlineValue, diagnostics);
                continue;
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("-", StringComparison.Ordinal))
            {
                diagnostics.Add(new CliDiagnostic("missing-option-value", $"Missing value for {spec.Name}"));
                continue;
            }

            var value = args[++i];
            ValidateOptionValue(spec, value, diagnostics);
        }

        var argumentSpecs = command.Arguments ?? Array.Empty<CliArgumentSpec>();
        for (var i = 0; i < argumentSpecs.Count; i++)
        {
            if (argumentSpecs[i].Required && bound.GetArgument(i) is null)
            {
                diagnostics.Add(new CliDiagnostic("missing-argument", $"Missing required argument: <{argumentSpecs[i].Name}>"));
            }
        }

        foreach (var spec in command.Options ?? Array.Empty<CliOptionSpec>())
        {
            if (spec.Required && bound.GetString(spec.Name) is null)
            {
                diagnostics.Add(new CliDiagnostic("missing-option", $"Missing required option: {spec.Name}"));
            }

            if (!string.IsNullOrWhiteSpace(spec.ConflictWith) &&
                bound.GetString(spec.Name) is not null &&
                bound.GetString(spec.ConflictWith) is not null)
            {
                diagnostics.Add(new CliDiagnostic("conflicting-options", $"Options {spec.Name} and {spec.ConflictWith} cannot be used together"));
            }
        }

        return diagnostics;
    }

    private static Dictionary<string, CliOptionSpec> BuildValidationOptionMap(CliCommandSpec command)
    {
        var map = new Dictionary<string, CliOptionSpec>(StringComparer.OrdinalIgnoreCase);
        var opts = command.Options ?? Array.Empty<CliOptionSpec>();
        foreach (var o in opts)
        {
            map[o.Name] = o;
            if (!string.IsNullOrWhiteSpace(o.ShortName))
                map[o.ShortName] = o;
        }

        return map;
    }

    private static void ValidateOptionValue(CliOptionSpec spec, string value, List<CliDiagnostic> diagnostics)
    {
        if (spec.Type == CliOptionType.Integer && !int.TryParse(value, out _))
        {
            diagnostics.Add(new CliDiagnostic("invalid-option-value", $"Invalid value for {spec.Name}: {value}"));
        }

        if (spec.AllowedValues is { Count: > 0 } && !spec.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            diagnostics.Add(new CliDiagnostic("invalid-option-value", $"Invalid value for {spec.Name}: {value}"));
        }
    }
}
