using Bukit.Cli;
using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Cli.Parsing;
using Bukit.Cli.Cli.Rendering;
using Bukit.Cli.Commands;
using Bukit.Cli.Commands.DocsCheck;
using Bukit.Shared;

var command = args.Length > 0 ? args[0] : null;

if (command is null || command is "help" or "--help" or "-h")
{
    HelpPrinter.Print();
    return 0;
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

        if (parsed is SubcommandParseResult sub)
        {
            var merged = CliBoundCommand.MergeForSubcommand(sub.BoundCommand, sub.SubcommandName, sub.InnerResult.BoundCommand);
            var resolved = spec.Name switch
            {
                "config" => await ConfigCommand.RunAsync(merged),
                "plugin" => await PluginCommand.RunAsync(merged),
                "seo" => await SeoCommand.RunAsync(merged),
                "geo" => await GeoCommand.RunAsync(merged),
                "data" => await DataCommand.RunAsync(merged),
                "notion" => await NotionCommand.RunAsync(merged),
                "theme" => await ThemeCommand.RunAsync(merged),
                "template" => await TemplateCommand.RunAsync(merged),
                "intent" => await IntentCommand.RunAsync(merged),
                "visual" => await VisualCommand.RunAsync(merged),
                "webhook" => await WebhookCommand.RunAsync(merged),
                "route" => await RouteCommand.RunAsync(merged),
                "import" => await ImportCommand.RunAsync(merged),
                _ => (int?)null
            };
            if (resolved.HasValue)
            {
                return resolved.Value;
            }
        }
    }

    return UnknownCommand(command);
}
catch (CommandArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (ConfigException ex)
{
    Console.Error.WriteLine(ex.Message);
    if (ex.Code.HasValue)
        Console.Error.WriteLine($"  DiagnosticCode: {ex.Code.Value}");
    return 2;
}
catch (ContentException ex)
{
    Console.Error.WriteLine(ex.Message);
    if (ex.Code.HasValue)
        Console.Error.WriteLine($"  DiagnosticCode: {ex.Code.Value}");
    return 2;
}
catch (RenderException ex)
{
    Console.Error.WriteLine(ex.Message);
    if (ex.Code.HasValue)
        Console.Error.WriteLine($"  DiagnosticCode: {ex.Code.Value}");
    return 3;
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
