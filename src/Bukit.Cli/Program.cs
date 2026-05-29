using Bukit.Cli;
using Bukit.Cli.Cli.Parsing;
using Bukit.Cli.Cli.Rendering;
using Bukit.Cli.Commands;
using Bukit.Cli.Commands.DocsCheck;

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

        var parsed = CliParser.Parse(spec, tail);
        if (!parsed.IsSuccess)
        {
            Console.Error.WriteLine(CliErrorRenderer.Render(parsed.Diagnostics[0]));
            Console.Error.WriteLine(CliHelpRenderer.Render(spec, $"bukit {spec.Name}"));
            return 2;
        }

        if (parsed is SimpleParseResult simple)
        {
            var resolved = spec.Name switch
            {
                "build" => await BuildCommand.RunAsync(simple.BoundCommand),
                "clean" => await CleanCommand.RunAsync(simple.BoundCommand),
                "clone" => await CloneCommand.RunAsync(simple.BoundCommand),
                "completion" => await CompletionCommand.RunAsync(simple.BoundCommand),
                "deploy" => await DeployCommand.RunAsync(simple.BoundCommand),
                "dev" => await DevCommand.RunAsync(simple.BoundCommand),
                "docs" => await DocsCheckCommand.RunAsync(simple.BoundCommand),
                "doctor" => await DoctorCommand.RunAsync(simple.BoundCommand),
                "init" or "create" => await InitCommand.RunAsync(simple.BoundCommand),
                "lint" => await LintCommand.RunAsync(simple.BoundCommand),
                "preview" => await PreviewCommand.RunAsync(simple.BoundCommand),
                "version" => await VersionCommand.RunAsync(simple.BoundCommand),
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
        "config" => await ConfigCommand.RunAsync(reader),
        "plugin" => await PluginCommand.RunAsync(reader),
        "seo" => await SeoCommand.RunAsync(reader),
        "geo" => await GeoCommand.RunAsync(reader),
        "data" => await DataCommand.RunAsync(reader),
        "theme" => await ThemeCommand.RunAsync(reader),
        "template" => await TemplateCommand.RunAsync(reader),
        "intent" => await IntentCommand.RunAsync(reader),
        "visual" => await VisualCommand.RunAsync(reader),
        "webhook" => await WebhookCommand.RunAsync(reader),
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
