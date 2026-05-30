using Bukit.Cli;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Tests;

public static class CliTestHelper
{
    public static CliBoundCommand CreateCommand(string specName, string[] args)
    {
        var spec = BukitCliSpecs.CreateRegistry().Resolve(specName);
        if (args.Length > 0 && string.Equals(args[0], specName, StringComparison.OrdinalIgnoreCase))
        {
            args = args.Skip(1).ToArray();
        }

        return CliBoundCommandFactory.Create(args, spec);
    }
}
