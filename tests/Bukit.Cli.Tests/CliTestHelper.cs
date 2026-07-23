using Bukit.Cli;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;

namespace Bukit.Cli.Tests;

public static class CliTestHelper
{
    public static CliBoundCommand CreateCommand(string specName, string[] args)
    {
        var spec = BukitCliSpecs.CreateRegistry().Resolve(specName)
            ?? throw new ArgumentException($"Unknown CLI spec: {specName}", nameof(specName));
        if (args.Length > 0 && string.Equals(args[0], specName, StringComparison.OrdinalIgnoreCase))
        {
            args = args.Skip(1).ToArray();
        }

        var bindingOptions = (spec.Options ?? [])
            .Concat((spec.Subcommands ?? []).SelectMany(subcommand => subcommand.Options ?? []))
            .ToArray();
        var bindingSpec = spec with
        {
            Options = bindingOptions,
            Subcommands = null,
        };

        return CliParser.Parse(bindingSpec, args).BoundCommand;
    }
}
