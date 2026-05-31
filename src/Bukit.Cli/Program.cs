using Bukit.Cli;
using Bukit.Cli.Cli.Parsing;
using Bukit.Cli.Cli.Rendering;
using Bukit.Cli.Commands;
using Bukit.Shared;

var command = args.Length > 0 ? args[0] : null;

if (command is null || command is "help" or "--help" or "-h")
{
    HelpPrinter.Print();
    return 0;
}

try
{
    var descriptors = BukitCliSpecs.CreateDescriptors();
    var descriptor = BukitCliSpecs.ResolveDescriptor(descriptors, command);

    if (descriptor is not null)
    {
        var tail = args.Skip(1).ToArray();
        if (tail.Any(x => x is "--help" or "-h"))
        {
            Console.WriteLine(CliHelpRenderer.Render(descriptor.Spec, $"bukit {descriptor.Spec.Name}"));
            return 0;
        }

        var parsed = CliParser.Parse(descriptor.Spec, tail);
        if (!parsed.IsSuccess)
        {
            Console.Error.WriteLine(CliErrorRenderer.Render(parsed.Diagnostics[0]));
            Console.Error.WriteLine(CliHelpRenderer.Render(descriptor.Spec, $"bukit {descriptor.Spec.Name}"));
            return 2;
        }

        return await descriptor.DispatchAsync(parsed);
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
