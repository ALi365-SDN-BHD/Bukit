using Bukit.Cli;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Tests;

public static class CliTestHelper
{
    public static CliBoundCommand CreateCommand(string specName, string[] args)
    {
        var spec = BukitCliSpecs.CreateRegistry().Resolve(specName);
        return CliBoundCommandFactory.Create(args, spec);
    }
}
