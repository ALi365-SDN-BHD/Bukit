using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    PrintHelp();
    return 0;
}

var command = args[0];
var commandArgs = args.Skip(1).ToArray();

try
{
    return command switch
    {
        "import" => await ImportCommand.RunAsync(BindPermissive(commandArgs)),
        _ => UnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static CliBoundCommand BindPermissive(IReadOnlyList<string> args)
{
    var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    var arguments = new List<string>();

    for (var i = 0; i < args.Count; i++)
    {
        var token = args[i];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            arguments.Add(token);
            continue;
        }

        var optionName = token;
        string? value = "true";
        var eqIndex = token.IndexOf('=');
        if (eqIndex >= 0)
        {
            optionName = token[..eqIndex];
            value = token[(eqIndex + 1)..];
        }
        else if (i + 1 < args.Count && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
        {
            value = args[++i];
        }

        options[optionName] = value;
    }

    return new CliBoundCommand(options, arguments);
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown labs command: {command}");
    Console.Error.WriteLine("Run 'bukit-labs --help' to view supported commands.");
    return 2;
}

static void PrintHelp()
{
    Console.WriteLine("bukit-labs - experimental Bukit tooling");
    Console.WriteLine();
    Console.WriteLine("Usage: bukit-labs <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  import       HTML demo and seed import workflows");
}
