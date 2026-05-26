using Bukit.Cli;
using Bukit.Cli.Cli.Parsing;
using Bukit.Cli.Cli.Rendering;
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
    var registry = BukitCliSpecs.CreateRegistry();
    var spec = registry.Resolve(command);

    if (spec is not null)
    {
        var tail = args.Skip(1).ToArray();
        if (tail.Any(x => x is "--help" or "-h"))
        {
            Console.WriteLine(CliHelpRenderer.Render(spec, $"bukit {spec.Name}"));
            return 0;
        }

        if (spec.Subcommands is null or { Count: 0 })
        {
            var parsed = CliParser.Parse(spec, tail);
            if (!parsed.IsSuccess)
            {
                Console.Error.WriteLine(CliErrorRenderer.Render(parsed.Diagnostics[0]));
                Console.Error.WriteLine(CliHelpRenderer.Render(spec, $"bukit {spec.Name}"));
                return 2;
            }

            var resolved = spec.Name switch
            {
                "build" => await BuildCommand.RunAsync(parsed.BoundCommand),
                "clone" => await CloneCommand.RunAsync(parsed.BoundCommand),
                "deploy" => await DeployCommand.RunAsync(parsed.BoundCommand),
                "dev" => await DevCommand.RunAsync(parsed.BoundCommand),
                "preview" => await PreviewCommand.RunAsync(parsed.BoundCommand),
                _ => (int?)null
            };
            if (resolved.HasValue)
            {
                return resolved.Value;
            }
        }
    }

    return command switch
    {
        "create" => await InitCommand.RunAsync(reader),
        "init" => await InitCommand.RunAsync(reader),
        "build" => await BuildCommand.RunAsync(reader),
        "deploy" => await DeployCommand.RunAsync(reader),
        "dev" => await DevCommand.RunAsync(args[1..]),
        "preview" => await PreviewCommand.RunAsync(reader),
        "clean" => await CleanCommand.RunAsync(reader),
        "completion" => await CompletionCommand.RunAsync(reader),
        "config" => await ConfigCommand.RunAsync(reader),
        "doctor" => await DoctorCommand.RunAsync(reader),
        "lint" => await LintCommand.RunAsync(reader),
        "plugin" => await PluginCommand.RunAsync(reader),
        "seo" => await SeoCommand.RunAsync(reader),
        "geo" => await GeoCommand.RunAsync(reader),
        "clone" => await CloneCommand.RunAsync(reader),
        "theme" => await ThemeCommand.RunAsync(reader),
        "template" => await TemplateCommand.RunAsync(reader),
        "intent" => await IntentCommand.RunAsync(reader),
        "visual" => await VisualCommand.RunAsync(reader),
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
