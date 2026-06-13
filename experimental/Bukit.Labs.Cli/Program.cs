using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;

var command = args.Length > 0 ? args[0] : null;
var commandArgs = args.Skip(1).ToArray();

if (command is null || command is "help" or "--help" or "-h")
{
    PrintHelp();
    return 0;
}

return command.ToLowerInvariant() switch
{
    "import" => await ImportCommand.RunAsync(BindPermissive(commandArgs)),
    "clone" => await CloneCommand.RunAsync(BindPermissive(commandArgs)),
    "notion" => await NotionCommand.RunAsync(BindPermissive(commandArgs)),
    "intent" => await IntentCommand.RunAsync(BindPermissive(commandArgs)),
    "visual" => await VisualCommand.RunAsync(BindPermissive(commandArgs)),
    "webhook" => await WebhookCommand.RunAsync(BindPermissive(commandArgs)),
    "data" => await DataCommand.RunAsync(BindPermissive(commandArgs)),
    "theme" => await ThemeCommand.RunAsync(BindPermissive(commandArgs)),
    _ => UnknownCommand(command)
};

static CliBoundCommand BindPermissive(IReadOnlyList<string> args)
{
    var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    var arguments = new List<string>();

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
            optionName = token[..eqIndex];
            inlineValue = token[(eqIndex + 1)..];
        }

        if (inlineValue is not null)
        {
            options[optionName] = inlineValue;
            continue;
        }

        if (i + 1 < args.Count && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
        {
            options[optionName] = args[++i];
            continue;
        }

        options[optionName] = "true";
    }

    return new CliBoundCommand(options, arguments);
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown labs command: {command}");
    Console.Error.WriteLine("Run 'bukit-labs help' to view supported labs commands.");
    return 2;
}

static void PrintHelp()
{
    Console.WriteLine("bukit-labs");
    Console.WriteLine();
    Console.WriteLine("Experimental commands:");
    Console.WriteLine("  import");
    Console.WriteLine("  clone");
    Console.WriteLine("  notion");
    Console.WriteLine("  intent");
    Console.WriteLine("  visual");
    Console.WriteLine("  webhook");
    Console.WriteLine("  data");
    Console.WriteLine("  theme");
}
