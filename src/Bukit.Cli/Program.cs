using Bukit.Cli;
using Bukit.Cli.Commands;

var reader = new ArgReader(args);
var command = reader.Command;

if (command is null || command is "help" or "--help" or "-h")
{
    HelpPrinter.Print();
    return 0;
}

if (command is not "version")
{
    Console.Error.WriteLine($"bukit {CliBuildInfo.Version}");
}

try
{
    return command switch
    {
        "create" => await InitCommand.RunAsync(reader),
        "init" => await InitCommand.RunAsync(reader),
        "build" => await BuildCommand.RunAsync(reader),
        "preview" => await PreviewCommand.RunAsync(reader),
        "clean" => await CleanCommand.RunAsync(reader),
        "doctor" => await DoctorCommand.RunAsync(reader),
        "plugin" => await PluginCommand.RunAsync(reader),
        "theme" => await ThemeCommand.RunAsync(reader),
        "intent" => await IntentCommand.RunAsync(reader),
        "webhook" => await WebhookCommand.RunAsync(reader),
        "version" => await VersionCommand.RunAsync(reader),
        _ => UnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    if (ex.InnerException is not null)
    {
        Console.Error.WriteLine(ex.InnerException.Message);
    }
    return 1;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    HelpPrinter.Print();
    return 2;
}
